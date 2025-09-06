---
date: 2025-09-06T06:53:09-05:00
researcher: Claude Code
git_commit: 734a2de4c3e5738df50fad6f51e40f3c8c0229f9
branch: feature/enhanced-dto-generation-and-ef-updates
repository: Facet
topic: "TestConsole to Unit Tests Migration Feature Specification"
tags: [feature, requirements, specification, testing, unit-tests, source-generator, verify]
status: complete
last_updated: 2025-09-06
last_updated_by: Claude Code
type: feature
---

# TestConsole to Unit Tests Migration Feature

## Overview
Convert the Facet.TestConsole project from a manual console application to a proper xUnit test project using Verify.SourceGenerators for source generator testing and TestDbContextFixture for Entity Framework integration tests. This provides automated testing, CI/CD integration, and GitHub annotation support.

## Business Value

### For Facet Development Team
- **Automated test execution**: Tests run automatically in CI/CD with proper pass/fail reporting and GitHub annotations
- **Regression prevention**: Source generator output changes are caught immediately via snapshot testing
- **Better debugging**: Individual test isolation makes it easier to identify and fix specific issues
- **Professional testing standards**: Aligns with industry best practices using proper test framework

### For Contributors
- **Clear test structure**: Easy to understand what each test validates using descriptive test names
- **IDE integration**: Full IntelliSense, debugging, and test runner support in development environment
- **Snapshot validation**: Verify.SourceGenerators automatically detects changes in generated code
- **Entity Framework validation**: Real database integration tests ensure projections work correctly

### Current Implementation
The current `test/Facet.TestConsole/` project is a console application that runs manual tests through static methods with console output. It contains comprehensive test scenarios but lacks proper assertions, test isolation, and CI/CD integration.

### Composition Pattern
**MANDATORY**: Follow established Facet testing patterns:
- Use **Verify.SourceGenerators** with `[ModuleInitializer]` for source generator testing
- Use **TestDbContextFixture** with xUnit for Entity Framework integration tests
- **Snapshot testing** for generated code verification using `Verify()` method
- **Simple, behavior-focused tests** that verify functionality not implementation details

### Data Model
Tests will validate the same entities and DTOs currently in TestConsole:
- `User`, `Employee`, `Manager` (inheritance chain testing)
- `Product`, `ModernUser` (modern record features)
- Various DTO types with different `FacetKind` values (class, record, struct, record struct)

## User Stories

### Test Developer
1. **Test Developer**: **Given** I need to validate source generator output, **when** I write a unit test, **then** I use Verify.SourceGenerators with snapshot testing to catch any changes in generated code automatically

2. **Test Developer**: **Given** I'm testing EF Core integration, **when** I run database tests, **then** I have proper TestDbContextFixture with transaction isolation and consistent seed data

### CI/CD Pipeline
1. **Pipeline**: **Given** tests run in GitHub Actions, **when** any source generator test fails, **then** the failure appears as a GitHub annotation showing exactly what generated code changed

2. **Pipeline**: **Given** tests execute with GitHub Actions logger, **when** Entity Framework tests fail, **then** specific query and assertion failures are clearly visible in PR annotations

### Feature Developer
1. **Feature Developer**: **Given** I add new Facet functionality, **when** I write tests following established patterns, **then** I have confidence the tests will catch regressions and integrate properly with existing test suite

## Core Functionality

### Source Generator Test Categories
- **Facet Generator Tests**: Basic DTO generation using Verify.SourceGenerators snapshot testing
- **GenerateDtos Tests**: Advanced DTO generation with attribute-based configuration
- **Inheritance Tests**: Complex inheritance chain validation (BaseEntity -> Person -> Employee -> Manager)
- **Modern Record Tests**: Init-only properties, required properties, record structs
- **Error Handling Tests**: Invalid entity scenarios with proper diagnostic verification

### Entity Framework Integration Tests
- **Projection Tests**: `ToFacetsAsync<T>()` and `FirstFacetAsync<T>()` with real database queries
- **Complex Query Tests**: Include, Where, OrderBy combinations with generated projections
- **UpdateFromFacet Tests**: Entity mutation workflows using generated DTOs
- **Performance Tests**: Verify projections execute efficiently without N+1 queries

