# Validate Plan

You are tasked with validating that an implementation plan was correctly executed for the Facet Source Generator project, verifying all success criteria and identifying any deviations or issues.

## Initial Setup

When invoked:
1. **Determine context** - Are you in an existing conversation or starting fresh?
   - If existing: Review what was implemented in this session
   - If fresh: Need to discover what was done through git and codebase analysis

2. **Locate the plan**:
   - If plan path provided, use it
   - Otherwise, search recent commits for plan references or ask user
   - Look in `specifications/*/implementation-plan.md` or `specifications/*/*/implementation-plan.md`

3. **Gather implementation evidence**:
   ```bash
   # Check recent commits
   git log --oneline -n 20
   git diff HEAD~N..HEAD  # Where N covers implementation commits

   # Run Facet-specific checks
   dotnet build --no-restore --verbosity normal
   dotnet test --no-build --verbosity normal
   ```

## Validation Process

### Step 1: Context Discovery

If starting fresh or need more context:

1. **Read the implementation plan** completely using Read tool (WITHOUT limit/offset)
2. **Identify what should have changed**:
   - List all files that should be modified
   - Note all success criteria (automated and manual)
   - Identify key functionality to verify
   - Check if source generator patterns were specified

3. **Spawn parallel research tasks** to discover implementation:
   ```
   Task 1 - Verify source generator implementation:
   Research if source generators were added/modified according to plan.
   Check: src/Facet.Extensions.EFCore/Generators/ directory, FacetEfGenerator.cs, emission files
   Look for: new generators, incremental generation patterns, diagnostic implementations
   Return: What was implemented vs what plan specified

   Task 2 - Verify code emission patterns:
   Find all emitter classes that should have been added/modified.
   Check: FluentBuilderEmitter.cs, SelectorsEmitter.cs, ShapeInterfacesEmitter.cs
   Look for: proper emission logic, usage-based generation, chain discovery integration
   Return: Emitter implementation vs plan specifications

   Task 3 - Verify test implementations:
   Check if test patterns using Verify.SourceGenerators were implemented as specified.
   Look in: test/Facet.UnitTests/, test/Facet.Extensions.EFCore.Tests/
   Check: snapshot tests, MSBuild task tests, integration tests with TestDbContext
   Return: Test coverage vs plan requirements

   Task 4 - Verify MSBuild integration:
   Check if MSBuild targets and tasks were implemented correctly.
   Look for: .targets files, ExportEfModelTask, analyzer references in .csproj files
   Return: Build integration conformance to Facet patterns
   ```

### Step 2: Systematic Validation

For each phase in the implementation plan document

1. **Check completion status**:
   - Look for checkmarks in the plan (- [x])
   - Verify the actual code matches claimed completion

2. **Run automated verification**:
   - Execute each command from "Automated Verification" section
   - Use Facet-specific validation tools:
     ```bash
     dotnet build --configuration Release --no-restore
     dotnet test --configuration Release --no-build --verbosity normal
     dotnet pack --configuration Release --no-build --output ./artifacts
     ```
   - Document pass/fail status
   - If failures, investigate root cause

3. **Assess manual criteria**:
   - List what needs manual testing (especially test console validation)
   - Check if generated code compiles without errors or warnings
   - Verify incremental generation performance
   - Run test console application: `cd test/Facet.TestConsole && dotnet run --configuration Release`

4. **Think deeply about Facet source generator patterns**:
   - Are source generators implementing IIncrementalGenerator correctly?
   - Do emitters use proper incremental generation with IncrementalValueProvider?
   - Are diagnostic descriptors properly categorized with unique IDs?
   - Does chain discovery analyze actual usage patterns and avoid 2^N type explosion?
   - Are generated files following Facet naming conventions and patterns?
   - Is error handling robust with proper diagnostic reporting and graceful degradation?

### Step 3: Generate Validation Report

Create comprehensive validation summary:

