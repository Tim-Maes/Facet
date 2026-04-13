using System.Collections.Immutable;

namespace Facet.Tests.TestModels.NestedTypeInCollection;

// Bug: when a property uses ImmutableList<FooBar.BarFoo> where BarFoo is nested inside FooBar,
// the generator incorrectly emits "using static System.Collections.Immutable" instead of
// "using static <Namespace>.FooBar".

public record Foo
{
    public required ImmutableList<FooBar.BarFoo> J { get; init; } = [];
}

public sealed record Bar : Foo
{
    public string? K { get; init; }
}

public sealed record FooBar
{
    public sealed record BarFoo
    {
        public string? L { get; init; }
    }

    public required Bar Bar { get; init; }
}

[Facet(typeof(Bar), exclude: nameof(Bar.K))]
public sealed partial record BarDto;

[Facet(typeof(FooBar), NestedFacets = [typeof(BarDto)])]
public sealed partial record FooBarDto;
