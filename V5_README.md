# Migration Guide: Facet V5 Alpha > V5 GA

This guide helps you migrate from Facet 5.0.0-alpha releases to the stable 5.0.0 GA release.

---

## Quick Summary

### Breaking Changes
1. **Removed obsolete `GenerateBackTo` property** - Use `GenerateToSource` instead
2. **GenerateToSource** - Defaults to false. Set to true if you need the reverse mapper!

## Full Changelog (Alpha > GA)

### Added
- **[MapWhen]** - Conditional property mapping
- **[MapFrom]** - Declarative property renaming
- **[FlattenTo]** - Collection unpacking
- **SmartLeaf** flattening strategy
- Source signature tracking with FAC022 analyzer
- Automatic EF Core navigation loading
- Advanced async mapping with DI support
- Expression transformation utilities
- 17 comprehensive Roslyn analyzers
- Code fix providers for common issues

---

## Migration

### Step 1: Update Package References

Update all Facet packages to version 5.0.0:

```xml
<PackageReference Include="Facet" Version="5.0.0" />
<PackageReference Include="Facet.Extensions" Version="5.0.0" />
<PackageReference Include="Facet.Extensions.EFCore" Version="5.0.0" />
<PackageReference Include="Facet.Extensions.EFCore.Mapping" Version="5.0.0" />
<PackageReference Include="Facet.Mapping" Version="5.0.0" />
<PackageReference Include="Facet.Mapping.Expressions" Version="5.0.0" />
```

Or via CLI:
```bash
dotnet add package Facet --version 5.0.0
dotnet add package Facet.Extensions --version 5.0.0
dotnet add package Facet.Extensions.EFCore --version 5.0.0
```

### Step 2: Replace `GenerateBackTo` with `GenerateToSource`

**Search for:** `GenerateBackTo`

**Replace with:** `GenerateToSource`

#### Example

**Before (Alpha):**
```csharp
[Facet(typeof(User), GenerateBackTo = true)]
public partial class UserDto;

[Facet(typeof(Product), "InternalNotes", GenerateBackTo = false)]
public partial class ProductDto;
```

**After (GA):**
```csharp
[Facet(typeof(User), GenerateToSource = true)]
public partial class UserDto;

[Facet(typeof(Product), "InternalNotes", GenerateToSource = false)]
public partial class ProductDto;
```

#### Find All Occurrences

**Visual Studio:**
- Press `Ctrl+Shift+H` (Find and Replace in Files)
- Find: `GenerateBackTo`
- Replace: `GenerateToSource`
- Click "Replace All"

**Rider:**
- Press `Ctrl+Shift+R` (Replace in Path)
- Find: `GenerateBackTo`
- Replace: `GenerateToSource`
- Click "Replace All"

**VS Code:**
- Press `Ctrl+Shift+H` (Replace in Files)
- Find: `GenerateBackTo`
- Replace: `GenerateToSource`
- Click "Replace All"

**Command Line (PowerShell):**
```powershell
Get-ChildItem -Recurse -Include *.cs |
    ForEach-Object {
        (Get-Content $_.FullName) -replace 'GenerateBackTo', 'GenerateToSource' |
        Set-Content $_.FullName
    }
```

**Command Line (Bash):**
```bash
find . -name "*.cs" -type f -exec sed -i 's/GenerateBackTo/GenerateToSource/g' {} +
```

### Step 3: Rebuild Your Solution

```bash
dotnet clean
dotnet build
```

This ensures all generated code is regenerated with the new version.

### Step 4: Run Tests

```bash
dotnet test
```

Ensure all tests pass after the migration.

---

## Verification Checklist

- [ ] All Facet packages updated to 5.0.0
- [ ] All occurrences of `GenerateBackTo` replaced with `GenerateToSource`
- [ ] Solution builds without errors
- [ ] All tests pass
- [ ] No compiler warnings from Facet analyzers
- [ ] Generated code looks correct

---

## New Features You Can Use

After migrating, you can take advantage of new V5 features:

### 1. Conditional Mapping with [MapWhen]

```csharp
[Facet(typeof(Order))]
public partial class OrderDto
{
    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? CompletedAt { get; set; }
}
```

### 2. Declarative Mapping with [MapFrom]

```csharp
[Facet(typeof(User), GenerateToSource = true)]
public partial class UserDto
{
    [MapFrom(nameof(User.FirstName), Reversible = true)]
    public string Name { get; set; }
}
```

### 3. Collection Unpacking with [FlattenTo]

```csharp
[Facet(typeof(Data), FlattenTo = [typeof(DataFlattened)])]
public partial class DataDto;
```

### 4. Source Signature Tracking

```csharp
[Facet(typeof(User), SourceSignature = "a1b2c3d4")]
public partial class UserDto;
```

### 5. Enhanced Flattening

```csharp
[Flatten(typeof(Person),
    NamingStrategy = FlattenNamingStrategy.SmartLeaf,
    IgnoreNestedIds = true)]
public partial class PersonFlat { }
```

---

## API Changes Summary

| Alpha API | GA API | Status | Notes |
|-----------|--------|--------|-------|
| `GenerateBackTo` | `GenerateToSource` | :x: Removed | Breaking change |
| All other attributes | Unchanged | :white_check_mark: Stable | No changes needed |
| All extension methods | Unchanged | :white_check_mark: Stable | No changes needed |
| All analyzers | Enhanced | :white_check_mark: Compatible | Better messages |

---

## Success!

Once you've completed these steps, you're successfully migrated to Facet 5.0.0 GA!

Enjoy the new features and improved stability.

---

## Feedback

We'd love to hear about your migration experience:
- Was this guide helpful?
- Did you encounter any issues?
- What could be improved?

Please share feedback at: https://github.com/Tim-Maes/Facet/issues

---

**Happy coding with Facet 5.0.0!**
