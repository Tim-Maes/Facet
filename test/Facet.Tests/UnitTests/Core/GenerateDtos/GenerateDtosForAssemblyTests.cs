using Facet.Tests.ExternalLib;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for assembly-level generation via <c>[assembly: GenerateDtosFor(typeof(...), ...)]</c>
/// (declared in TestModels/AssemblyLevelDtoRegistrations.cs): DTOs compile into the
/// DECLARING assembly, sourced from entities that may live in referenced assemblies.
/// </summary>
public class GenerateDtosForAssemblyTests
{
    private static readonly Assembly TestAssembly = typeof(GenerateDtosForAssemblyTests).Assembly;

    [Fact]
    public void CrossAssemblyEntity_GeneratesDtosInDeclaringAssembly()
    {
        // The entity lives in Facet.Tests.ExternalLib and carries no attribute at all;
        // the DTOs must exist HERE, in the assembly declaring GenerateDtosFor.
        typeof(ExternalDomainEntity).Assembly.Should().NotBeSameAs(TestAssembly,
            "the point of the test is cross-assembly generation");

        var createRecord = TestAssembly.GetType("Facet.Tests.AssemblyGenerated.CreateExternalDomainEntityRequest");
        var updateRecord = TestAssembly.GetType("Facet.Tests.AssemblyGenerated.UpdateExternalDomainEntityRequest");
        var createInterface = TestAssembly.GetType("Facet.Tests.AssemblyGenerated.ICreateExternalDomainEntityRequest");

        createRecord.Should().NotBeNull("Create DTO should be generated into the declaring assembly");
        updateRecord.Should().NotBeNull();
        createInterface.Should().NotBeNull();
        createRecord!.GetMethod("<Clone>$").Should().NotBeNull("OutputType.Record should emit a record");
        createInterface!.IsAssignableFrom(createRecord).Should().BeTrue(
            "the record should implement its sibling interface");

        createRecord.GetProperty("Id").Should().BeNull("Create DTOs exclude Id");
        updateRecord!.GetProperty("Id").Should().NotBeNull();
        updateRecord.GetProperty("Name").Should().NotBeNull();
    }

    [Fact]
    public void CrossAssemblyDto_SourceCopyConstructorAndToSource_Work()
    {
        var updateRecord = TestAssembly.GetType("Facet.Tests.AssemblyGenerated.UpdateExternalDomainEntityRequest")!;

        var entity = new ExternalDomainEntity { Id = 7, Name = "ext", Description = "d", IsActive = true };
        var dto = Activator.CreateInstance(updateRecord, entity)!;

        updateRecord.GetProperty("Name")!.GetValue(dto).Should().Be("ext");

        var back = (ExternalDomainEntity)updateRecord.GetMethod("ToSource")!.Invoke(dto, null)!;
        back.Name.Should().Be("ext");
        back.IsActive.Should().BeTrue();
    }

    [Fact]
    public void MultipleEntitiesInOneAssembly_PairOnlyWithTheirOwnInterface()
    {
        var firstInterface = TestAssembly.GetType("Facet.Tests.AssemblyGenerated.IUpdateTestAssemblyPairFirstEntityRequest");
        var firstConcrete = TestAssembly.GetType("Facet.Tests.AssemblyGenerated.UpdateTestAssemblyPairFirstEntityRequest");
        var secondInterface = TestAssembly.GetType("Facet.Tests.AssemblyGenerated.IUpdateTestAssemblyPairSecondEntityRequest");
        var secondConcrete = TestAssembly.GetType("Facet.Tests.AssemblyGenerated.UpdateTestAssemblyPairSecondEntityRequest");

        firstInterface.Should().NotBeNull();
        firstConcrete.Should().NotBeNull();
        secondInterface.Should().NotBeNull();
        secondConcrete.Should().NotBeNull();

        firstInterface!.IsAssignableFrom(firstConcrete!).Should().BeTrue();
        secondInterface!.IsAssignableFrom(secondConcrete!).Should().BeTrue();

        firstInterface.IsAssignableFrom(secondConcrete).Should().BeFalse(
            "pairing must never cross-link outputs of different source entities");
        secondInterface.IsAssignableFrom(firstConcrete).Should().BeFalse();
    }
}
