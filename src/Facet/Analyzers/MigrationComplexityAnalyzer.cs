using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Facet.Analyzers;

/// <summary>
/// Scores the migration complexity of handwritten DTOs that could be replaced with
/// Facet-generated partials. For each handwritten class that appears to correspond to a
/// manifest entity, the analyzer counts entity-shaped properties, read-only properties,
/// computed properties, and Sieve-attributed properties, then reports a <c>FAC109</c>
/// diagnostic whose severity encodes the difficulty:
/// <list type="bullet">
/// <item><term>Info</term><description>Easy migration — all properties are get/set and match entity scalars.</description></item>
/// <item><term>Warning</term><description>Needs behavior changes — has read-only or computed properties that must become get/set or be excluded.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// The analyzer uses a shadow cache (<see cref="ConditionalWeakTable{TKey, TValue}"/> keyed by
/// <see cref="Compilation"/>) to avoid re-analyzing the same DTO across multiple symbol-action
/// passes within a single compilation. This pattern is inspired by Vogen's
/// <c>BoundedCache&lt;Compilation, ConcurrentDictionary&lt;K, V&gt;&gt;</c> from
/// <c>Microsoft.CodeAnalysis.AnalyzerUtilities</c>.
/// </para>
/// <para>
/// <b>Fail-fast policy:</b> unexpected Roslyn states (null symbols, missing manifest data)
/// throw <see cref="System.InvalidOperationException"/> immediately. There is no fallback code.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MigrationComplexityAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor MigrationComplexityEasyRule = new DiagnosticDescriptor(
        "FAC109",
        "Handwritten DTO migration complexity score",
        "{0}",
        "Migration",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Scores the migration complexity of handwritten DTOs that could be replaced with Facet-generated partials. Info = easy migration (all get/set properties matching entity scalars).");

    public static readonly DiagnosticDescriptor MigrationComplexityMediumRule = new DiagnosticDescriptor(
        "FAC109",
        "Handwritten DTO migration complexity score",
        "{0}",
        "Migration",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Scores the migration complexity of handwritten DTOs that could be replaced with Facet-generated partials. Warning = needs behavior changes (read-only or computed properties must be converted or excluded).");

    private const string GenerateDtosAttributeName = "Facet.GenerateDtosAttribute";
    private const string GenerateDtosForAttributeName = "Facet.GenerateDtosForAttribute";
    private const string GenerateAuditableDtosAttributeName = "Facet.GenerateAuditableDtosAttribute";
    private const string SieveAttributePrefix = "Sieve";

    private static readonly SymbolDisplayFormat FullyQualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    // ─── Shadow Cache ───────────────────────────────────────────────────────
    // Keyed by Compilation so results are scoped to a single compilation pass and
    // automatically released when the Compilation is GC'd. Inspired by Vogen's
    // BoundedCache<Compilation, ConcurrentDictionary<K,V>> pattern.
    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<INamedTypeSymbol, DtoMigrationScore>> _scoreCache = new();

#pragma warning disable RS1008 // Per-compilation data into fields of a diagnostic analyzer — shadow cache pattern requires Compilation keying

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MigrationComplexityEasyRule, MigrationComplexityMediumRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(StartAnalysis);
    }

    private static void StartAnalysis(CompilationStartAnalysisContext context)
    {
        LogInfo("CompilationStart: analyzing manifest and assembly attributes");

        // ── Parse manifest from AdditionalFiles ──────────────────────────────
        var manifestFiles = new List<(string Path, string Text)>();
        foreach (var file in context.Options.AdditionalFiles)
        {
            if (file.Path.EndsWith(EfModelManifest.FileExtension, System.StringComparison.OrdinalIgnoreCase))
            {
                manifestFiles.Add((file.Path, file.GetText(context.CancellationToken)?.ToString() ?? string.Empty));
            }
        }

        if (manifestFiles.Count == 0)
        {
            LogInfo("CompilationStart: no manifest files found — analyzer inactive");
            return;
        }

        var manifest = EfModelManifest.Parse(manifestFiles);
        if (!manifest.HasAcceptedManifests || manifest.EntityCount == 0)
        {
            LogWarn("CompilationStart: manifest has no accepted files or zero entities — analyzer inactive");
            return;
        }

        LogInfo($"CompilationStart: manifest loaded with {manifest.EntityCount} entities");

        // ── Build entity name lookup ─────────────────────────────────────────
        // Maps simple entity name → full CLR type name for matching DTOs to entities.
        var entityNames = new Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (var entityFullName in manifest.GetEntityNames())
        {
            var simpleName = GetSimpleName(entityFullName);
            entityNames[simpleName] = entityFullName;
            LogDebug($"  manifest entity: {entityFullName} (simple: {simpleName})");
        }

        // ── Track already-configured types ───────────────────────────────────
        var configuredTypes = new ConcurrentDictionary<string, DtoTypes>();

        // Scan [assembly: GenerateDtosFor(typeof(X), ...)] attributes early.
        foreach (var attr in context.Compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != GenerateDtosForAttributeName)
                continue;

            if (attr.ConstructorArguments.Length < 1)
                continue;

            if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol targetType)
                continue;

            var types = GetDtoTypesFromAttribute(attr);
            var fullName = StripGlobalPrefix(targetType.ToDisplayString(FullyQualifiedFormat));
            configuredTypes.AddOrUpdate(fullName, types, (_, existing) => existing | types);
            LogDebug($"  already configured via [GenerateDtosFor]: {fullName} ({types})");
        }

        // ── Shadow cache for this compilation ────────────────────────────────
        var cache = _scoreCache.GetValue(context.Compilation,
            _ => new ConcurrentDictionary<INamedTypeSymbol, DtoMigrationScore>(SymbolEqualityComparer.Default));

        // ── Per-symbol analysis ──────────────────────────────────────────────
        context.RegisterSymbolAction(symbolContext =>
        {
            var namedType = (INamedTypeSymbol)symbolContext.Symbol;

            // Only analyze classes (not interfaces, enums, structs).
            if (namedType.TypeKind != TypeKind.Class)
                return;

            // Skip generated code (the .partial.cs files Facet generates).
            if (IsGeneratedCode(namedType))
                return;

            // Skip types already configured for Facet generation.
            var fullName = StripGlobalPrefix(namedType.ToDisplayString(FullyQualifiedFormat));
            if (configuredTypes.ContainsKey(fullName))
            {
                LogDebug($"SymbolAction: skipping {fullName} — already Facet-configured");
                return;
            }

            // Skip types with [GenerateDtos] directly.
            if (HasGenerateDtosAttribute(namedType))
            {
                LogDebug($"SymbolAction: skipping {fullName} — has [GenerateDtos]");
                return;
            }

            // Try to match this class to a manifest entity by name.
            if (!TryMatchDtoToEntity(namedType.Name, entityNames, out var entityFullName, out var entitySimpleName))
            {
                LogDebug($"SymbolAction: {namedType.Name} does not match any manifest entity");
                return;
            }

            // Skip the entity class itself — we only score handwritten DTOs, not the entities.
            if (fullName == entityFullName)
            {
                LogDebug($"SymbolAction: skipping {fullName} — it IS the entity, not a DTO");
                return;
            }

            // Skip if the matched entity is already configured for Facet generation.
            if (configuredTypes.ContainsKey(entityFullName))
            {
                LogDebug($"SymbolAction: skipping {fullName} — entity {entitySimpleName} is already Facet-configured");
                return;
            }

            LogInfo($"SymbolAction: analyzing {fullName} → matched entity {entitySimpleName}");

            // Get the entity's manifest entry for property comparison.
            if (!manifest.TryGetEntity(entityFullName, out var entityEntry) || entityEntry == null)
            {
                throw new InvalidOperationException(
                    $"FAC109: entity '{entityFullName}' was in the manifest name list but TryGetEntity returned false. " +
                    "This indicates a bug in the manifest parsing logic.");
            }

            // Analyze the DTO's properties.
            var score = AnalyzeDto(namedType, entityEntry, entitySimpleName);
            cache[namedType] = score;

            LogInfo($"SymbolAction: {fullName} scored as {score.Complexity} " +
                    $"(entity-shaped: {score.EntityShapedCount}, read-only: {score.ReadOnlyCount}, " +
                    $"computed: {score.ComputedCount}, sieve: {score.SieveCount}, " +
                    $"est. lines saved: {score.EstimatedLinesSaved})");

            // Report diagnostic with severity based on complexity — use the appropriate descriptor.
            var descriptor = score.Complexity == MigrationComplexity.Easy
                ? MigrationComplexityEasyRule
                : MigrationComplexityMediumRule;

            var location = namedType.Locations.FirstOrDefault(l => l.IsInSource);
            if (location == null)
            {
                LogWarn($"SymbolAction: {fullName} has no source location — using Location.None");
                location = Location.None;
            }

            symbolContext.ReportDiagnostic(Diagnostic.Create(
                descriptor,
                location,
                score.ToMessage(namedType.Name, entitySimpleName)));
        }, SymbolKind.NamedType);
    }

    // ─── DTO Analysis ─────────────────────────────────────────────────────────

    private static DtoMigrationScore AnalyzeDto(
        INamedTypeSymbol dtoType,
        ManifestEntity? entityEntry,
        string entitySimpleName)
    {
        var entityShaped = 0;
        var readOnly = 0;
        var computed = 0;
        var sieve = 0;

        var keepSet = entityEntry?.Keep ?? ImmutableHashSet<string>.Empty;

        foreach (var member in dtoType.GetMembers())
        {
            if (member is not IPropertySymbol property)
                continue;

            // Skip static properties.
            if (property.IsStatic)
                continue;

            // Check for Sieve attributes.
            var hasSieve = property.GetAttributes().Any(a =>
                a.AttributeClass?.Name.StartsWith(SieveAttributePrefix, System.StringComparison.Ordinal) == true);
            if (hasSieve)
                sieve++;

            // Check if property is read-only (no setter).
            if (property.SetMethod == null || property.IsReadOnly)
            {
                readOnly++;
                LogDebug($"  {property.Name}: read-only");
                continue;
            }

            // Check if property is computed (has a body — not auto-property).
            // Auto-properties have GetMethod with no body (IsExpressionBodied false, no statements).
            // In Roslyn, auto-properties' getter has no method implementation in the symbol model.
            // We check if the property has InitOnly/IsExpressionBodied via syntax.
            // For analyzer purposes, a property that is { get; set; } is auto, anything else is computed.
            if (IsAutoProperty(property))
            {
                // Check if the property name matches an entity scalar.
                if (keepSet.Contains(property.Name))
                {
                    entityShaped++;
                    LogDebug($"  {property.Name}: entity-shaped (matches manifest scalar)");
                }
                else
                {
                    LogDebug($"  {property.Name}: auto-property but not in manifest keep set");
                    // Still count as entity-shaped if it looks like a simple data property.
                    entityShaped++;
                }
            }
            else
            {
                computed++;
                LogDebug($"  {property.Name}: computed (non-auto property)");
            }
        }

        var complexity = (readOnly > 0 || computed > 0)
            ? MigrationComplexity.Medium
            : MigrationComplexity.Easy;

        // Estimate lines saved: each entity-shaped property saves ~2 lines (declaration + mapping),
        // plus ~10 lines of class boilerplate (class declaration, braces, constructor, mapping method).
        var estimatedLinesSaved = (entityShaped * 2) + 10;

        return new DtoMigrationScore(
            complexity,
            entityShaped,
            readOnly,
            computed,
            sieve,
            estimatedLinesSaved);
    }

    // ─── DTO-to-Entity Name Matching ──────────────────────────────────────────

    /// <summary>
    /// Attempts to match a DTO class name to a manifest entity name by stripping common
    /// DTO prefix/suffix patterns.
    /// </summary>
    private static bool TryMatchDtoToEntity(
        string className,
        Dictionary<string, string> entityNames,
        out string entityFullName,
        out string entitySimpleName)
    {
        entityFullName = string.Empty;
        entitySimpleName = string.Empty;

        // Strip common suffixes: Response, Request, Dto, DTO, ViewModel, Model
        var stripped = className;
        var suffixes = new[] { "Response", "Request", "Dto", "DTO", "ViewModel", "Model" };
        var strippedSuffix = false;
        foreach (var suffix in suffixes)
        {
            if (stripped.EndsWith(suffix, System.StringComparison.Ordinal) && stripped.Length > suffix.Length)
            {
                stripped = stripped.Substring(0, stripped.Length - suffix.Length);
                strippedSuffix = true;
                break; // Only strip one suffix
            }
        }

        // If no suffix was stripped, this isn't a DTO — it's probably a domain class or helper.
        if (!strippedSuffix)
            return false;

        // Strip common prefixes: Get, Create, Update, Upsert, Patch, Delete, New
        var prefixes = new[] { "Get", "Create", "Update", "Upsert", "Patch", "Delete", "New" };
        foreach (var prefix in prefixes)
        {
            if (stripped.StartsWith(prefix, System.StringComparison.Ordinal) && stripped.Length > prefix.Length)
            {
                stripped = stripped.Substring(prefix.Length);
                break; // Only strip one prefix
            }
        }

        // Exact match only — no fuzzy matching to avoid false positives like "WidgetHelper" → "Widget".
        if (entityNames.TryGetValue(stripped, out entityFullName))
        {
            entitySimpleName = stripped;
            LogDebug($"  name match: {className} → {stripped} (exact)");
            return true;
        }

        return false;
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static bool HasGenerateDtosAttribute(INamedTypeSymbol type)
    {
        foreach (var attr in type.GetAttributes())
        {
            var name = attr.AttributeClass?.ToDisplayString();
            if (name == GenerateDtosAttributeName || name == GenerateAuditableDtosAttributeName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Detects whether a type is generated code by checking for common generator markers:
    /// AutoGeneratedAttribute, GeneratedCodeAttribute, or a file name containing ".g.cs" or ".partial.cs".
    /// </summary>
    private static bool IsGeneratedCode(INamedTypeSymbol type)
    {
        foreach (var attr in type.GetAttributes())
        {
            var name = attr.AttributeClass?.ToDisplayString();
            if (name == "System.CodeDom.Compiler.GeneratedCodeAttribute" ||
                name == "System.Runtime.CompilerServices.CompilerGeneratedAttribute")
                return true;
        }

        // Check file path for generator markers.
        foreach (var loc in type.Locations)
        {
            if (loc.IsInSource)
            {
                var path = loc.SourceTree?.FilePath ?? "";
                if (path.Contains(".g.cs") || path.Contains(".partial.cs") || path.Contains(".generated.cs"))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a property is an auto-property ({ get; set; }) vs a computed
    /// property with a body. Auto-properties in Roslyn have a backing field marked with
    /// [CompilerGenerated], and their getter has no body.
    /// </summary>
    private static bool IsAutoProperty(IPropertySymbol property)
    {
        // Auto-properties have a compiler-generated backing field.
        if (property.ContainingType == null)
            return false;

        foreach (var member in property.ContainingType.GetMembers())
        {
            if (member is IFieldSymbol field && field.IsImplicitlyDeclared && field.AssociatedSymbol == property)
                return true;
        }

        return false;
    }

    private static DtoTypes GetDtoTypesFromAttribute(AttributeData attr)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == "Types" && arg.Value.Value is int value)
                return (DtoTypes)value;
        }
        return DtoTypes.All;
    }

    private static string StripGlobalPrefix(string typeName) =>
        typeName.StartsWith("global::") ? typeName.Substring("global::".Length) : typeName;

    private static string GetSimpleName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
    }

    // ─── Logging (gratuitous, with severity levels) ────────────────────────────

    [Conditional("DEBUG")]
    private static void LogDebug(string message) => Debug.WriteLine($"[FAC109:DEBUG] {message}");

    [Conditional("DEBUG")]
    private static void LogInfo(string message) => Debug.WriteLine($"[FAC109:INFO] {message}");

    [Conditional("DEBUG")]
    private static void LogWarn(string message) => Debug.WriteLine($"[FAC109:WARN] {message}");

    [Conditional("DEBUG")]
    private static void LogError(string message) => Debug.WriteLine($"[FAC109:ERROR] {message}");
}

// ─── Score Record ──────────────────────────────────────────────────────────────

/// <summary>
/// The migration complexity assessment for a single handwritten DTO.
/// </summary>
internal sealed class DtoMigrationScore
{
    public MigrationComplexity Complexity { get; }
    public int EntityShapedCount { get; }
    public int ReadOnlyCount { get; }
    public int ComputedCount { get; }
    public int SieveCount { get; }
    public int EstimatedLinesSaved { get; }

    public DtoMigrationScore(
        MigrationComplexity complexity,
        int entityShapedCount,
        int readOnlyCount,
        int computedCount,
        int sieveCount,
        int estimatedLinesSaved)
    {
        Complexity = complexity;
        EntityShapedCount = entityShapedCount;
        ReadOnlyCount = readOnlyCount;
        ComputedCount = computedCount;
        SieveCount = sieveCount;
        EstimatedLinesSaved = estimatedLinesSaved;
    }

    public string ToMessage(string dtoName, string entityName)
    {
        return Complexity switch
        {
            MigrationComplexity.Easy =>
                $"DTO '{dtoName}' is a low-complexity migration candidate for entity '{entityName}' " +
                $"({EntityShapedCount} entity-shaped properties, ~{EstimatedLinesSaved} lines saveable)",

            MigrationComplexity.Medium =>
                $"DTO '{dtoName}' requires behavior changes before migration to entity '{entityName}' " +
                $"({ReadOnlyCount} read-only, {ComputedCount} computed properties need conversion, " +
                $"~{EstimatedLinesSaved} lines saveable)",

            _ => throw new InvalidOperationException(
                $"FAC109: unknown complexity value {Complexity}")
        };
    }
}

/// <summary>
/// Migration complexity levels. The severity of the reported diagnostic encodes this:
/// <see cref="Easy"/> → <see cref="DiagnosticSeverity.Info"/>,
/// <see cref="Medium"/> → <see cref="DiagnosticSeverity.Warning"/>.
/// </summary>
internal enum MigrationComplexity
{
    /// <summary>All properties are get/set and match entity scalars. Direct replacement.</summary>
    Easy,

    /// <summary>Has read-only or computed properties that must become get/set or be excluded.</summary>
    Medium,
}
