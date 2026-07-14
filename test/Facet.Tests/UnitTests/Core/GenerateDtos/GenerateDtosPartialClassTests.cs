using Facet.Tests.TestModels;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for <see cref="OutputType.PartialClass"/> output kind on <c>[GenerateDtos]</c>.
/// </summary>
public class GenerateDtosPartialClassTests
{
    private static readonly Assembly TestAssembly = Assembly.GetAssembly(typeof(TestPartialClassEntity))!;

    [Fact]
    public void PartialClass_CreateDto_IsEmittedAsClass()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.CreateTestPartialClassEntityRequest");

        type.Should().NotBeNull("PartialClass-output Create DTO should be emitted");
        type!.IsClass.Should().BeTrue("the generated type should be a class");
        type.IsInterface.Should().BeFalse();
    }

    [Fact]
    public void PartialClass_UpdateDto_IsEmittedAsClass()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestPartialClassEntityRequest");

        type.Should().NotBeNull("PartialClass-output Update DTO should be emitted");
        type!.IsClass.Should().BeTrue();
    }

    [Fact]
    public void PartialClass_ResponseDto_IsEmittedAsClass()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.TestPartialClassEntityResponse");

        type.Should().NotBeNull("PartialClass-output Response DTO should be emitted");
        type!.IsClass.Should().BeTrue();
    }

    [Fact]
    public void PartialClass_CreateDto_ExcludesId()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.CreateTestPartialClassEntityRequest");
        type.Should().NotBeNull();
        type!.GetProperty("Id").Should().BeNull("Create DTOs should exclude Id by default");
    }

    [Fact]
    public void PartialClass_UpdateDto_IncludesId()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestPartialClassEntityRequest");
        type.Should().NotBeNull();
        type!.GetProperty("Id").Should().NotBeNull("Update DTOs should include Id for identification");
    }

    [Fact]
    public void PartialClass_PropertiesAreGetSet()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestPartialClassEntityRequest");
        type.Should().NotBeNull();

        // Note: DisplayLabel comes from a hand-written partial, so the generated interface should stay get-only.
        var generatedProps = new[] { "Id", "Name", "Description", "IsActive" };
        foreach (var name in generatedProps)
        {
            var prop = type!.GetProperty(name);
            prop.Should().NotBeNull($"{name} should be present on the generated DTO");
            prop!.GetGetMethod().Should().NotBeNull($"{name} should have a getter");
            prop.GetSetMethod().Should().NotBeNull($"{name} should have a public setter (PartialClass mirrors Class)");
        }
    }

    [Fact]
    public void PartialClass_IsNotSealed()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestPartialClassEntityRequest");
        type.Should().NotBeNull();
        type!.IsSealed.Should().BeFalse("PartialClass output must not seal so callers can derive from it");
    }

    [Fact]
    public void PartialClass_HasAllArgsAndParameterlessConstructors()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestPartialClassEntityRequest");
        type.Should().NotBeNull();

        type!.GetConstructor(Type.EmptyTypes).Should().NotBeNull("PartialClass should emit a parameterless constructor");

        var sourceCtor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(TestPartialClassEntity) }, null);
        sourceCtor.Should().NotBeNull("PartialClass should emit a constructor taking the source type");
    }

    [Fact]
    public void PartialClass_DoesNotEmitProjectionOrToSource()
    {
        var type = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestPartialClassEntityRequest");
        type.Should().NotBeNull();

        // Note: PartialClass leaves projection and source-mapping members to user code.
        type!.GetMember("Projection").Should().BeEmpty();
        type.GetMember("FromSource").Should().BeEmpty();
        type.GetMember("ToSource").Should().BeEmpty();
        type.GetMember("BackTo").Should().BeEmpty();
    }

    [Fact]
    public void PartialClass_CanBeExtendedWithHandWrittenPartial()
    {
        var dto = new UpdateTestPartialClassEntityRequest { Id = 7, Name = "Widget" };
        dto.DisplayLabel.Should().Be("7: Widget");
    }

    [Fact]
    public void PartialClass_CanBeDerivedFrom()
    {
        var derived = new DerivedFromGeneratedPartial { Id = 1, Name = "x", DerivedOnly = "y" };
        derived.Should().BeAssignableTo<UpdateTestPartialClassEntityRequest>();
    }

    [Fact]
    public void PartialClass_ConstructorCopiesFromSource()
    {
        var source = new TestPartialClassEntity
        {
            Id = 42,
            Name = "From Source",
            Description = "Desc",
            IsActive = true
        };

        var dto = new UpdateTestPartialClassEntityRequest(source);

        dto.Id.Should().Be(42);
        dto.Name.Should().Be("From Source");
        dto.Description.Should().Be("Desc");
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public void PartialClass_WithSiblingInterface_ImplementsGeneratedInterface()
    {
        var partialType = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestPartialAndInterfaceEntityRequest");
        var interfaceType = TestAssembly.GetType("Facet.Tests.TestModels.IUpdateTestPartialAndInterfaceEntityRequest");

        partialType.Should().NotBeNull();
        interfaceType.Should().NotBeNull();
        interfaceType!.IsInterface.Should().BeTrue();

        partialType!.GetInterfaces().Should().Contain(interfaceType, "PartialClass should implement the sibling generated interface");
    }

    [Fact]
    public void PartialClass_WithoutSiblingInterface_DoesNotImplementUnrelatedInterface()
    {
        var partialType = TestAssembly.GetType("Facet.Tests.TestModels.CreateTestPartialClassEntityRequest");
        var stranglerInterface = TestAssembly.GetType("Facet.Tests.TestModels.ICreateTestPartialClassEntityRequest");

        partialType.Should().NotBeNull();
        stranglerInterface.Should().BeNull("Interface output was not requested, so no I-prefixed type should be generated");

        partialType!.GetInterfaces().Where(i => i.Namespace == "Facet.Tests.TestModels").Should().BeEmpty();
    }
}
