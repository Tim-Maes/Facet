# Facet.Tests Project Structure

## Overview

The test project has been reorganized to mirror the structure of the main Facet solution, making it easier to find and maintain tests related to specific components.

## New Structure

```
test/Facet.Tests/
??? UnitTests/
?   ??? Core/                           # Tests for core Facet functionality
?   ?   ??? Facet/                      # FacetAttribute-related tests
?   ?   ?   ??? BasicMappingTests.cs
?   ?   ?   ??? IncludePropertyTests.cs
?   ?   ?   ??? CustomMappingTests.cs
?   ?   ?   ??? BackToTests.cs
?   ?   ?   ??? ModernRecordTests.cs
?   ?   ??? GenerateDtos/               # GenerateDtosAttribute-related tests
?   ?   ?   ??? GenerateDtosSimpleTest.cs
?   ?   ?   ??? GenerateDtosFacetIntegrationTests.cs
?   ?   ??? FacetKinds/                 # FacetKind enum and type generation tests
?   ?   ?   ??? FacetKindsTests.cs
?   ?   ??? Generators/                 # Code generator tests (future)
?   ??? Extensions/                     # Tests for extension libraries
?   ?   ??? Mapping/                    # Facet.Extensions and Facet.Mapping.Expressions tests
?   ?   ?   ??? ExpressionMappingTests.cs
?   ?   ?   ??? AsyncMappingTests.cs
?   ?   ??? EFCore/                     # Facet.Extensions.EFCore tests
?   ?       ??? LinqProjectionTests.cs
?   ??? Features/                       # Cross-cutting feature tests
?       ??? EnumHandlingTests.cs
?       ??? NullableHandlingTests.cs
?       ??? InheritanceTests.cs
?       ??? BackToRequiredFieldsTests.cs
??? TestModels/                         # Test entity and DTO models
?   ??? TestEntities.cs
?   ??? TestDtos.cs
?   ??? GenerateDtosTestEntities.cs
??? Utilities/                          # Test helper classes and factories
?   ??? TestDataFactory.cs
??? GlobalUsings.cs                     # Global using statements
??? Facet.Tests.csproj                 # Project file
```

## Test Categories

### Core Tests
- **Facet/**: Tests for the core `[Facet]` attribute functionality including basic mapping, property inclusion/exclusion, custom mapping configurations, and back-to-source mapping.
- **GenerateDtos/**: Tests for the `[GenerateDtos]` attribute that generates multiple DTO types from a single source.
- **FacetKinds/**: Tests for different facet types (Class, Record, Struct, RecordStruct) and their behaviors.
- **Generators/**: Reserved for future source generator-specific tests.

### Extensions Tests
- **Mapping/**: Tests for expression mapping, async mapping operations, and projection capabilities.
- **EFCore/**: Tests for Entity Framework Core integration, including LINQ projection and database query optimization.

### Features Tests
- **Features/**: Cross-cutting functionality tests including enum handling, nullable types, inheritance scenarios, and required field validation.

## Benefits of This Structure

1. **Mirror Source Structure**: The test structure now mirrors the main Facet solution structure, making it intuitive to find related tests.

2. **Logical Grouping**: Related tests are grouped together, making it easier to understand what functionality is being tested.

3. **Scalability**: The structure can easily accommodate new test categories as the project grows.

4. **Namespace Organization**: Each folder has its own namespace that avoids conflicts with actual Facet types.

5. **Clear Separation**: Core functionality, extensions, and cross-cutting features are clearly separated.

## Namespace Convention

- `Facet.Tests.UnitTests.Core.Facet` - Core FacetAttribute tests
- `Facet.Tests.UnitTests.Core.GenerateDtos` - GenerateDtosAttribute tests  
- `Facet.Tests.UnitTests.Core.FacetKinds` - FacetKind enum tests
- `Facet.Tests.UnitTests.Extensions.Mapping` - Mapping extension tests
- `Facet.Tests.UnitTests.Extensions.EFCore` - EF Core extension tests
- `Facet.Tests.UnitTests.Features` - Cross-cutting feature tests

This organization makes the test project much more maintainable and provides a clear structure for both current and future tests.