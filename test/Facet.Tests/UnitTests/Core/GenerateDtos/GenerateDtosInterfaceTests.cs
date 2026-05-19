using Facet.Tests.TestModels;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for <see cref="OutputType.Interface"/> output kind on <c>[GenerateDtos]</c>.
/// </summary>
public class GenerateDtosInterfaceTests
{
    private static readonly Assembly TestAssembly = Assembly.GetAssembly(typeof(TestInterfaceEntity))!;

    [Fact]
    public void Interface_CreateDto_IsEmittedAsInterface_WithIPrefix()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.ICreateTestInterfaceEntityRequest");

        type.Should().NotBeNull("Interface-output Create DTO should be emitted with an I prefix");
        type!.IsInterface.Should().BeTrue("the generated type should be an interface, not a class/record/struct");
    }

    [Fact]
    public void Interface_UpdateDto_IsEmittedAsInterface_WithIPrefix()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.IUpdateTestInterfaceEntityRequest");

        type.Should().NotBeNull("Interface-output Update DTO should be emitted with an I prefix");
        type!.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void Interface_ResponseDto_IsEmittedAsInterface_WithIPrefix()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.ITestInterfaceEntityResponse");

        type.Should().NotBeNull("Interface-output Response DTO should be emitted with an I prefix");
        type!.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void Interface_PropertiesAreGetOnly()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.IUpdateTestInterfaceEntityRequest");
        type.Should().NotBeNull();

        foreach (var prop in type!.GetProperties())
        {
            prop.GetGetMethod().Should().NotBeNull($"{prop.Name} should have a getter");
            prop.GetSetMethod().Should().BeNull($"{prop.Name} should NOT have a setter on the generated interface");
        }
    }

    [Fact]
    public void Interface_CreateDto_ExcludesId()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.ICreateTestInterfaceEntityRequest");
        type.Should().NotBeNull();
        type!.GetProperty("Id").Should().BeNull("Create DTOs (interface form too) should exclude Id by default");
    }

    [Fact]
    public void Interface_UpdateDto_IncludesId()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.IUpdateTestInterfaceEntityRequest");
        type.Should().NotBeNull();
        type!.GetProperty("Id").Should().NotBeNull("Update DTOs should include Id for identification");
    }

    [Fact]
    public void Interface_HasNoConstructorsProjectionOrToSource()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.IUpdateTestInterfaceEntityRequest");
        type.Should().NotBeNull();

        // Interfaces cannot declare constructors at all; getting any constructor info would be a defect.
        type!.GetConstructors().Should().BeEmpty();
        // The generator should not emit Projection / FromSource / ToSource on interface output.
        type.GetMember("Projection").Should().BeEmpty();
        type.GetMember("FromSource").Should().BeEmpty();
        type.GetMember("ToSource").Should().BeEmpty();
    }

    /// <summary>
    /// Compile-time check that a hand-written DTO can declare itself as implementing the
    /// generated interface. This is the primary use case: the interface acts as a "this DTO
    /// must cover all entity properties" contract that breaks compilation when the entity grows.
    /// </summary>
    private sealed class ConcreteImpl : IUpdateTestInterfaceEntityRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public bool IsActive { get; init; }
    }

    [Fact]
    public void Interface_CanBeImplementedByHandWrittenDto()
    {
        IUpdateTestInterfaceEntityRequest dto = new ConcreteImpl { Id = 1, Name = "n", IsActive = true };
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("n");
        dto.Description.Should().BeNull();
        dto.IsActive.Should().BeTrue();
    }
}