```markdown
## Validation Report: [Plan Name]

### Implementation Status
✅ Phase 1: [Name] - Fully implemented
✅ Phase 2: [Name] - Fully implemented  
⚠️ Phase 3: [Name] - Partially implemented (see issues)

### Automated Verification Results
✅ Build succeeds: `dotnet build --configuration Release --no-restore`
✅ Tests pass: `dotnet test --configuration Release --no-build --verbosity normal`
✅ Package creation: `dotnet pack --configuration Release --no-build --output ./artifacts`
❌ Test console validation fails: `cd test/Facet.TestConsole && dotnet run` (2 failures)

### Code Review Findings

#### Matches Plan:
- Source generator correctly implements incremental generation with IIncrementalGenerator
- Emitters implement specified emission patterns with proper usage discovery
- Test patterns follow Verify.SourceGenerators with snapshot validation
- MSBuild integration properly exports EF models and integrates with analyzers

#### Deviations from Plan:
- Used different emitter organization in [file:line] (architectural improvement)
- Added extra diagnostic reporting in [file:line] (enhancement)
- Optimized chain discovery algorithm for better performance

#### Facet Source Generator Pattern Compliance:
✅ Generators implement IIncrementalGenerator with proper caching
✅ Emitters use IncrementalValueProvider for performance optimization
✅ Diagnostics follow structured pattern with unique IDs (FACET_EF001, etc.)
✅ Chain discovery prevents combinatorial explosion through usage analysis
❌ Missing error recovery in [emitter:line] for malformed input

#### Potential Issues:
- Missing null checks in generated code could cause runtime exceptions
- Chain discovery may not handle edge cases with generic types
- Generated code doesn't include XML documentation comments

### Manual Testing Required:
1. **Test Console Validation**:
   - [ ] Run `cd test/Facet.TestConsole && dotnet run --configuration Release`
   - [ ] Verify all test scenarios complete without exceptions
   - [ ] Check generated fluent API methods work correctly
   - [ ] Test error handling with invalid navigation chains

2. **Integration Testing**:
   - [ ] Confirm generated code compiles with target projects
   - [ ] Test with different EF models and DTO configurations
   - [ ] Verify incremental generation triggers only when needed

3. **Performance Testing**:
   - [ ] Check compilation times with large EF models
   - [ ] Verify generated code doesn't impact runtime query performance
   - [ ] Test memory usage during design-time generation

### Source Generator Verification:
- [ ] Generators registered correctly in .csproj as analyzers
- [ ] Generated files appear in obj/Generated/ directory
- [ ] MSBuild export task creates efmodel.json successfully
- [ ] Chain discovery detects usage patterns accurately

### Code Quality Review:
- [ ] All generated code follows established naming conventions
- [ ] No compiler warnings or errors in generated output
- [ ] Proper null handling in generated extension methods
- [ ] Generated code includes appropriate using statements

### Recommendations:
- Address any compiler warnings before merge
- Add missing diagnostic handlers for edge cases
- Consider adding performance benchmarks for large models
- Update documentation if new generation patterns were established
```

## Working with Existing Context

If you were part of the implementation:
- Review the conversation history for what was actually done
- Check your todo list (if any) for completed items
- Focus validation on work done in this session
- Be honest about any shortcuts or incomplete items
- Note any decisions that deviated from the plan and why

## Facet Source Generator-Specific Checks

Always verify Facet source generator patterns:

### Source Generator Implementation
- [ ] Generators implement `IIncrementalGenerator` interface
- [ ] Use `IncrementalValueProvider` for performance optimization
- [ ] Proper registration in `.csproj` files as analyzers
- [ ] Generated files excluded from compilation

### Code Emission Patterns
- [ ] Emitters in `src/Facet.Extensions.EFCore/Generators/Emission/`
- [ ] Consistent `Emit()` method signatures across emitters
- [ ] Usage-based generation to prevent combinatorial explosion
- [ ] Proper handling of generic type constraints
- [ ] Generated code follows established naming conventions

