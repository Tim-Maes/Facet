# write_tests.md - Facet Source Generator Testing Guide

You are tasked with writing tests for the Facet Source Generator codebase, focusing on source generator functionality, Entity Framework integration, and generated code verification.

## Initial Response

When this command is invoked:

1. **Check if parameters were provided**:
   - If a feature idea, description, context, or rough specification was provided as a parameter, begin the discovery process with that context
   - If files are referenced, read them FULLY first to understand existing context
   - If no parameters provided, respond with the default prompt below

2. IMPORTANT: **If no parameters provided**, respond with:
```
I'm ready to help you write tests for Facet source generators. Please provide a feature, implementation plan, file path(s) or directory, and I will analyze it thoroughly and write comprehensive tests.

What are you looking to test? This could be:
- A new source generator feature ("FluentBuilder generation for...")
- An existing generator that needs more coverage ("SelectorsEmitter...")
- Integration tests for EF Core functionality
- Generated code verification tests

Don't worry about having all the details - we'll explore the codebase and create comprehensive tests together!

Tip: You can also invoke this command with context: `/write_tests FluentBuilderEmitter`
```

Then wait for the user's input.

## Core Testing Principles for Facet

### 1. Test Generated Code, Not Just Generation Logic
- **DO** test that generated code compiles and works correctly
- **DO** verify generated code matches expected patterns
- **DO** test incremental compilation behavior
- **DON'T** only test generator internals without verifying output

### 2. Use Real Entity Framework Context
- **DO** use actual Entity Framework contexts in tests
- **DO** test against real EF model metadata
- **DON'T** mock DbContext or entity metadata
- **DO** use the TestDbContext from test fixtures

### 3. Test Complete Source Generator Pipeline
- **DO** test end-to-end source generation
- **DO** verify MSBuild integration works
- **DO** test incremental compilation scenarios
- **DON'T** test individual methods in isolation

### 4. Verify Generated Code Quality
- **DO** test that generated code follows C# conventions
- **DO** verify generated code handles edge cases
- **DO** test nullable reference type annotations
- **DO** ensure generated code is performant

## Test Structure for Facet

### Project Structure
- Unit tests: `test/Facet.UnitTests/`
- EF Integration tests: `test/Facet.Extensions.EFCore.Tests/`
- Generated code verification: `test/Facet.Extensions.EFCore.Tests/Generated/`

### Test Naming Convention
- Test files: `[ClassName]Tests.cs`
- Test methods: `[MethodName]_[Scenario]_[ExpectedBehavior]`

Example:
```csharp
[Test]
public void EmitFluentBuilder_WithNavigationProperties_GeneratesWithMethods()
```

## Common Test Patterns

### Testing Source Generators

```csharp
[Test]
public void FacetEfGenerator_WithValidEntity_GeneratesExpectedCode()
{
    // Arrange
    var source = """
        using Microsoft.EntityFrameworkCore;
        using Facet;
        
        public class TestContext : DbContext
        {
            public DbSet<User> Users { get; set; }
        }
        
        [GenerateDtos]
        public class User
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
        """;

    // Act
    var result = RunGenerator(source);

    // Assert
    result.Diagnostics.Should().BeEmpty();
    result.GeneratedSources.Should().HaveCount(1);
    result.GeneratedSources[0].SourceText.ToString()
        .Should().Contain("public static IQueryable<UserDto>");
}
```

### Testing EF Integration

```csharp
[Test]
public void FluentBuilderEmitter_WithTestDbContext_GeneratesWorkingBuilders()
{
    // Arrange
    using var context = new TestDbContext();
    var emitter = new FluentBuilderEmitter();
    
    // Act
    var generated = emitter.EmitForContext(context);
    
    // Assert
    generated.Should().NotBeEmpty();
    
    // Verify the generated code compiles and works
    var compilation = CreateCompilation(generated);
    compilation.GetDiagnostics().Should().BeEmpty();
}
```

### Testing Generated Code Behavior

