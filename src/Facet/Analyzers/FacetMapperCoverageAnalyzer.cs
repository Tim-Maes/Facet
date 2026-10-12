using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet.Analyzers;

/// <summary>
/// Reports <c>FAC115</c> for a facet declared by a referenced contract assembly that this
/// assembly's mappers do not cover.
/// </summary>
/// <remarks>
/// <para>
/// A facet in a contract assembly built with <c>Facet_GenerateMappings=false</c> has no mapping
/// members of its own, so forgetting its <c>[assembly: FacetMapper]</c> registration produces
/// nothing to notice: no error, no warning, and no missing method until something tries to call
/// one. This closes that gap by reading the contract manifest — the string-only record a facet
/// assembly leaves of its (facet, source) pairs — and reporting the pairs with no mapper.
/// </para>
/// <para>
/// It only speaks about assemblies this compilation already maps <em>something</em> from. A
/// project that uses Facet the ordinary way, with mappings on the facet types themselves, has no
/// registrations at all and hears nothing; nor does a project that happens to reference a contract
/// assembly it has no mappers for. The diagnostic therefore means what it says — a gap in a set of
/// mappers that is otherwise being maintained — rather than "you could write more mappers".
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FacetMapperCoverageAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor CoverageRule = new DiagnosticDescriptor(
        "FAC115",
        "Facet has no mapper in an assembly that maps its contract assembly",
        "No mapper is generated for '{0}' (shaped from '{1}'). This assembly maps other facets from '{2}', so this one was probably missed: add [assembly: FacetMapper(typeof({1}), typeof({0}))], or [assembly: FacetMappersForAssembly] to cover the assembly, or name it in that attribute's Exclude.",
        "Coverage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A facet in a contract assembly carries no mapping members of its own, so a missing [assembly: FacetMapper] registration is silent until something calls a method that was never generated. This reads the referenced assembly's contract manifest and reports the pairs left uncovered, but only for assemblies this compilation already maps other facets from — so it reports drift in a maintained set of mappers rather than merely the absence of mappers.",
        // Reported once per compilation, not per syntax node.
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(CoverageRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(Analyze);
    }

    private static void Analyze(CompilationAnalysisContext context)
    {
        var assemblyAttributes = context.Compilation.Assembly.GetAttributes();

        var covered = new HashSet<string>();
        var coveredAssemblies = new HashSet<string>();
        var wholeAssemblyRegistrations = new HashSet<string>();
        var excludedByAssembly = new Dictionary<string, HashSet<string>>();
        var anchors = new List<Location>();

        foreach (var attribute in assemblyAttributes)
        {
            var name = attribute.AttributeClass?.ToDisplayString();

            if (name == FacetConstants.FacetMapperAttributeFullName)
            {
                if (attribute.ConstructorArguments.Length < 2
                    || attribute.ConstructorArguments[1].Value is not INamedTypeSymbol facet)
                {
                    continue;
                }

                covered.Add(facet.ToDisplayString());
                coveredAssemblies.Add(facet.ContainingAssembly.Name);
                AddAnchor(anchors, attribute);
            }
            else if (name == FacetConstants.FacetMappersForAssemblyAttributeFullName)
            {
                if (attribute.ConstructorArguments.Length < 1
                    || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol marker)
                {
                    continue;
                }

                var assemblyName = marker.ContainingAssembly.Name;

                // Covers the assembly wholesale, so only what it excludes can be uncovered.
                wholeAssemblyRegistrations.Add(assemblyName);
                coveredAssemblies.Add(assemblyName);
                excludedByAssembly[assemblyName] = ReadExclusions(attribute);
                AddAnchor(anchors, attribute);
            }
        }

        if (coveredAssemblies.Count == 0)
            return;

        var anchor = anchors.Count > 0 ? anchors[0] : Location.None;

        foreach (var reference in context.Compilation.References)
        {
            if (context.Compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
                continue;

            if (!coveredAssemblies.Contains(assembly.Name))
                continue;

            var excluded = excludedByAssembly.TryGetValue(assembly.Name, out var set)
                ? set
                : new HashSet<string>();

            foreach (var (facetFullName, sourceFullName) in ContractManifest.Read(assembly))
            {
                if (covered.Contains(facetFullName))
                    continue;

                if (wholeAssemblyRegistrations.Contains(assembly.Name)
                    && !excluded.Contains(facetFullName)
                    && !excluded.Contains(SimpleName(facetFullName)))
                {
                    // FacetMappersForAssembly already covers it.
                    continue;
                }

                // An excluded facet was excluded deliberately.
                if (excluded.Contains(facetFullName) || excluded.Contains(SimpleName(facetFullName)))
                    continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    CoverageRule, anchor, facetFullName, sourceFullName, assembly.Name));
            }
        }
    }

    private static void AddAnchor(List<Location> anchors, AttributeData attribute)
    {
        var reference = attribute.ApplicationSyntaxReference;
        if (reference is not null)
            anchors.Add(Location.Create(reference.SyntaxTree, reference.Span));
    }

    private static HashSet<string> ReadExclusions(AttributeData attribute)
    {
        var excluded = new HashSet<string>();

        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key != "Exclude" || named.Value.Kind != TypedConstantKind.Array || named.Value.IsNull)
                continue;

            foreach (var value in named.Value.Values)
            {
                if (value.Value is string s && !string.IsNullOrWhiteSpace(s))
                    excluded.Add(s);
            }
        }

        return excluded;
    }

    private static string SimpleName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');

        return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
    }
}