### Testing Infrastructure (Verify.SourceGenerators)
- [ ] Tests use `Verify.SourceGenerators` with snapshot validation
- [ ] `[ModuleInitializer]` properly initializes Verify framework
- [ ] Test naming follows `MethodName_WithCondition_ExpectedResult` pattern
- [ ] Snapshot files organized in dedicated directories
- [ ] Generated test files excluded from source control

### MSBuild Integration
- [ ] `.targets` files properly configured for analyzer integration
- [ ] `ExportEfModelTask` generates efmodel.json before compilation
- [ ] Additional files registered for source generator consumption
- [ ] Incremental build support with proper input/output tracking

### Diagnostic System
- [ ] Diagnostic descriptors with unique IDs (FACET_EF001, etc.)
- [ ] Appropriate severity levels for different error types
- [ ] Structured error messages with actionable guidance
- [ ] Graceful degradation when encountering invalid input

## Important Guidelines

1. **Be thorough but practical** - Focus on what matters for shipping
2. **Run all automated checks** - Don't skip build/test verification
3. **Document everything** - Both successes and issues
4. **Think critically** - Question if implementation truly solves the problem
5. **Consider maintenance** - Will this be maintainable long-term?
6. **Respect incremental generation** - Test performance with large models

## Validation Checklist

Always verify:
- [ ] All phases marked complete are actually implemented
- [ ] `dotnet build` succeeds without warnings or errors
- [ ] `dotnet test` passes with comprehensive coverage
- [ ] Code follows Facet source generator patterns
- [ ] No regressions in existing generation functionality
- [ ] Incremental generation performance maintained
- [ ] Error handling provides meaningful diagnostics
- [ ] Manual test steps with test console are clear
- [ ] Generated code compiles correctly in target projects

## Common Issues to Check

### Source Generator Issues
- Generators not implementing `IIncrementalGenerator` correctly
- Missing incremental value providers causing performance issues
- Analyzer references not configured properly in project files
- Generated files not excluded from compilation

### Code Emission Issues
- Emitters missing consistent `Emit()` method signatures
- Combinatorial explosion due to lack of usage-based generation
- Generic type constraints not handled properly
- Generated code doesn't follow naming conventions

### Testing Infrastructure Issues
- Tests not using `Verify.SourceGenerators` framework
- Missing `[ModuleInitializer]` for Verify setup
- Test naming not following established patterns
- Snapshot files not organized properly

### MSBuild Integration Issues
- Missing or misconfigured `.targets` files
- EF model export task not running before compilation
- Additional files not registered for analyzer consumption
- Incremental build not working due to missing input/output tracking

### Diagnostic Issues
- Diagnostic descriptors missing unique IDs
- Inappropriate severity levels for error types
- Error messages not actionable or structured
- No graceful degradation for invalid input

## Addenda:
Remember: Good validation catches issues before they reach users. Be constructive but thorough in identifying gaps or improvements that align with Facet's source generator patterns and quality standards.

### Facet-Specific Validation Commands:
```bash
# Core validation commands
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --verbosity normal
dotnet pack --configuration Release --no-build --output ./artifacts

# Test console validation
cd test/Facet.TestConsole
dotnet run --configuration Release --no-build

# Check generated files
ls -la test/Facet.UnitTests/Generated/
ls -la test/Facet.Extensions.EFCore.Tests/Generated/
```

### Key Facet Patterns to Validate:
1. **Incremental Generation**: Uses `IIncrementalGenerator` with proper caching
2. **Usage-Based Emission**: Chain discovery prevents combinatorial explosion
3. **Verify Testing**: Snapshot-based validation with `Verify.SourceGenerators`
4. **MSBuild Integration**: Analyzer registration and EF model export
5. **Structured Diagnostics**: Unique IDs and actionable error messages
6. **Performance Optimization**: No significant overhead in generated code execution