## Requirements

### Functional Requirements
- **MANDATORY: Use Verify.SourceGenerators** for all source generator output testing with `[ModuleInitializer]`
- **MANDATORY: Use TestDbContextFixture** for all Entity Framework integration tests
- **Complete test coverage**: Convert all existing TestConsole test scenarios to proper unit tests
- **Snapshot testing**: Generated code changes must be explicitly approved via snapshot updates
- **Database test isolation**: Each test runs with clean database state via proper fixture management

### Non-Functional Requirements

#### Performance
- **Test execution**: Complete test suite runs in under 60 seconds for developer workflow
- **Database tests**: Use in-memory SQLite for fast, isolated database operations
- **Parallel execution**: Tests can run in parallel where appropriate without conflicts

#### Integration Requirements
- **GitHub Actions**: Tests integrate with existing build.yml using `--logger GitHubActions`
- **Package alignment**: Use same package versions as existing `test/Facet.Extensions.EFCore.Tests`
- **xUnit framework**: Follow established xUnit patterns with Fact/Theory attributes

#### Maintainability
- **Clear naming**: Test method names follow `MethodName_WithCondition_ExpectedResult` pattern
- **Organized structure**: Group tests by functionality using separate test classes
- **Snapshot management**: Generated code snapshots stored in organized directory structure

## Design Considerations

### Test Project Structure
```
test/Facet.UnitTests/  (new project)
├── Facet.UnitTests.csproj
├── GlobalInitializer.cs  (MANDATORY: [ModuleInitializer])
├── SourceGeneration/
│   ├── FacetGeneratorTests.cs
│   ├── GenerateDtosGeneratorTests.cs
│   ├── InheritanceGenerationTests.cs
│   ├── ModernRecordGenerationTests.cs
│   ├── ParameterlessConstructorTests.cs
│   └── ErrorHandlingTests.cs
├── EntityFramework/
│   ├── BasicProjectionTests.cs
│   ├── ComplexQueryTests.cs
│   ├── UpdateFromFacetTests.cs
│   └── NavigationPropertyTests.cs
├── Mapping/
│   ├── CustomMappingTests.cs
│   ├── FacetKindTests.cs
│   └── LinqExtensionTests.cs
├── TestFixtures/
│   ├── TestDbContextFixture.cs
│   ├── SampleDataBuilder.cs
│   └── TestEntityModels.cs
└── Snapshots/  (Verify.SourceGenerators output)
    ├── FacetGeneratorTests/
    ├── GenerateDtosGeneratorTests/
    └── [other test snapshots]
```

### MANDATORY: Global Initializer Pattern
```csharp
using System.Runtime.CompilerServices;

namespace Facet.UnitTests;

// MANDATORY: Initialize Verify.SourceGenerators globally
public static class GlobalInitializer
{
    [ModuleInitializer]
    public static void Init() => VerifySourceGenerators.Initialize();
}
```

### Source Generator Test Pattern (MANDATORY)
```csharp
[Fact]
public Task FacetGenerator_WithBasicEntity_GeneratesExpectedDto()
{
    // Arrange
    var source = @"
        using Facet;

        public class User
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
        }";

    var compilation = CSharpCompilation.Create(
        "TestAssembly",
        syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
        references: GetMetadataReferences());

    var generator = new FacetGenerator();

    // Act
    var driver = CSharpGeneratorDriver.Create(generator)
        .RunGenerators(compilation);

    // Assert - MANDATORY: Use Verify() for snapshot testing
    return Verify(driver)
        .UseDirectory("Snapshots")
        .ScrubLines(line => line.StartsWith("// <auto-generated"));
}
```

### Entity Framework Test Pattern
```csharp
public class BasicProjectionTests : IClassFixture<TestDbContextFixture>
{
    private readonly TestDbContextFixture _fixture;
    private readonly ITestOutputHelper _output;

    public BasicProjectionTests(TestDbContextFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task ToFacetsAsync_WithActiveUsers_ReturnsCorrectDtos()
    {
        // Act
        var results = await _fixture.Context.Users
            .Where(u => u.IsActive)
            .ToFacetsAsync<UserDto>();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, dto => {
            Assert.NotEqual(0, dto.Id);
            Assert.NotEmpty(dto.FirstName);
            Assert.True(dto.IsActive);
        });

        _output.WriteLine($"Retrieved {results.Count} active user DTOs");
    }
}
```

