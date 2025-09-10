# OutputPath Feature for Facet

The `OutputPath` property on `FacetAttribute` allows you to specify where generated facet files should be written to disk, in addition to being included in the compilation.

## Basic Usage

```csharp
// Generate UserDto and write it to the Generated/Dtos directory
[Facet(typeof(User), "Password", OutputPath = "Generated/Dtos")]
public partial class UserDto;

// Generate UserDto and write it to a specific file path
[Facet(typeof(User), "Password", OutputPath = "Generated/SafeUserDto.cs")]
public partial class SafeUserDto;

// Normal facet without OutputPath (existing behavior)
[Facet(typeof(User), "Password")]
public partial class NormalUserDto;
```

## Path Resolution

- **Relative paths**: Resolved relative to the project directory
- **Absolute paths**: Used as-is
- **Directory paths**: The generated filename (`{FacetName}.g.cs`) is appended
- **File paths**: Used directly (should end with `.cs`)

## Examples

```csharp
// Write to subdirectory
[Facet(typeof(User), OutputPath = "Generated/Dtos")]
public partial class UserDto; // → Generated/Dtos/UserDto.g.cs

// Write to specific file
[Facet(typeof(User), OutputPath = "Shared/Models/UserModel.cs")]
public partial class UserModel; // → Shared/Models/UserModel.cs

// Write to different project (absolute path)
[Facet(typeof(User), OutputPath = "/path/to/shared/project/Dtos/")]
public partial class SharedUserDto; // → /path/to/shared/project/Dtos/SharedUserDto.g.cs
```

## Implementation Notes

The `OutputPath` feature is implemented in phases:

1. **Phase 1 (Current)**: The source generator emits diagnostic messages indicating where files should be written
2. **Phase 2 (Future)**: Integration with MSBuild tasks to automatically write files during build
3. **Phase 3 (Future)**: IDE integration for real-time file writing

### Phase 1: Diagnostic Messages

When `OutputPath` is specified, the source generator emits an informational diagnostic with ID `FACET_OUTPUT_PATH`:

```
info FACET_OUTPUT_PATH: Generated code for 'UserDto' should be written to: Generated/Dtos
```

External tooling can parse these diagnostics and the generated source to write files to the specified locations.

### Current Limitations

- Files are not automatically written to disk (source generators have security restrictions)
- Manual or external tooling integration required for file writing
- Generated files are still included in compilation as normal

### Future Enhancements

- MSBuild integration for automatic file writing
- Visual Studio extension for real-time updates
- Watch mode for development scenarios
- Support for template-based file naming

## Migration from T4 Templates

This feature provides similar functionality to T4 template output redirection:

```csharp
// T4 equivalent: <#@ output extension=".cs" #>
// Facet equivalent:
[Facet(typeof(User), OutputPath = "Generated/UserDto.cs")]
public partial class UserDto;
```

The key advantage over T4 is that facets remain strongly typed and integrated with the compilation process while providing the flexibility to write files to specific locations.