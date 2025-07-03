# Facet.Extensions.EFCore

EF Core async extension methods for the Facet library, enabling one-line async mapping and projection between your domain entities and generated facet types.

## Key Features

- Async projection to `List<TTarget>`: `ToFacetsAsync<TSource,TTarget>()`
- Async projection to first or default: `FirstFacetAsync<TSource,TTarget>()`
- Async projection to single: `SingleFacetAsync<TSource,TTarget>()`

All methods leverage your already generated ctor or Projection property and require EF Core 6+.

## Getting Started

### 1. Install packages

```bash
dotnet add package Facet
dotnet add package Facet.Extensions.EFCore
```
### 2. Import namespaces

```csharp
using Facet.Extensions;        // for sync extension methods (built into Facet)
using Facet.Extensions.EFCore; // for async EF Core extension methods
```

### 3. Use async mapping in EF Core

```csharp
// Async projection to list
var dtos = await dbContext.People.ToFacetsAsync<Person, PersonDto>();

// Async projection to first or default
var firstDto = await dbContext.People.FirstFacetAsync<Person, PersonDto>();

// Async projection to single
var singleDto = await dbContext.People.SingleFacetAsync<Person, PersonDto>();
```

## Requirements

- Facet
- .NET 6.0+
- Microsoft.EntityFrameworkCore 6.0+

---

For provider-agnostic sync and LINQ methods, these are now built into the main [Facet](https://www.nuget.org/packages/Facet) package.
