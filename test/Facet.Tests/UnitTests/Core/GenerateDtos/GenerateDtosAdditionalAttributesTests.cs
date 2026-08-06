using Facet.Tests.TestModels;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for the AdditionalAttributes property on [GenerateDtos].
/// Verifies that raw attribute strings are emitted verbatim on generated
/// DTO classes/records but not on interfaces.
/// </summary>
public class GenerateDtosAdditionalAttributesTests
{
    private static readonly Assembly TestAssembly = Assembly.GetAssembly(typeof(TestAdditionalAttributesEntity))!;

    [Fact]
    public void AdditionalAttributes_EmitsAttributeOnGeneratedClass()
    {
        var dto = TestAssembly.GetType("Facet.Tests.TestModels.TestAdditionalAttributesEntityResponse");

        dto.Should().NotBeNull();
        dto!.IsClass.Should().BeTrue();

        var attr = dto.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == "System.SerializableAttribute");

        attr.Should().NotBeNull("AdditionalAttributes should emit the Serializable attribute verbatim");
    }

    [Fact]
    public void AdditionalAttributes_EmitsMultipleAttributesOnGeneratedRecord()
    {
        var dto = TestAssembly.GetType("Facet.Tests.TestModels.TestAdditionalAttributesMultiEntityResponse");

        dto.Should().NotBeNull();
        dto!.IsClass.Should().BeTrue("records are reference types");

        var attrs = dto.GetCustomAttributesData();
        attrs.Should().Contain(a => a.AttributeType.FullName == "System.SerializableAttribute",
            "the Serializable attribute should be emitted verbatim");
        attrs.Should().Contain(a => a.AttributeType.FullName == "System.ComponentModel.DefaultPropertyAttribute",
            "the DefaultProperty attribute should be emitted verbatim");
    }

    [Fact]
    public void AdditionalAttributes_DoesNotEmitOnInterfaceOutput()
    {
        var iface = TestAssembly.GetType("Facet.Tests.TestModels.ITestAdditionalAttributesInterfaceEntityResponse");
        var cls = TestAssembly.GetType("Facet.Tests.TestModels.TestAdditionalAttributesInterfaceEntityResponse");

        iface.Should().NotBeNull("Interface | Class should produce both outputs");
        cls.Should().NotBeNull();

        // Interface should NOT have the Serializable attribute
        var ifaceAttr = iface!.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == "System.SerializableAttribute");
        ifaceAttr.Should().BeNull("AdditionalAttributes should not be emitted on interface outputs");

        // Class SHOULD have the Serializable attribute
        var classAttr = cls!.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == "System.SerializableAttribute");
        classAttr.Should().NotBeNull("AdditionalAttributes should be emitted on class outputs even when paired with an interface");
    }

    [Fact]
    public void AdditionalAttributes_EmptyArray_ProducesNoExtraAttributes()
    {
        var dto = TestAssembly.GetType("Facet.Tests.TestModels.TestAdditionalAttributesEmptyEntityResponse");

        dto.Should().NotBeNull();
        dto!.IsClass.Should().BeTrue();

        // Should still have the [Facet] attribute but no Serializable attribute
        var attr = dto.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == "System.SerializableAttribute");
        attr.Should().BeNull("empty AdditionalAttributes should not emit any extra attributes");
    }

    [Fact]
    public void AdditionalAttributes_PreservesFacetAttribute()
    {
        var dto = TestAssembly.GetType("Facet.Tests.TestModels.TestAdditionalAttributesEntityResponse");

        dto.Should().NotBeNull();

        var facetAttr = dto!.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName?.StartsWith("Facet.FacetAttribute") == true
                              || a.AttributeType.Name == "FacetAttribute");
        facetAttr.Should().NotBeNull("the [Facet] attribute should still be emitted alongside AdditionalAttributes");
    }
}
