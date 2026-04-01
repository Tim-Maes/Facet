using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for DDD-style classes with private constructors and non-public setters (issue #302).
/// Nested facets should have full access to the containing type's private members.
/// </summary>
public class DDDNestedFacetTests
{
    [Fact]
    public void NestedRecordFacet_ShouldMapAllProperties()
    {
        var source = DDDSample.Create("pub", "priv", "intern");
        var dto = source.ToFacet<DDDSample, DDDSample.InsideFacetRecord>();

        dto.AProperty.Should().Be("pub");
        dto.APrivateSetterProperty.Should().Be("priv");
        dto.AInternalSetterProperty.Should().Be("intern");
    }

    [Fact]
    public void NestedClassFacet_ShouldMapAllProperties()
    {
        var source = DDDSample.Create("pub", "priv", "intern");
        var dto = source.ToFacet<DDDSample, DDDSample.InsideFacetClass>();

        dto.AProperty.Should().Be("pub");
        dto.APrivateSetterProperty.Should().Be("priv");
        dto.AInternalSetterProperty.Should().Be("intern");
    }

    [Fact]
    public void NestedRecordFacet_ShouldGenerateToSource()
    {
        // ToSource should be generated because the nested facet can access the private constructor
        var source = DDDSample.Create("pub", "priv", "intern");
        var dto = source.ToFacet<DDDSample, DDDSample.InsideFacetRecord>();

        var hasToSource = typeof(DDDSample.InsideFacetRecord).GetMethod("ToSource");
        hasToSource.Should().NotBeNull("nested facets should have ToSource generated");

        var roundTripped = dto.ToSource();
        roundTripped.AProperty.Should().Be("pub");
        roundTripped.APrivateSetterProperty.Should().Be("priv");
        roundTripped.AInternalSetterProperty.Should().Be("intern");
    }

    [Fact]
    public void NestedClassFacet_ShouldGenerateToSource()
    {
        var source = DDDSample.Create("pub", "priv", "intern");
        var dto = source.ToFacet<DDDSample, DDDSample.InsideFacetClass>();

        var hasToSource = typeof(DDDSample.InsideFacetClass).GetMethod("ToSource");
        hasToSource.Should().NotBeNull("nested facets should have ToSource generated");

        var roundTripped = dto.ToSource();
        roundTripped.AProperty.Should().Be("pub");
        roundTripped.APrivateSetterProperty.Should().Be("priv");
        roundTripped.AInternalSetterProperty.Should().Be("intern");
    }

    [Fact]
    public void OutsideFacet_ShouldMapAllProperties()
    {
        // Outside facets can still read all public properties
        var source = DDDSample.Create("pub", "priv", "intern");
        var dto = source.ToFacet<DDDSample, OutsideFacetRecord>();

        dto.AProperty.Should().Be("pub");
        dto.APrivateSetterProperty.Should().Be("priv");
        dto.AInternalSetterProperty.Should().Be("intern");
    }

    [Fact]
    public void OutsideFacet_ShouldNotHaveToSource()
    {
        // Outside facets cannot access the private constructor, so ToSource is not generated
        var hasToSource = typeof(OutsideFacetRecord).GetMethod("ToSource");
        hasToSource.Should().BeNull("outside facets cannot access private constructor for ToSource");
    }

    [Fact]
    public void OutsideFacet_WithInternalCtor_ShouldMapAllProperties()
    {
        // Outside facets in the same assembly can read all public properties
        var source = DDDSampleInternal.Create("pub", "priv", "intern");
        var dto = source.ToFacet<DDDSampleInternal, OutsideFacetInternalCtorClass>();

        dto.AProperty.Should().Be("pub");
        dto.APrivateSetterProperty.Should().Be("priv");
        dto.AInternalSetterProperty.Should().Be("intern");
    }

    [Fact]
    public void OutsideFacet_WithInternalCtor_ExcludingPrivateSetter_ShouldGenerateToSource()
    {
        // Internal constructor is accessible from same assembly.
        // Excluding APrivateSetterProperty (private setter) allows ToSource to work
        // because the remaining properties have public or internal setters.
        var source = DDDSampleInternal.Create("pub", "priv", "intern");
        var dto = source.ToFacet<DDDSampleInternal, OutsideFacetInternalCtorWithToSource>();

        var hasToSource = typeof(OutsideFacetInternalCtorWithToSource).GetMethod("ToSource");
        hasToSource.Should().NotBeNull("internal ctor + public/internal setters should allow ToSource");

        var roundTripped = dto.ToSource();
        roundTripped.AProperty.Should().Be("pub");
        roundTripped.AInternalSetterProperty.Should().Be("intern");
    }
}
