# Debug

You are tasked with helping debug issues during Facet development or testing. This command allows you to investigate problems by examining build logs, generated code, test outputs, and git history without editing files. Think of this as a way to bootstrap a debugging session without using the primary window's context.

## Initial Response

When invoked WITH a plan/ticket file:
```
I'll help debug issues with [file name]. Let me understand the current state.

What specific problem are you encountering?
- What were you trying to build/test?
- What went wrong during generation or compilation?
- Any error messages or diagnostic codes?

I'll investigate the build logs, generated code, and git state to help figure out what's happening.
```

When invoked WITHOUT parameters:
```
I'll help debug your current Facet issue.

Please describe what's going wrong:
- What source generator feature are you working on?
- What specific problem occurred during build/test?
- When did it last work?

I can investigate build outputs, generated code, test results, and recent changes to help identify the issue.
```

## Environment Information

You have access to these key locations and tools:

**Build Outputs**:
- MSBuild logs: `dotnet build -v d`
- Source generator diagnostics: Look for FACET_EF001-004 codes
- Generated code: `test/Facet.Extensions.EFCore.Tests/Generated/`

**Test Results**:
- xUnit test outputs: `dotnet test --logger "console;verbosity=detailed"`
- Verify snapshot files: `*.received.*` and `*.verified.*`
- Test databases: InMemory and SQLite test databases

**Git State**:
- Check current branch, recent commits, uncommitted changes
- Main branch: `main`

**Source Generator Status**:
- Check if build succeeds: `dotnet build`
- Generated file count: `find . -name "*.g.cs" | wc -l`
- MSBuild task execution: Check for ExportEfModelTask execution
- Incremental compilation state: Check `bin/` and `obj/` folders

## Process Steps

### Step 1: Understand the Problem

After the user describes the issue:

1. **Read any provided context** (plan or ticket file):
   - Understand what they're implementing/testing
   - Note which phase or step they're on
   - Identify expected vs actual behavior

2. **Quick state check**:
   - Current git branch and recent commits
   - Any uncommitted changes
   - When the issue started occurring

### Step 2: Investigate the Issue

Spawn parallel Task agents for efficient investigation:

```
Task 1 - Check Build State:
Analyze the current build and compilation status:
1. Run dotnet build and check for compilation errors
2. Look for source generator diagnostic codes (FACET_EF001-004)
3. Check MSBuild task execution logs
4. Verify generated files exist in Generated/ folders
5. Look for incremental compilation issues
Return: Key build errors/warnings with context
```

```
Task 2 - Generated Code Analysis:
Check the current generated code state:
1. Examine generated files in test/Facet.Extensions.EFCore.Tests/Generated/
2. Look for compilation errors in generated .g.cs files
3. Verify efmodel.json is properly generated and readable
4. Check for missing or malformed generated methods
5. Analyze chain discovery output and usage patterns
Return: Generated code issues and patterns
```

```
Task 3 - Git and Test State:
Understand what changed recently and test status:
1. Check git status and current branch
2. Look at recent commits: git log --oneline -10
3. Check uncommitted changes: git diff
4. Run tests and check for failures: dotnet test
5. Look for Verify snapshot conflicts (.received vs .verified files)
Return: Git state and test failure information
```

### Step 3: Present Findings

Based on the investigation, present a focused debug report:

```markdown
## Debug Report

### What's Wrong
[Clear statement of the issue based on evidence]

### Evidence Found

**From Application State**:
- [Error/warning from Next.js console]
- [Client-side errors or hydration issues]
- [API route failures]

**From Database**:
```sql
-- Relevant query and result
[Finding from database]
```

**From Git/Files**:
- [Recent changes that might be related]
- [TypeScript or build errors]

### Root Cause
[Most likely explanation based on evidence]

### Next Steps

1. **Try This First**:
   ```bash
   [Specific command or action]
   ```

2. **If That Doesn't Work**:
   - Clean and rebuild: `dotnet clean && dotnet build`
   - Clear generated files: `rm -rf test/Facet.Extensions.EFCore.Tests/Generated/`
   - Regenerate EF model: Check for `efmodel.json` generation
   - Run specific tests: `dotnet test --filter "FluentBuilder"`
   - Check MSBuild binary logs: `dotnet build -bl` then analyze with MSBuild Log Viewer

### Can't Access?
Some issues might be outside my reach:
- Visual Studio debugger sessions
- IDE-specific error highlighting
- External NuGet package compatibility issues
- Platform-specific build issues (Windows vs macOS vs Linux)

Would you like me to investigate something specific further?
```

## Important Notes

- **Focus on source generator scenarios** - This is for debugging during Facet development
- **Always require problem description** - Can't debug without knowing what's wrong
- **Read files completely** - No limit/offset when reading context
- **Understand the generation pipeline** - Chain discovery → emission → compilation
- **Check incremental compilation** - Source generators use incremental patterns
- **No file editing** - Pure investigation only
- **Build system is complex** - MSBuild, source generators, and EF model export

## Quick Reference

**Check Application State**:
```bash
# Use Puppeteer to check UI at http://localhost:3000
# Navigate to /login-for-claude if auth needed
```

**Test Project Commands**:
```bash
# Run all Facet tests
dotnet test

# Run specific test projects
dotnet test test/Facet.Extensions.EFCore.Tests/Facet.Extensions.EFCore.Tests.csproj
dotnet test test/Facet.TestConsole/Facet.TestConsole.csproj

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test categories or filters
dotnet test --filter "FluentBuilder"
dotnet test --filter "Category=Integration"
dotnet test --filter "FullyQualifiedName~NavigationPropertyTests"

# Run tests and update Verify snapshots
dotnet test -- verify.autoverify=true

# Run console integration tests
dotnet run --project test/Facet.TestConsole/Facet.TestConsole.csproj
```

**Service Check**:
```bash
ps aux | grep "next dev"     # Is Next.js running?
lsof -i :3000               # What's on port 3000?
```

**Git State**:
```bash
git status
git log --oneline -10
git diff
```

**Common Issues**:
- Auth problems: Check both auth systems (platform vs sub-tenant)
- Routing issues: Verify vhost subdomain matches MCP server slug
- Hydration errors: Check for server/client data mismatches
- TypeScript errors: Run `bun run typecheck` if available

Remember: This command helps you investigate without burning the primary window's context. Perfect for when you hit an issue during manual testing and need to dig into logs, database, or application state.