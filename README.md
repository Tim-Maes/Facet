# Facet

> "One part of a subject, situation, object that has many parts."


**Facet** is a C# source generator that lets you define **lightweight projections** (DTOs, API models, etc.) directly from your domain models — without writing boilerplate.

It generates partial classes, records, structs, or record structs with constructors, optional LINQ projections, and even supports custom mappings — all at compile time, with zero runtime cost.

---

## What is Facetting?

Facetting is the process of defining **focused views** of a larger model at compile time.

Instead of manually writing separate DTOs, mappers, and projections, **Facet** allows you to declare what you want to keep — and generates everything else.

You can think of it like **carving out a specific facet** of a gem:

- The part you care about
- Leaving the rest behind.

## Why Facetting?

- Reduce duplication across DTOs, projections, and ViewModels
- Maintain strong typing with no runtime cost
- Stay DRY (Don't Repeat Yourself) without sacrificing performance
- Works seamlessly with LINQ providers like Entity Framework

## Key Features

- :white_check_mark: Generate classes, records, structs, or record structs from existing types
- :white_check_mark: Exclude fields/properties you don't want (create a Facetted view of your model)
- :white_check_mark: Include/redact public fields
- :white_check_mark: Auto-generate constructors for fast mapping
- :white_check_mark: LINQ projection expressions `(Expression<Func<TSource,TTarget>>)`
- :white_check_mark: Custom mapping via `IFacetMapConfiguration`
- :white_check_mark: Provider-agnostic extension methods for mapping and projecting

## Documentation

- **[Documentation & Guides](docs/README.md)**
- [What is being generated?](docs/07_WhatIsBeingGenerated.md)

## The Facet Ecosystem

Facet is modular and consists of the following NuGet packages:

- **Facet**: The core source generator with built-in extension methods and mapping configuration support. Generates DTOs, projections, and mapping code with provider-agnostic extension methods.

- **Facet.Extensions.EFCore**: Async extension methods for Entity Framework Core (requires EF Core 6+).

---

**Facet** — Define less, project more.
