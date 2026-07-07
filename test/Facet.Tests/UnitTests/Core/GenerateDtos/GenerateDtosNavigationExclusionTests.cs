using Facet.Tests.TestModels;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for the <c>ExcludeNavigationProperties</c> option on <c>[GenerateDtos]</c>:
/// same-assembly class-typed properties (single and collection) are dropped, while
/// scalars, framework types, primitive collections, and user-defined value types
/// (e.g. strongly-typed ID structs) are kept.
/// </summary>
public class GenerateDtosNavigationExclusionTests
{
    private static readonly Assembly TestAssembly = Assembly.GetAssembly(typeof(TestNavigationExclusionEntity))!;

    [Fact]
    public void ExcludeNavigationProperties_DropsSingleAndCollectionNavigations()
    {
        var updateType = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestNavigationExclusionEntityRequest");

        updateType.Should().NotBeNull();
        updateType!.GetProperty("Parent").Should().BeNull("same-assembly class properties are navigations");
        updateType.GetProperty("Children").Should().BeNull("List<Entity> properties are navigations");
        updateType.GetProperty("Related").Should().BeNull("ICollection<Entity> properties are navigations");
        updateType.GetProperty("TargetArray").Should().BeNull("Entity[] properties are navigations");
    }

    [Fact]
    public void ExcludeNavigationProperties_KeepsScalarsValueTypesAndPrimitiveCollections()
    {
        var updateType = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestNavigationExclusionEntityRequest");

        updateType.Should().NotBeNull();
        updateType!.GetProperty("Id").Should().NotBeNull();
        updateType.GetProperty("Name").Should().NotBeNull("string is never a navigation");
        updateType.GetProperty("TypedId").Should().NotBeNull("user-defined value types (strongly-typed IDs) are kept");
        updateType.GetProperty("OptionalTypedId").Should().NotBeNull("nullable user-defined value types are kept");
        updateType.GetProperty("Aliases").Should().NotBeNull("collections of primitives are kept");
        updateType.GetProperty("Payload").Should().NotBeNull("byte arrays are kept");
        updateType.GetProperty("Timestamp").Should().NotBeNull("framework value types are kept");
    }

    [Fact]
    public void ExcludeNavigationProperties_AppliesToCreateDtoToo()
    {
        var createType = TestAssembly.GetType("Facet.Tests.TestModels.CreateTestNavigationExclusionEntityRequest");

        createType.Should().NotBeNull();
        createType!.GetProperty("Parent").Should().BeNull();
        createType.GetProperty("Children").Should().BeNull();
        createType.GetProperty("Name").Should().NotBeNull();
    }

    [Fact]
    public void ExcludeNavigationProperties_ComposesWithFlagsCombinedOutputType()
    {
        var updateInterface = TestAssembly.GetType("Facet.Tests.TestModels.IUpdateTrimTestNavigationFlagsComboEntityRequest");
        var updateClass = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTrimTestNavigationFlagsComboEntityRequest");

        updateInterface.Should().NotBeNull("Interface | PartialClass should emit the Interface output");
        updateClass.Should().NotBeNull("Interface | PartialClass should emit the PartialClass output");
        updateInterface!.IsAssignableFrom(updateClass!).Should().BeTrue(
            "the pair from one attribute should stay linked when navigation exclusion is active");

        updateClass!.GetProperty("Parent").Should().BeNull("navigations are dropped from the partial class");
        updateClass.GetProperty("Children").Should().BeNull();
        updateInterface.GetProperty("Parent").Should().BeNull("navigations are dropped from the interface");
        updateClass.GetProperty("Name").Should().NotBeNull();
    }
}