```csharp
[Test]  
public void GeneratedSelectors_WithComplexEntity_ProjectCorrectly()
{
    // Arrange
    using var context = new TestDbContext();
    context.Users.Add(new User { Name = "Test User" });
    context.SaveChanges();
    
    // Act - Use generated projection
    var result = context.Users.SelectUserDto().First();
    
    // Assert
    result.Name.Should().Be("Test User");
    result.Should().BeOfType<UserDto>();
}
```

## Test Utilities and Helpers

### Source Generator Test Helper

```csharp
public static class SourceGeneratorTestHelper
{
    public static GeneratorDriverRunResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(GenerateDtosAttribute).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new FacetEfGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        
        return driver.RunGenerators(compilation);
    }
}
```

### Entity Framework Test Setup

```csharp
public class TestDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        });
        
        // Configure other entities...
    }
}
```

## Running Tests

### Command Line
```bash
# Run all tests (solution-level - test projects are in .sln)
dotnet test

# Run specific test projects
dotnet test test/Facet.Extensions.EFCore.Tests/
dotnet test test/Facet.UnitTests/

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific tests by filter
dotnet test --filter "FullyQualifiedName~FluentBuilderEmitterTests"
dotnet test --filter "TestProject=Facet.Extensions.EFCore.Tests"
```

### Test Categories
Use test categories to organize different types of tests:

```csharp
[Test]
[Category("Unit")]
public void EmitFluentBuilder_ValidInput_GeneratesCode() { }

[Test]
[Category("Integration")]  
public void SourceGenerator_WithEFContext_GeneratesWorkingCode() { }

[Test]
[Category("Generated")]
public void GeneratedCode_CompileAndRun_WorksCorrectly() { }
```

## Key Testing Areas for Facet

### 1. Source Generator Core Functionality
- Test generator registration and execution
- Verify incremental compilation behavior
- Test with various EF model configurations

### 2. Code Generation Accuracy
- Verify generated DTOs match entity structure
- Test selector generation for complex projections
- Validate fluent builder generation

### 3. EF Integration
- Test with real DbContext instances
- Verify metadata extraction works correctly
- Test with various entity configurations

### 4. Generated Code Quality
- Ensure generated code compiles without warnings
- Test generated code follows C# conventions
- Verify nullable reference type handling

### 5. Edge Cases and Error Handling
- Test with invalid entity configurations
- Verify diagnostic messages are helpful
- Test behavior with circular references

## Anti-Patterns to Avoid

### Don't Mock EF Components
```csharp
// DON'T DO THIS
var mockContext = new Mock<DbContext>();
var mockSet = new Mock<DbSet<User>>();

// DO THIS INSTEAD
using var context = new TestDbContext();
```

### Don't Test Only Internal Logic
```csharp
// DON'T DO THIS - Only testing internal methods
[Test]
public void GetEntityProperties_ReturnsProperties() { }

// DO THIS INSTEAD - Test the complete pipeline
[Test]
public void SourceGenerator_GeneratesWorkingDtoSelectors() { }
```

### Don't Skip Generated Code Verification
```csharp
// DON'T DO THIS - Only check generation occurred
result.GeneratedSources.Should().HaveCount(1);

// DO THIS INSTEAD - Verify generated code quality
var generated = result.GeneratedSources[0].SourceText.ToString();
generated.Should().Contain("public static IQueryable<UserDto>");
var compilation = CreateCompilation(generated);
compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
    .Should().BeEmpty();
```

## Integration with CI/CD

### Test Configuration
Ensure tests run in CI with proper configuration:

```xml
<Project>
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
  </PropertyGroup>
</Project>
```

### Performance Testing
Include performance benchmarks for generated code:

```csharp
[Test]
public void GeneratedProjections_PerformanceBaseline()
{
    using var context = new TestDbContext();
    // Add test data...
    
    var stopwatch = Stopwatch.StartNew();
    var results = context.Users.SelectUserDto().ToList();
    stopwatch.Stop();
    
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
}
```

Remember: **Test the generated code behavior, not just the generation logic. Use real EF contexts and verify end-to-end functionality.**