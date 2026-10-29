using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Facet.Tests.ExternalLib;

namespace Facet.Tests.UnitTests.Features;

/// <summary>
/// Guards the claim the whole layered-assembly feature rests on: that a contract assembly compiles
/// without a reference to the layer its facets were shaped from.
/// </summary>
/// <remarks>
/// Asserted against the emitted metadata rather than the compilation, because that is where the
/// claim actually lives — the compiler drops an assembly reference nothing uses, and the question
/// is whether anything still uses it. Facet.Tests.ExternalLib holds both the sources and the
/// contracts, so what is checked here is the narrower, still-decisive property: no generated
/// mapping member names a source type, and no Facet attribute survives into metadata.
/// </remarks>
public class ContractAssemblyReferenceTests
{
    [Fact]
    public void ContractFacets_HaveNoMappingMembers()
    {
        // Facet_GenerateMappings=false, so the constructor, FromSource and Projection that would
        // each name the source type are never emitted. This is what lets the mapping live
        // downstream — and what a contract assembly needs in order to reference nothing.
        var contract = typeof(ExternalOrderContract);

        contract.GetConstructors()
            .Should().OnlyContain(c => c.GetParameters().Length == 0,
                "a constructor taking the source type would name it");

        contract.GetProperty("Projection", BindingFlags.Public | BindingFlags.Static)
            .Should().BeNull();

        contract.GetMethod("FromSource", BindingFlags.Public | BindingFlags.Static)
            .Should().BeNull();
    }

    [Fact]
    public void ContractAssembly_DoesNotReferenceTheGeneratorsAttributePackage()
    {
        // The [Facet] attributes are [Conditional], and this project does not define the symbol
        // that keeps them, so nothing in its metadata refers to Facet.Attributes — the reference
        // the compiler would otherwise have had to keep for the attribute blob.
        var references = ReadAssemblyReferences(typeof(ExternalOrderContract).Assembly.Location);

        references.Should().NotContain("Facet.Attributes");
    }

    [Fact]
    public void ContractFacets_CarryNoFacetAttribute()
    {
        typeof(ExternalOrderContract).GetCustomAttributes(inherit: false)
            .Should().NotContain(a => a.GetType().FullName == "Facet.FacetAttribute");
    }

    private static List<string> ReadAssemblyReferences(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();

        return metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .ToList();
    }
}