## Implementation Considerations

### Technical Architecture
- **New test project**: Create `test/Facet.UnitTests/Facet.UnitTests.csproj` following existing EFCore.Tests patterns
- **Package references**: Align with `test/Facet.Extensions.EFCore.Tests/Facet.Extensions.EFCore.Tests.csproj`
- **Database testing**: Use SQLite in-memory with proper transaction isolation
- **Snapshot storage**: Organize Verify.SourceGenerators snapshots by test class

### Migration Strategy
1. **Create test project structure** with proper global initializer
2. **Convert source generator tests** using Verify.SourceGenerators patterns first
3. **Migrate Entity Framework tests** using TestDbContextFixture patterns
4. **Validate test coverage** ensures all TestConsole scenarios preserved
5. **Update GitHub Actions** to run new test project in build.yml
6. **Deprecate TestConsole** after validation of complete migration

### Dependencies
- **Same packages as EFCore.Tests**: `Microsoft.NET.Test.Sdk`, `xunit`, `Verify.SourceGenerators`, `Verify.Xunit`
- **Entity Framework packages**: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.InMemory`
- **Project references**: Same Facet project references as current TestConsole

## Success Criteria

### Core Functionality
- **All TestConsole scenarios converted**: Every test case has equivalent xUnit test with proper assertions
- **Verify.SourceGenerators integration**: All source generator tests use snapshot validation
- **Entity Framework validation**: All EF projections tested with TestDbContextFixture
- **GitHub Actions integration**: Tests run automatically with native annotation support

### Technical Implementation
- **Test isolation**: Each test runs independently with proper setup/teardown
- **Snapshot management**: Generated code changes require explicit approval via snapshot updates
- **Performance**: Test suite completes within acceptable time for CI/CD pipeline
- **Maintainability**: Tests follow established patterns and are easy to understand/modify

### Coverage Validation
- **Feature parity**: New tests validate same functionality as original TestConsole tests
- **Edge case coverage**: Tests catch error conditions and invalid scenarios
- **Regression protection**: Changes to source generators immediately detected via snapshot testing

## Scope Boundaries

### Definitely In Scope
- **Complete TestConsole test migration** using Verify.SourceGenerators and TestDbContextFixture
- **Proper xUnit test project structure** with organized test classes and fixtures
- **GitHub Actions integration** with existing build.yml workflow
- **Snapshot-based source generator testing** following established Facet patterns

### Definitely Out of Scope
- **Changing test scenarios**: Preserve existing test coverage, only improve structure and automation
- **Performance benchmarking**: Focus on correctness testing, not performance measurement
- **New test scenarios**: Don't add new functionality, only convert existing manual tests

### Future Considerations
- **Code coverage reporting**: Add detailed coverage analysis and reporting
- **Additional test categories**: Performance tests, integration tests with external dependencies
- **Test result visualization**: Enhanced reporting and analysis tools

## Open Questions & Risks

### Questions Needing Resolution
- **TestConsole deprecation timeline**: When should the console project be completely removed?
- **Snapshot update workflow**: How should developers handle snapshot updates in PRs?
- **Test data management**: Should test fixtures use shared or isolated test data?

### Identified Risks
- **Snapshot maintenance overhead**: Large numbers of snapshots could be difficult to manage
- **Test execution time**: Comprehensive test suite might slow down CI/CD pipeline
- **Database test reliability**: EF Core tests need robust isolation and cleanup mechanisms

## Next Steps
- **Ready for implementation**: Create new `test/Facet.UnitTests` project with proper structure
- **Test migration approach**: Begin with source generator tests using Verify.SourceGenerators
- **Validation strategy**: Ensure each migrated test maintains equivalent coverage to original
- **CI/CD integration**: Update GitHub Actions workflow to include new test project
- **Documentation**: Update development documentation with new testing patterns
