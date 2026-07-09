using Facet;
using Facet.Tests.ExternalLib;
using Facet.Tests.TestModels;

// Assembly-level DTO generation: the DTOs compile into THIS assembly (Facet.Tests),
// while the source entities live wherever they naturally belong — including a
// different, referenced assembly (ExternalDomainEntity in Facet.Tests.ExternalLib).

// True cross-assembly generation: entity in ExternalLib, DTOs generated here.
[assembly: GenerateDtosFor(typeof(ExternalDomainEntity),
    Types = DtoTypes.Create | DtoTypes.Update,
    OutputType = OutputType.Interface | OutputType.Record,
    Namespace = "Facet.Tests.AssemblyGenerated")]

// Two GenerateDtosFor attributes for DIFFERENT entities in one assembly: sibling
// interface pairing must link each concrete output to its OWN entity's interface,
// never across entities.
[assembly: GenerateDtosFor(typeof(TestAssemblyPairFirstEntity),
    Types = DtoTypes.Update,
    OutputType = OutputType.Interface | OutputType.PartialClass,
    Namespace = "Facet.Tests.AssemblyGenerated")]
[assembly: GenerateDtosFor(typeof(TestAssemblyPairSecondEntity),
    Types = DtoTypes.Update,
    OutputType = OutputType.Interface | OutputType.Record,
    Namespace = "Facet.Tests.AssemblyGenerated")]

namespace Facet.Tests.TestModels
{
    public class TestAssemblyPairFirstEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class TestAssemblyPairSecondEntity
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }
}
