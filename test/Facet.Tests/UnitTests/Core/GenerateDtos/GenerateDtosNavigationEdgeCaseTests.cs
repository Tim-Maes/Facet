using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Adversarial coverage for the ExcludeNavigationProperties heuristic: interface-typed
/// navigations, dictionaries of entities, the IncludeProperties escape hatch, and the
/// documented wrapper-generic limitation.
/// </summary>
public class GenerateDtosNavigationEdgeCaseTests
{
    private static string GenerateUpdateDto(string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        var compilation = CSharpCompilation.Create("NavEdge",
            new[] { CSharpSyntaxTree.ParseText(source) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create(new GenerateDtosGenerator());
        var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _).GetRunResult();
        return result.GeneratedTrees.Select(t => t.ToString())
            .First(t => t.Contains("UpdateParentRequest") && !t.Contains("interface UpdateParentRequest"));
    }

    private const string Entities = """
        using Facet;
        using System;
        using System.Collections.Generic;
        namespace NavEdge;

        public class Child { public int Id { get; set; } }
        public interface IChild { int Id { get; } }
        public class OwnedPayload { public string Text { get; set; } = ""; }
        """;

    [Fact]
    public void InterfaceTypedNavigation_IsExcluded()
    {
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true)]
            public class Parent
            {
                public int Id { get; set; }
                public IChild? InterfaceNav { get; set; }
                public string? Name { get; set; }
            }
            """);

        dto.Should().NotContain("InterfaceNav", "same-assembly interface-typed properties are domain references");
        dto.Should().Contain("Name");
    }

    [Fact]
    public void DictionaryOfEntities_IsExcluded()
    {
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true)]
            public class Parent
            {
                public int Id { get; set; }
                public Dictionary<string, Child> DictNav { get; set; } = new();
                public Dictionary<string, int> Counts { get; set; } = new();
            }
            """);

        dto.Should().NotContain("DictNav", "a dictionary of entities is a navigation like a list of them");
        dto.Should().Contain("Counts", "dictionaries of primitives are data, not navigations");
    }

    [Fact]
    public void IncludeProperties_ForcesAggregateChildrenBackIn()
    {
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true,
                IncludeProperties = new[] { nameof(Parent.Children) })]
            public class Parent
            {
                public int Id { get; set; }
                public List<Child> Children { get; set; } = new();
                public Child? BackReference { get; set; }
            }
            """);

        dto.Should().Contain("Children", "IncludeProperties wins over the navigation heuristic");
        dto.Should().NotContain("BackReference", "everything not force-included still follows the heuristic");
    }

    [Fact]
    public void WrapperGenerics_AreKept_DocumentedLimitation()
    {
        // Lazy<T>/Task<T> are not collection shapes and are not EF navigation patterns;
        // the heuristic deliberately ignores them. Pinned here so a behavior change is a
        // conscious decision.
        var dto = GenerateUpdateDto(Entities + """
            [GenerateDtos(Types = DtoTypes.Update, ExcludeNavigationProperties = true)]
            public class Parent
            {
                public int Id { get; set; }
                public Lazy<Child>? LazyNav { get; set; }
            }
            """);

        dto.Should().Contain("LazyNav");
    }
}
