using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet.Analyzers;

/// <summary>
/// Reports the two ways a contract assembly can quietly fail to be one: a generated property whose
/// type still comes from the layer the facet was shaped from (<c>FAC116</c>), and a mapping hook
/// configured on a <c>[Facet]</c> whose mappings are suppressed, so it can never run
/// (<c>FAC117</c>).
/// </summary>
/// <remarks>
/// Both only apply where <c>Facet_GenerateMappings</c> is <c>false</c> — the setting by which a
/// project declares that its facets are contracts and their mapping belongs elsewhere. Without
/// that declaration neither is a mistake, so neither is reported.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FacetContractAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor SourceTypeOnContractRule = new DiagnosticDescriptor(
        "FAC116",
        "Contract facet exposes a type from the layer it was shaped from",
        "'{0}.{1}' is typed '{2}', which comes from the same assembly as '{3}'. This assembly suppresses its mappings to avoid referencing that layer, but the property type references it anyway. Add the corresponding facet to NestedFacets on the [Facet] attribute, or exclude the property.",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Facet copies a property's type across as-is, so a property whose type is a class from the source layer gives the generated facet a reference to that layer — defeating the point of a contract assembly even with the mappings and attributes suppressed. NestedFacets rewrites such a property to the corresponding facet type. Only reported where Facet_GenerateMappings is false, since that is where the reference is meant to be avoided.");

    public static readonly DiagnosticDescriptor InertConfigurationRule = new DiagnosticDescriptor(
        "FAC117",
        "Facet mapping hook cannot run because mappings are suppressed",
        "'{0}' sets {1}, but this project sets Facet_GenerateMappings to false, so the members that would call it are never generated and the hook has no effect. Move the custom mapping to the project that holds the mapping for this facet, which is the only one that can see both layers.",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A [Facet] hook — Configuration, BeforeMapConfiguration, AfterMapConfiguration or ToSourceConfiguration — is invoked from the constructor, FromSource or ToSource, all of which live in the mappings half that Facet_GenerateMappings=false suppresses. The hook therefore compiles and does nothing. In a layered build the custom mapping belongs with the mapping itself, in the project that references both the source layer and the contracts; that project is also the only one whose configuration type can name both types in a single signature.");

    private const string FacetAttributeName = "Facet.FacetAttribute";

    private static readonly ImmutableArray<string> HookArguments = ImmutableArray.Create(
        "Configuration",
        "BeforeMapConfiguration",
        "AfterMapConfiguration",
        "ToSourceConfiguration");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(SourceTypeOnContractRule, InertConfigurationRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(StartAnalysis);
    }

    private static void StartAnalysis(CompilationStartAnalysisContext context)
    {
        // Only a project that has declared its facets to be contracts can be failing to be one.
        if (!IsContractProject(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions))
            return;

        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static bool IsContractProject(AnalyzerConfigOptions options) =>
        options.TryGetValue("build_property.Facet_GenerateMappings", out var value)
        && bool.TryParse(value, out var generateMappings)
        && !generateMappings;

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != FacetAttributeName)
                continue;

            if (attribute.ConstructorArguments.Length < 1
                || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol sourceType)
            {
                continue;
            }

            ReportInertHooks(context, type, attribute);
            ReportSourceLayerTypes(context, type, sourceType, attribute);
        }
    }

    private static void ReportInertHooks(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        AttributeData attribute)
    {
        var configured = attribute.NamedArguments
            .Where(a => HookArguments.Contains(a.Key) && a.Value.Value is INamedTypeSymbol)
            .Select(a => a.Key)
            .ToList();

        if (configured.Count == 0)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            InertConfigurationRule,
            AttributeLocation(attribute, type),
            type.Name,
            string.Join(" and ", configured)));
    }

    /// <summary>
    /// Reports each generated property still typed as something from the source type's assembly.
    /// </summary>
    /// <remarks>
    /// The comparison is against the source type's own assembly rather than "anything external",
    /// which keeps it exact: <c>string</c> and <c>List&lt;T&gt;</c> come from other assemblies too,
    /// and referencing the framework was never the problem.
    /// </remarks>
    private static void ReportSourceLayerTypes(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        INamedTypeSymbol sourceType,
        AttributeData attribute)
    {
        var sourceAssembly = sourceType.ContainingAssembly;
        if (sourceAssembly is null)
            return;

        // A facet in the same assembly as its source has nothing to leak into.
        if (SymbolEqualityComparer.Default.Equals(sourceAssembly, context.Compilation.Assembly))
            return;

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.IsStatic || property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public)
                continue;

            var offending = FindSourceLayerType(property.Type, sourceAssembly);
            if (offending is null)
                continue;

            // Anchored on the attribute, not the property: the property is generated, and a
            // diagnostic reported inside generated code is suppressed — but the attribute is also
            // simply where the fix goes, since NestedFacets is set there.
            context.ReportDiagnostic(Diagnostic.Create(
                SourceTypeOnContractRule,
                AttributeLocation(attribute, type),
                type.Name,
                property.Name,
                offending.ToDisplayString(),
                sourceType.ToDisplayString()));
        }
    }

    /// <summary>
    /// Finds a type from <paramref name="sourceAssembly"/> in a property's type, looking through
    /// arrays and generic arguments so <c>List&lt;OrderUseCaseModel&gt;</c> is caught as readily as
    /// a bare <c>OrderUseCaseModel</c>.
    /// </summary>
    private static ITypeSymbol? FindSourceLayerType(ITypeSymbol type, IAssemblySymbol sourceAssembly)
    {
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        return Walk(type);

        ITypeSymbol? Walk(ITypeSymbol current)
        {
            if (!seen.Add(current))
                return null;

            if (current is IArrayTypeSymbol array)
                return Walk(array.ElementType);

            if (SymbolEqualityComparer.Default.Equals(current.ContainingAssembly, sourceAssembly))
                return current;

            if (current is INamedTypeSymbol named)
            {
                foreach (var argument in named.TypeArguments)
                {
                    var found = Walk(argument);
                    if (found is not null)
                        return found;
                }
            }

            return null;
        }
    }

    private static Location AttributeLocation(AttributeData attribute, INamedTypeSymbol fallback)
    {
        var reference = attribute.ApplicationSyntaxReference;

        return reference is not null
            ? Location.Create(reference.SyntaxTree, reference.Span)
            : fallback.Locations.FirstOrDefault() ?? Location.None;
    }
}
