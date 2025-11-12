# Generated Files Output Configuration

By default, Roslyn source generators (including Facet) output generated files to the `obj/Generated/` folder, which is hidden from the Solution Explorer. However, you can configure where generated files are written using standard MSBuild properties.

## Making Generated Files Visible

To make generated files visible in your project and control where they are output, add the following properties to your `.csproj` file:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

### Configuration Options

#### 1. Output to Project Folder (Recommended)

Place generated files in a `Generated` folder within your project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>

<!-- Exclude from compilation to avoid duplicate definitions -->
<ItemGroup>
  <Compile Remove="Generated\**" />
  <!-- Keep them visible in Solution Explorer -->
  <None Include="Generated\**" />
</ItemGroup>
```

This configuration:
- Makes generated files visible in Solution Explorer
- Allows you to browse and inspect generated code
- Excludes them from compilation (they're already compiled as generated files)
- Useful for debugging and understanding what code is being generated

#### 2. Output to obj Folder (Default Behavior)

Keep generated files in the obj folder but make them visible:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

This is what Facet's test projects use internally.

#### 3. Output to Shared Project

Generate files in a separate shared project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>..\MySharedProject\Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

**Important**: When outputting to a shared project, ensure the types being generated are `partial` so they can be merged with the declarations in your source project.

## File Structure

With the default configuration, generated files are organized by generator:

```
Generated/
├── Facet/
│   ├── Facet.Generators.FacetGenerator/
│   │   ├── UserDto.g.cs
│   │   ├── ProductDto.g.cs
│   │   └── ...
│   ├── Facet.Generators.FlattenGenerator/
│   │   └── ...
│   └── Facet.Generators.GenerateDtosGenerator/
│       └── ...
└── OtherGenerator/
    └── ...
```

## Benefits of Visible Generated Files

1. **Debugging**: Easier to see what code is being generated and debug issues
2. **Learning**: Understand how Facet generates code by inspecting the output
3. **Code Reviews**: Include generated files in code reviews if needed
4. **Documentation**: Auto-generated code serves as documentation

## Important Notes

- **Do NOT manually edit generated files** - they will be overwritten on the next build
- Generated files are **recreated on every build** based on your source code and attributes
- **Do NOT commit generated files to source control** unless you have a specific reason (add to `.gitignore`)
- When placing files in the project folder, **always exclude them from compilation** using `<Compile Remove="Generated\**" />`

## Example .gitignore

If you choose to make generated files visible in your project, add this to your `.gitignore`:

```gitignore
# Facet generated files
Generated/
**/Generated/
```

## Troubleshooting

### Duplicate Type Definitions

If you see errors about duplicate type definitions, ensure you've excluded the Generated folder from compilation:

```xml
<ItemGroup>
  <Compile Remove="Generated\**" />
</ItemGroup>
```

### Files Not Appearing

1. Clean and rebuild your solution
2. Verify `EmitCompilerGeneratedFiles` is set to `true`
3. Check the output path exists and is accessible
4. Ensure you're using a recent .NET SDK (6.0+)

### Performance Considerations

Emitting generated files to disk has minimal performance impact. However, if you have thousands of generated files and are using source control, consider:
- Keeping them in the `obj` folder (default)
- Adding the output directory to `.gitignore`

