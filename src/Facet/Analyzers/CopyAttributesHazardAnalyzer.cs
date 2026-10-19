using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Facet.Analyzers;

/// <summary>
/// Reports <c>FAC109</c> for each attribute <c>CopyAttributes</c> would carry onto a generated
/// member from a package assembly, which pins that package into the generated type's metadata.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the fix for that hazard is switched off by default. Dropping those
/// attributes automatically would change what an existing project generates, which a minor version
/// should not do — so <see cref="AttributeCopyMode.Preserve"/> keeps the previous behaviour, and
/// this makes the consequence visible instead. Without it the mode would be a feature nobody knew
/// to look for, protecting against a failure nobody knew they had.
/// </para>
/// <para>
/// Reported as <see cref="DiagnosticSeverity.Info"/> rather than a warning, deliberately. The
/// attribute resolves perfectly well whenever the consumer does reference the package, which is
/// the common case, so a warning would be noise for the projects that are fine — and a new warning
/// on upgrade breaks any build using <c>TreatWarningsAsErrors</c>, which is its own kind of
/// breaking change.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CopyAttributesHazardAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor PackageAttributeCopiedRule = new DiagnosticDescriptor(
        "FAC109",
        "Copied attribute pins a package into the generated type",
        "CopyAttributes carries '{0}' from '{1}.{2}' onto '{3}', pinning '{4}' into this assembly. Set CopyAttributesMode to ContractSafe to skip attributes from package assemblies.",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Copying an attribute copies its assembly reference. An attribute from a NuGet package therefore pins that package into the generated type's metadata, and anything that reflects over the property — Swagger schema generation and DataAnnotations validation both do — throws FileNotFoundException where the package is absent. The default mode preserves the previous behaviour rather than silently dropping such attributes, so this reports them instead. Set CopyAttributesMode to ContractSafe to skip package attributes, or name a single attribute in CopyAttributesExclude. Attributes the shared framework carries are not reported: [Column], [Table] and [Key] resolve wherever [Required] does.");

    private const string FacetAttributeName = "Facet.FacetAttribute";
    private const string WrapperAttributeName = "Facet.WrapperAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PackageAttributeCopiedRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(StartAnalysis);
    }

    private static void StartAnalysis(CompilationStartAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        var copyByDefault = GetBool(options, "build_property.Facet_CopyAttributes");
        var modeByDefault = GetMode(options, "build_property.Facet_CopyAttributesMode");

        context.RegisterSymbolAction(
            symbolContext => Analyze(symbolContext, copyByDefault, modeByDefault),
            SymbolKind.NamedType);
    }

    private static void Analyze(
        SymbolAnalysisContext context,
        bool copyByDefault,
        AttributeCopyMode modeByDefault)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        foreach (var attribute in type.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            if (name != FacetAttributeName && name != WrapperAttributeName)
                continue;

            // [Wrapper] has never inherited the project-wide default, so only [Facet] falls back.
            var copies = GetNamedBool(attribute, "CopyAttributes")
                ?? (name == FacetAttributeName && copyByDefault);

            if (!copies)
                continue;

            var mode = GetNamedMode(attribute, "CopyAttributesMode") ?? modeByDefault;
            if (mode != AttributeCopyMode.Preserve)
                continue;

            // Already excluded by name, so nothing would be copied and nothing to report.
            var excluded = GetNamedStrings(attribute, "CopyAttributesExclude");

            if (attribute.ConstructorArguments.Length < 1
                || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol sourceType)
            {
                continue;
            }

            ReportPackageAttributes(context, type, sourceType, excluded);
        }
    }

    private static void ReportPackageAttributes(
        SymbolAnalysisContext context,
        INamedTypeSymbol facetType,
        INamedTypeSymbol sourceType,
        ImmutableArray<string> excluded)
    {
        foreach (var member in sourceType.GetMembers())
        {
            if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public)
                continue;

            if (member is not IPropertySymbol and not IFieldSymbol)
                continue;

            foreach (var memberAttribute in member.GetAttributes())
            {
                var attributeType = memberAttribute.AttributeClass;
                if (attributeType is null)
                    continue;

                var attributeName = attributeType.ToDisplayString();

                if (!AttributeCopyPolicy.IsPackageAttribute(attributeName))
                    continue;

                if (excluded.Any(e => Matches(e, attributeName)))
                    continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    PackageAttributeCopiedRule,
                    facetType.Locations.FirstOrDefault() ?? Location.None,
                    attributeType.Name,
                    sourceType.Name,
                    member.Name,
                    facetType.Name,
                    attributeType.ContainingAssembly?.Name ?? "its assembly"));
            }
        }
    }

    /// <summary>
    /// A loose match against a configured exclusion, so an attribute the project already excludes
    /// is not also reported. Deliberately permissive: over-matching here only suppresses an
    /// advisory diagnostic.
    /// </summary>
    private static bool Matches(string pattern, string attributeFullName) =>
        pattern.EndsWith(".*", System.StringComparison.Ordinal)
            ? attributeFullName.StartsWith(pattern.Substring(0, pattern.Length - 1), System.StringComparison.OrdinalIgnoreCase)
            : attributeFullName.EndsWith(pattern, System.StringComparison.OrdinalIgnoreCase)
              || attributeFullName.EndsWith(pattern + "Attribute", System.StringComparison.OrdinalIgnoreCase);

    private static bool GetBool(AnalyzerConfigOptions options, string key) =>
        options.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) && parsed;

    private static AttributeCopyMode GetMode(AnalyzerConfigOptions options, string key) =>
        options.TryGetValue(key, out var value)
        && System.Enum.TryParse<AttributeCopyMode>(value, ignoreCase: true, out var parsed)
            ? parsed
            : AttributeCopyMode.Preserve;

    private static bool? GetNamedBool(AttributeData attribute, string name)
    {
        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == name && named.Value.Value is bool value)
                return value;
        }

        return null;
    }

    private static AttributeCopyMode? GetNamedMode(AttributeData attribute, string name)
    {
        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == name && named.Value.Value is int value)
                return (AttributeCopyMode)value;
        }

        return null;
    }

    private static ImmutableArray<string> GetNamedStrings(AttributeData attribute, string name)
    {
        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key != name || named.Value.Kind != TypedConstantKind.Array || named.Value.IsNull)
                continue;

            var builder = ImmutableArray.CreateBuilder<string>();
            foreach (var value in named.Value.Values)
            {
                if (value.Value is string s && !string.IsNullOrWhiteSpace(s))
                    builder.Add(s);
            }

            return builder.ToImmutable();
        }

        return ImmutableArray<string>.Empty;
    }
}
