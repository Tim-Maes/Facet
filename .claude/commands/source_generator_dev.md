# Source Generator Development

You are tasked with developing, enhancing, or debugging Facet source generators. This command provides specialized workflows for working with the Facet source generation architecture, including incremental compilation, MSBuild integration, and code emission patterns.

## Initial Response

When invoked WITH a specific generator or feature:
```
I'll help with [generator name/feature] development. Let me understand the current implementation and requirements.

What are you looking to accomplish?
- Adding new emission capabilities?
- Fixing generation bugs or diagnostics?
- Optimizing incremental compilation performance?
- Enhancing chain discovery logic?

I'll analyze the existing patterns and provide targeted development assistance.
```

When invoked WITHOUT parameters:
```
I'm ready to help with Facet source generator development.

What generator area would you like to work on?
- FacetGenerator (individual DTO generation)
- GenerateDtosGenerator (bulk DTO generation)
- FacetEfGenerator (EF Core fluent builders)
- Emission logic (FluentBuilderEmitter, SelectorsEmitter)
- Chain discovery and usage analysis
- Diagnostics and error handling
- Testing and verification

I can help with implementation, debugging, optimization, or adding new features.
```

## Core Development Areas

### 1. **Source Generator Architecture**
- Incremental generator patterns with IIncrementalGenerator
- Syntax provider configuration and filtering
- Compilation context analysis and symbol processing
- Multi-generator coordination and data sharing

### 2. **Code Emission and Templates**
- FluentBuilderEmitter for type-safe navigation builders
- SelectorsEmitter for Entity Framework projections
- Interface generation (shapes and capabilities)
- Code formatting and documentation preservation

### 3. **Chain Discovery System**
- Syntax tree analysis for method chain detection
- Usage pattern recognition and optimization
- Depth capping and circular dependency prevention
- Performance optimization for large codebases

### 4. **MSBuild Integration**
- ExportEfModelTask for Entity Framework metadata
- Build target configuration and execution
- Package deployment and NuGet integration
- Incremental build support

## Development Workflow

### Step 1: Analyze Current Implementation
1. **Examine existing generator code** to understand patterns
2. **Review test files** to understand expected behavior
3. **Check generated code samples** for output format
4. **Analyze diagnostics and error handling** patterns

### Step 2: Plan Enhancement
1. **Identify integration points** with existing generators
2. **Design emission templates** following established patterns
3. **Plan incremental compilation** support
4. **Consider performance implications** and optimization

### Step 3: Implement with Testing
1. **Follow established patterns** from existing generators
2. **Implement comprehensive diagnostics** with helpful messages
3. **Add thorough test coverage** using Verify.SourceGenerators
4. **Test incremental compilation** behavior

### Step 4: Integration and Verification
1. **Verify generator coordination** works correctly
2. **Test MSBuild integration** and package deployment
3. **Performance test** with realistic codebases
4. **Validate generated code quality** and compilation

## Key Patterns and Standards

### Incremental Generator Structure
```csharp
[Generator(LanguageNames.CSharp)]
public sealed class MyFacetGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Configure syntax providers with proper filtering
        var targets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Fully.Qualified.AttributeName",
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, token) => ExtractModel(ctx, token))
            .Where(static model => model is not null);

        // 2. Combine with other data sources
        var combined = targets.Combine(otherProvider);

        // 3. Register source output with error handling
        context.RegisterSourceOutput(combined, static (spc, data) =>
        {
            try
            {
                GenerateCode(spc, data);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.GenerationError,
                    Location.None,
                    ex.Message));
            }
        });
    }
}
```

### Code Emission Pattern
```csharp
internal static class MyEmitter
{
    public static void Emit(SourceProductionContext context, 
                          ModelRoot efModel, 
                          ImmutableArray<FacetDtoInfo> facetDtos)
    {
        foreach (var dto in facetDtos)
        {
            var source = GenerateSource(dto);
            var sourceText = SourceText.From(source, Encoding.UTF8);
            context.AddSource($"{dto.DtoTypeName}.g.cs", sourceText);
        }
    }

    private static string GenerateSource(FacetDtoInfo dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        // ... generate code
        return sb.ToString();
    }
}
```

### Diagnostic Pattern
```csharp
public static class Diagnostics
{
    public static readonly DiagnosticDescriptor MyError = new(
        id: "FACET_EF006",
        title: "My specific error",
        messageFormat: "Error in {0}: {1}",
        category: "Facet.Extensions.EFCore",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
```

## Testing Guidelines

