using Facet.Generators.MapperGenerators;
using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facet.Generators;

/// <summary>
/// Generates a free-standing static mapper class for each <c>[assembly: FacetMapper(...)]</c>
/// registration, so that mapping code can live in an assembly other than the one declaring the
/// facet type.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FacetGenerator"/> emits mappings as a partial half of the facet type. A partial type
/// cannot span assemblies, so those mappings are necessarily in the facet's own assembly, which
/// drags the source type's assembly in with them. This generator emits extension methods on a
/// static class instead — no partial, so no such constraint — and is driven from the assembly
/// that is allowed to see both layers.
/// </para>
/// <para>
/// The contract assembly pairs this with <c>Facet_GenerateMappings=false</c> (to drop the
/// mapping half of its own output) and <c>Facet_EmitAttributeMetadata=false</c> (to drop the
/// <c>typeof</c> in its <c>[Facet]</c> attributes), leaving it with no reference to the layer it
/// was shaped from.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class FacetMapperGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor UnmappedMemberRule = new DiagnosticDescriptor(
        "FAC110",
        "Facet mapper leaves a property unmapped",
        "The mapper from '{0}' to '{1}' leaves '{2}' unmapped: {3}. It keeps its default value.",
        "Generator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A mapper generated from [assembly: FacetMapper] matches members by name, because the facet type is read from metadata and its own [Facet]/[MapFrom] attributes need not be present there. A property the generator cannot fill is reported rather than silently left at its default, so that a rename on either side of the boundary is a build warning. Exclude the property in the attribute to acknowledge it deliberately.");

    private static readonly DiagnosticDescriptor InvalidRegistrationRule = new DiagnosticDescriptor(
        "FAC111",
        "Facet mapper registration is not usable",
        "This [assembly: FacetMapper] registration cannot be generated. Reason: {0}.",
        "Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The registration names types the generator cannot build a mapper between, so no mapper is generated for it and any call to the expected methods will not compile.");

    private static readonly DiagnosticDescriptor ToSourceNotPossibleRule = new DiagnosticDescriptor(
        "FAC112",
        "Facet mapper cannot generate a reverse mapping",
        "The mapper from '{0}' to '{1}' sets GenerateToSource, but '{0}' has no accessible parameterless constructor. The reverse mapping is omitted.",
        "Generator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The reverse mapping is emitted as an object initializer, which needs a parameterless constructor on the source type. Declare one, or drop GenerateToSource and map back by hand.");

    private static readonly DiagnosticDescriptor DuplicateMapperRule = new DiagnosticDescriptor(
        "FAC113",
        "Two Facet mappers would generate the same class",
        "More than one [assembly: FacetMapper] registration generates '{0}'. Only the first is emitted; set Name on the others to give them distinct class names.",
        "Generator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The generated class is named {FacetType}Mapper by default, so mapping one facet type from several source types collides. Naming each mapper explicitly resolves it.");

    private static readonly DiagnosticDescriptor FacetInSameCompilationRule = new DiagnosticDescriptor(
        "FAC114",
        "Facet mapper cannot see a facet declared in the same compilation",
        "The mapper from '{0}' to '{1}' is generated into the same assembly that declares '{1}'. Members that [Facet] generates onto '{1}' are not visible to this generator, so only its hand-written members ({2} mapped) are. Declare '{1}' in a referenced assembly, or drop the registration and use the mapping members [Facet] already generates onto it.",
        "Generator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Roslyn runs all generators against the same input compilation, so one generator never observes another's output. A facet type in the same compilation therefore appears to have only the members written by hand, and the generated mapper silently skips the rest. This generator is for facets read from metadata — which is also the only configuration in which it is useful, since a facet in this assembly can simply carry its own mappings.");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var registrations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                FacetConstants.FacetMapperAttributeFullName,
                predicate: static (node, _) => node is CompilationUnitSyntax,
                transform: static (ctx, token) => FacetMapperModelBuilder.BuildModels(ctx, token))
            .SelectMany(static (results, _) => results)
            .Collect();

        context.RegisterSourceOutput(registrations, static (spc, results) =>
        {
            spc.CancellationToken.ThrowIfCancellationRequested();

            foreach (var invalid in results.Where(r => r.Registration is null))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    InvalidRegistrationRule,
                    invalid.Location?.ToLocation() ?? Location.None,
                    invalid.Error));
            }

            var valid = results
                .Where(r => r.Registration is not null)
                .Select(r => r.Registration!)
                .ToList();

            // Nested members are inlined from the registration of the pair they need, so the code
            // builder needs every pair in the compilation, not just the one it is emitting.
            var lookup = new Dictionary<string, MapperRegistration>();
            foreach (var registration in valid)
            {
                if (!lookup.ContainsKey(registration.PairKey))
                    lookup.Add(registration.PairKey, registration);
            }

            var emitted = new HashSet<string>();

            foreach (var registration in valid)
            {
                spc.CancellationToken.ThrowIfCancellationRequested();

                var fullName = string.IsNullOrEmpty(registration.MapperNamespace)
                    ? registration.MapperName
                    : $"{registration.MapperNamespace}.{registration.MapperName}";

                if (!emitted.Add(fullName))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        DuplicateMapperRule,
                        registration.Location?.ToLocation() ?? Location.None,
                        fullName));
                    continue;
                }

                foreach (var unmapped in registration.UnmappedMembers)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnmappedMemberRule,
                        registration.Location?.ToLocation() ?? Location.None,
                        GeneratorUtilities.StripGlobalPrefix(registration.SourceTypeName),
                        GeneratorUtilities.StripGlobalPrefix(registration.FacetTypeName),
                        unmapped.Name,
                        unmapped.Reason));
                }

                if (registration.FacetIsInCurrentCompilation)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        FacetInSameCompilationRule,
                        registration.Location?.ToLocation() ?? Location.None,
                        GeneratorUtilities.StripGlobalPrefix(registration.SourceTypeName),
                        GeneratorUtilities.StripGlobalPrefix(registration.FacetTypeName),
                        registration.Members.Length));
                }

                if (registration.GenerateToSource && !registration.SourceHasParameterlessConstructor)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        ToSourceNotPossibleRule,
                        registration.Location?.ToLocation() ?? Location.None,
                        GeneratorUtilities.StripGlobalPrefix(registration.SourceTypeName),
                        GeneratorUtilities.StripGlobalPrefix(registration.FacetTypeName)));
                }

                var code = FacetMapperCodeBuilder.Generate(registration, lookup);
                spc.AddSource($"{fullName}.g.cs", SourceText.From(code, Encoding.UTF8));
            }
        });
    }
}
