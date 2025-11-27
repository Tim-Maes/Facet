# Facet.Attributes

Runtime attributes for the [Facet](https://github.com/Siphonophora/Facet) source generator.

This package contains only the attribute classes and enums needed at runtime. For the source generator itself, install the `Facet` package.

## Installation

```bash
dotnet add package Facet
```

The `Facet` package automatically includes `Facet.Attributes` as a dependency.

You should **not** need to install `Facet.Attributes` directly unless you're building custom tooling.

## Attributes Included

- `[Facet]` - Generate facets/DTOs from source types
- `[Flatten]` - Generate flattened projections with nested properties as top-level properties
- `[MapFrom]` - Custom property mapping
- `[MapWhen]` - Conditional property mapping
- `[GenerateDtos]` - Batch DTO generation
- `[Wrapper]` - Generate wrapper types

## AOT Compatibility

This package is fully compatible with AOT (Ahead-Of-Time) compilation, including .NET MAUI applications. The separation of attributes from the source generator ensures that Roslyn dependencies do not leak into your runtime.
