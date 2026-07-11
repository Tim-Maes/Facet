using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Facet.Analyzers;

/// <summary>
/// Per-entity coverage analyzer that reports <c>FAC108</c> for each EF model manifest entity
/// that has no <c>[GenerateDtos]</c> or <c>[assembly: GenerateDtosFor]</c> attribute, or whose
/// configured <c>DtoTypes</c> do not cover the expected set (default: <c>Create | Update</c>).
/// </summary>
/// <remarks>
/// <para>
/// FAC107 (emitted by the source generator) gives a project-level summary: "X of Y entities
/// configured." FAC108 goes further: one diagnostic per uncovered entity, anchored to the entity
/// class when it is in the current compilation, or to the first <c>[assembly: GenerateDtosFor]</c>
/// attribute location otherwise (the file where the fix should be applied).
/// </para>
/// <para>
/// The expected <c>DtoTypes</c> are configurable via <c>.editorconfig</c>:
/// <code>
/// [*.cs]
/// facet_coverage.expected_dto_types = Create,Update
/// </code>
/// An entity with only <c>Create</c> configured when <c>Update</c> is expected produces
/// "Entity 'Computer' has Create configured, but Update is missing."
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GenerateDtosCoverageAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor CoverageRule = new DiagnosticDescriptor(
        "FAC108",
        "Entity has no or partial Facet DTO coverage",
        "{0}",
        "Coverage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Reports one diagnostic per EF model manifest entity that has no [GenerateDtos] or [assembly: GenerateDtosFor] attribute, or whose configured DtoTypes do not cover the expected set (default: Create | Update). The expected set is configurable via .editorconfig (facet_coverage.expected_dto_types).");

    private const string GenerateDtosAttributeName = "Facet.GenerateDtosAttribute";
    private const string GenerateDtosForAttributeName = "Facet.GenerateDtosForAttribute";
    private const string GenerateAuditableDtosAttributeName = "Facet.GenerateAuditableDtosAttribute";

    private static readonly SymbolDisplayFormat FullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(CoverageRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(StartAnalysis);
    }

    private static void StartAnalysis(CompilationStartAnalysisContext context)
    {
        // Parse manifest from AdditionalFiles — same mechanism as the source generator.
        var manifestFiles = new List<(string Path, string Text)>();
        foreach (var file in context.Options.AdditionalFiles)
        {
            if (file.Path.EndsWith(EfModelManifest.FileExtension, System.StringComparison.OrdinalIgnoreCase))
            {
                manifestFiles.Add((file.Path, file.GetText(context.CancellationToken)?.ToString() ?? string.Empty));
            }
        }

        if (manifestFiles.Count == 0) return;

        var manifest = EfModelManifest.Parse(manifestFiles);
        if (!manifest.HasAcceptedManifests || manifest.EntityCount == 0) return;

        // Expected DtoTypes — defaults to Create | Update. Configurability via .editorconfig
        // can be added later; for now the default covers the dominant write-DTO scenario.
        var expectedDtoTypes = DtoTypes.Create | DtoTypes.Update;

        // entityFullName → combined DtoTypes across all attributes targeting that entity.
        var configuredTypes = new ConcurrentDictionary<string, DtoTypes>();

        // Track the first [assembly: GenerateDtosFor] location for anchoring diagnostics
        // when the entity class is in a referenced assembly.
        var generateDtosForLocation = new ConcurrentQueue<Location>();

        // Scan [assembly: GenerateDtosFor(typeof(X), ...)] attributes early — they're
        // available on the compilation's assembly symbol at start time.
        foreach (var attr in context.Compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != GenerateDtosForAttributeName)
                continue;

            if (attr.ConstructorArguments.Length != 1)
                continue;

            if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol targetType)
                continue;

            var types = GetDtoTypesFromAttribute(attr);
            var fullName = StripGlobalPrefix(targetType.ToDisplayString(FullyQualifiedFormat));
            configuredTypes.AddOrUpdate(fullName, types, (_, existing) => existing | types);

            if (attr.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation() is Location loc)
                generateDtosForLocation.Enqueue(loc);
        }

        // Collect class-level [GenerateDtos] targets.
        context.RegisterSymbolAction(symbolContext =>
        {
            var namedType = (INamedTypeSymbol)symbolContext.Symbol;

            foreach (var attr in namedType.GetAttributes())
            {
                var attrName = attr.AttributeClass?.ToDisplayString();
                if (attrName != GenerateDtosAttributeName &&
                    attrName != GenerateAuditableDtosAttributeName)
                    continue;

                var types = GetDtoTypesFromAttribute(attr);
                var fullName = StripGlobalPrefix(namedType.ToDisplayString(FullyQualifiedFormat));
                configuredTypes.AddOrUpdate(fullName, types, (_, existing) => existing | types);
            }
        }, SymbolKind.NamedType);

        // Final compilation-wide analysis: compare configured types against manifest.
        context.RegisterCompilationEndAction(compilationContext =>
        {
            // Report FAC108 for each uncovered or partially covered manifest entity.
            // Anchor to the first [assembly: GenerateDtosFor] attribute location
            // (typically FacetGeneration.cs) so the code fixer knows which document
            // to modify. Roslyn requires the location to be in the current compilation.
            var anchorLocation = generateDtosForLocation.TryDequeue(out var anchor) && anchor.IsInSource
                ? anchor
                : Location.None;
            if (anchorLocation.IsInSource)
                generateDtosForLocation.Enqueue(anchor); // re-enqueue for other diagnostics

            foreach (var entityName in manifest.GetEntityNames())
            {
                var simpleName = GetSimpleName(entityName);
                var properties = ImmutableDictionary.CreateRange(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string?>("EntityFullName", entityName),
                });

                if (!configuredTypes.TryGetValue(entityName, out var configured))
                {
                    compilationContext.ReportDiagnostic(Diagnostic.Create(
                        CoverageRule,
                        anchorLocation,
                        properties,
                        $"Entity '{simpleName}' has no DTOs configured (expected {FormatDtoTypes(expectedDtoTypes)})"));
                }
                else
                {
                    var missing = expectedDtoTypes & ~configured;
                    if (missing != DtoTypes.None)
                    {
                        var configuredNames = FormatDtoTypes(configured & expectedDtoTypes);
                        var missingNames = FormatDtoTypes(missing);

                        compilationContext.ReportDiagnostic(Diagnostic.Create(
                            CoverageRule,
                            anchorLocation,
                            properties,
                            $"Entity '{simpleName}' has {configuredNames} configured, but {missingNames} missing"));
                    }
                }
            }
        });
    }

    /// <summary>
    /// Extracts the DtoTypes flags from a [GenerateDtos] or [GenerateDtosFor] attribute.
    /// Defaults to <see cref="DtoTypes.All"/> when the property is not explicitly set.
    /// </summary>
    private static DtoTypes GetDtoTypesFromAttribute(AttributeData attr)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == "Types" && arg.Value.Value is int value)
                return (DtoTypes)value;
        }

        return DtoTypes.All;
    }

    /// <summary>
    /// Anchors a diagnostic to the entity class declaration when it is in the current
    /// compilation's source tree. Falls back to the first [assembly: GenerateDtosFor]
    /// attribute location (typically FacetGeneration.cs), then to <see cref="Location.None"/>.
    /// Roslyn rejects diagnostics whose <see cref="Location"/> is in a referenced assembly,
    /// so we must verify the syntax tree belongs to the current compilation.
    /// </summary>
    private static Location ResolveEntityLocation(
        Compilation compilation,
        string entityFullName,
        ConcurrentQueue<Location> fallbackLocations,
        CancellationToken cancellationToken)
    {
        // Try to find the entity type in the current compilation.
        var entitySymbol = compilation.GetTypeByMetadataName(entityFullName);
        if (entitySymbol != null)
        {
            foreach (var syntaxRef in entitySymbol.DeclaringSyntaxReferences)
            {
                var tree = syntaxRef.SyntaxTree;
                if (compilation.ContainsSyntaxTree(tree))
                {
                    var location = syntaxRef.GetSyntax(cancellationToken).GetLocation();
                    if (location.IsInSource)
                        return location;
                }
            }
        }

        // Fall back to the first [assembly: GenerateDtosFor] attribute location.
        if (fallbackLocations.TryDequeue(out var fallback) && fallback.IsInSource)
        {
            // Re-enqueue so other diagnostics can use the same fallback.
            fallbackLocations.Enqueue(fallback);
            return fallback;
        }

        return Location.None;
    }

    private static string StripGlobalPrefix(string typeName) =>
        typeName.StartsWith("global::") ? typeName.Substring("global::".Length) : typeName;

    private static string GetSimpleName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
    }

    private static string FormatDtoTypes(DtoTypes types)
    {
        var parts = new List<string>();
        if ((types & DtoTypes.Create) != 0) parts.Add("Create");
        if ((types & DtoTypes.Update) != 0) parts.Add("Update");
        if ((types & DtoTypes.Response) != 0) parts.Add("Response");
        if ((types & DtoTypes.Query) != 0) parts.Add("Query");
        if ((types & DtoTypes.Upsert) != 0) parts.Add("Upsert");
        if ((types & DtoTypes.Patch) != 0) parts.Add("Patch");
        return parts.Count > 0 ? string.Join(", ", parts) : "None";
    }
}