### Unit Test Structure
```csharp
[Fact]
public Task MyGenerator_WithValidInput_GeneratesExpectedCode()
{
    // Arrange
    var source = """
        using Facet;
        [MyAttribute]
        public class TestEntity { }
        """;

    var compilation = CreateCompilation(source);
    var generator = new MyFacetGenerator();

    // Act
    var driver = CSharpGeneratorDriver.Create(generator)
        .RunGenerators(compilation);

    // Assert - Use Verify.SourceGenerators
    return Verify(driver)
        .UseDirectory("Snapshots")
        .UseFileName("ValidInput");
}
```

### Integration Test Structure
```csharp
[Fact]
public async Task GeneratedCode_WithTestDbContext_WorksCorrectly()
{
    // Arrange
    using var context = new TestDbContext();
    
    // Act - Use generated code
    var results = await context.MyEntities.SelectMyDto().ToListAsync();
    
    // Assert
    Assert.NotEmpty(results);
    Assert.All(results, dto => Assert.NotEqual(0, dto.Id));
}
```

## Common Tasks

### Adding New Emission Capability
1. **Study existing emitters** (FluentBuilderEmitter, SelectorsEmitter)
2. **Design code template** following established patterns
3. **Integrate with main generator** coordination logic
4. **Add comprehensive tests** with snapshot verification
5. **Update diagnostics** with helpful error messages

### Optimizing Performance
1. **Profile incremental compilation** behavior
2. **Optimize syntax filtering** for better performance
3. **Cache expensive operations** appropriately
4. **Test with large codebases** for scalability
5. **Monitor memory usage** during generation

### Enhancing Chain Discovery
1. **Analyze new usage patterns** in syntax trees
2. **Extend chain normalization** logic
3. **Improve depth capping** with better diagnostics
4. **Test with complex navigation** scenarios
5. **Validate performance impact** of changes

### Fixing Generation Issues
1. **Reproduce issue** with minimal test case
2. **Analyze syntax tree** and symbol information
3. **Check incremental compilation** state
4. **Verify diagnostic reporting** provides helpful info
5. **Add regression tests** to prevent future issues

## Advanced Topics

### Generator Coordination
- Multiple generators must coordinate through shared data
- Use immutable data structures for thread safety
- Implement proper cancellation token support
- Handle generator execution order dependencies

### MSBuild Integration
- ExportEfModelTask runs during build to export EF metadata
- Generated files must be included in compilation
- Support both PackageReference and ProjectReference scenarios
- Handle incremental builds correctly

### Performance Optimization
- Incremental generators only regenerate when inputs change
- Use efficient syntax filtering predicates
- Cache expensive symbol analysis operations
- Monitor memory usage during generation

### Error Recovery
- Gracefully handle malformed input
- Provide helpful diagnostic messages
- Continue generation for other valid inputs
- Report structured errors with location information

## Debugging Tools

### MSBuild Diagnostics
```bash
# Enable detailed MSBuild logging
dotnet build -v detailed

# Generate binary log for analysis
dotnet build -bl

# Check source generator execution
dotnet build -v diagnostic | grep -i "source.*generator"
```

### Generated Code Analysis
```bash
# View generated files
find . -name "*.g.cs" -type f

# Check compilation errors in generated code
grep -r "CS[0-9][0-9][0-9][0-9]" test/*/Generated/

# Analyze source generator performance
dotnet build --verbosity diagnostic 2>&1 | grep -i "elapsed.*generator"
```

### Test Verification
```bash
# Run source generator tests with verbose output
dotnet test test/Facet.Extensions.EFCore.Tests/ -v detailed

# Update Verify snapshots when patterns change
dotnet test -- verify.autoverify=true

# Run specific generator tests
dotnet test --filter "FluentBuilderEmitter"
```

## Best Practices

### 1. **Follow Established Patterns**
- Use incremental generators for all new generators
- Follow existing emission template styles
- Maintain consistent diagnostic error codes
- Preserve existing testing approaches

### 2. **Performance Considerations**
- Filter syntax early with efficient predicates
- Use immutable data structures appropriately
- Implement proper cancellation token support
- Test with realistic codebase sizes

### 3. **Error Handling**
- Provide specific, actionable diagnostic messages
- Include context information in error reports
- Handle edge cases gracefully
- Test error scenarios thoroughly

### 4. **Code Quality**
- Generate readable, well-formatted code
- Include appropriate documentation comments
- Follow C# coding conventions
- Use proper nullable reference type annotations

### 5. **Testing Strategy**
- Use Verify.SourceGenerators for all output validation
- Test both positive and negative scenarios
- Include incremental compilation tests
- Validate generated code compiles and runs

Remember: Facet's source generator architecture is sophisticated and performance-critical. Always consider incremental compilation impact, maintain thread safety, and follow established patterns for consistency with the existing codebase.