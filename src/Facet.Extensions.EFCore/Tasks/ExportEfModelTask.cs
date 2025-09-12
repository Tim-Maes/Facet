using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Facet.Extensions.EFCore.Tasks;

/// <summary>
/// MSBuild task that exports EF Core model metadata to JSON for source generation.
/// </summary>
public sealed class ExportEfModelTask : Task
{
    // Cache for compiled constructor delegates
    private static readonly ConcurrentDictionary<Type, Func<object, object>> ConstructorCache = new();
    // Intentionally avoid global Resolving hooks; we attach a scoped handler during Execute()
    static ExportEfModelTask() { }
    /// <summary>
    /// Path to the assembly containing DbContext types.
    /// </summary>
    [Required]
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of DbContext type names to export. If empty, exports all found contexts.
    /// </summary>
    public string? ContextTypes { get; set; }

    /// <summary>
    /// Output path for the efmodel.json file.
    /// </summary>
    [Required]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Optional provider hint: one of 'inmemory', 'npgsql', 'sqlite', 'sqlserver'.
    /// If not provided, the task will attempt to use a design-time factory, then
    /// auto-detect provider assemblies, then fall back to InMemory.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Optional directory to probe for dependencies (e.g., the web/common bin output with transitive packages).
    /// </summary>
    public string? ProbeDirectory { get; set; }

    public override bool Execute()
    {
        try
        {
            // Normalize ProbeDirectory to an absolute path if provided
            if (!string.IsNullOrWhiteSpace(ProbeDirectory))
            {
                try { ProbeDirectory = Path.GetFullPath(ProbeDirectory!); }
                catch { /* best-effort */ }
            }

            // Validate OutputPath
            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                throw new ArgumentException("OutputPath cannot be null or empty");
            }

                Log.LogMessage(MessageImportance.Normal, $"Facet.Export: AssemblyPath='{AssemblyPath}' OutputPath='{OutputPath}' ProbeDirectory='{ProbeDirectory}' ContextTypes='{ContextTypes}' ProviderHint='{Provider}'");

            if (!File.Exists(AssemblyPath))
            {
                Log.LogError($"Facet.Export: Assembly not found at {AssemblyPath}");
                return false;
            }

            // Use default ALC with a temporary resolving handler to avoid type-identity mismatches
            var resolver = new AssemblyDependencyResolver(AssemblyPath);
            var ctxDir = Path.GetDirectoryName(AssemblyPath)!;
            var taskAsmDir = Path.GetDirectoryName(typeof(ExportEfModelTask).Assembly.Location);

            Assembly? ResolveHandler(AssemblyLoadContext context, AssemblyName name)
            {
                Log.LogMessage(MessageImportance.High, $"Facet.Export: Resolving dependency '{name.FullName}'");

                var path = resolver.ResolveAssemblyToPath(name);
                if (path != null && File.Exists(path))
                {
                    Log.LogMessage(MessageImportance.High, $"Facet.Export: Found dependency via resolver at '{path}'");
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                }

                // Fallback: probe next to the context assembly
                var local = Path.Combine(ctxDir, name.Name + ".dll");
                if (File.Exists(local))
                {
                    Log.LogMessage(MessageImportance.High, $"Facet.Export: Found dependency locally at '{local}'");
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(local);
                }

                // Fallback: probe an optional directory with richer transitive outputs
                if (!string.IsNullOrWhiteSpace(ProbeDirectory))
                {
                    var probe = Path.Combine(ProbeDirectory!, name.Name + ".dll");
                    if (!Path.IsPathFullyQualified(probe))
                    {
                        try { probe = Path.GetFullPath(probe); } catch { }
                    }
                    if (File.Exists(probe))
                    {
                        Log.LogMessage(MessageImportance.High, $"Facet.Export: Found dependency in probe directory at '{probe}'");
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(probe);
                    }
                }

                // Fallback: probe the task assembly directory (contains EFCore + InMemory via CopyLocalLockFileAssemblies)
                if (!string.IsNullOrWhiteSpace(taskAsmDir))
                {
                    var taskLocal = Path.Combine(taskAsmDir!, name.Name + ".dll");
                    if (File.Exists(taskLocal))
                    {
                        Log.LogMessage(MessageImportance.High, $"Facet.Export: Found dependency in task directory at '{taskLocal}'");
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(taskLocal);
                    }
                }

                // Special handling for Identity assemblies - try common NuGet locations
                if (name.Name?.Contains("Identity") == true || name.Name?.Contains("HealthChecks") == true)
                {
                    Log.LogMessage(MessageImportance.High, $"Facet.Export: Attempting special resolution for Identity/HealthChecks assembly '{name.Name}'");

                    var nugetPaths = new[]
                    {
                        // Try the runtime directory first
                        Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location) ?? "", name.Name + ".dll"),
                        // Try common NuGet package cache locations
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", name.Name.ToLowerInvariant(), "*", "lib", "net9.0", name.Name + ".dll"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", name.Name.ToLowerInvariant(), "*", "lib", "net8.0", name.Name + ".dll"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", name.Name.ToLowerInvariant(), "*", "lib", "netstandard2.0", name.Name + ".dll")
                    };

                    foreach (var nugetPath in nugetPaths)
                    {
                        try
                        {
                            // Handle wildcard paths for NuGet packages
                            if (nugetPath.Contains("*"))
                            {
                                var directory = Path.GetDirectoryName(nugetPath);
                                var pattern = Path.GetFileName(Path.GetDirectoryName(nugetPath));
                                var fileName = Path.GetFileName(nugetPath);

                                if (Directory.Exists(Path.GetDirectoryName(directory)))
                                {
                                    var matchingDirs = Directory.GetDirectories(Path.GetDirectoryName(directory), pattern);
                                    foreach (var matchingDir in matchingDirs.OrderByDescending(d => d)) // Get latest version
                                    {
                                        var fullPath = Path.Combine(matchingDir, Path.GetFileName(directory), fileName);
                                        if (File.Exists(fullPath))
                                        {
                                            Log.LogMessage(MessageImportance.High, $"Facet.Export: Found Identity/HealthChecks dependency at '{fullPath}'");
                                            return AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
                                        }
                                    }
                                }
                            }
                            else if (File.Exists(nugetPath))
                            {
                                Log.LogMessage(MessageImportance.High, $"Facet.Export: Found Identity/HealthChecks dependency at '{nugetPath}'");
                                return AssemblyLoadContext.Default.LoadFromAssemblyPath(nugetPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.LogMessage(MessageImportance.High, $"Facet.Export: Failed to load from '{nugetPath}': {ex.Message}");
                        }
                    }
                }

                Log.LogMessage(MessageImportance.High, $"Facet.Export: Could not resolve dependency '{name.FullName}' - exhausted all resolution paths");
                return null;
            }

            AssemblyLoadContext.Default.Resolving += ResolveHandler;
            try
            {
                var assembly = Assembly.LoadFrom(AssemblyPath);
                Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Loaded assembly '{assembly.FullName}'");

                Log.LogMessage(MessageImportance.Normal, $"Facet.Export: ContextTypes='{ContextTypes}'");
                var contextTypes = ResolveDbContextTypes(assembly, ContextTypes);
                var resolvedList = contextTypes.ToList();
                Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Resolved contexts ({resolvedList.Count}): {string.Join(", ", resolvedList.Select(t => t.FullName))}");
                if (resolvedList.Count == 0)
                {
                    Log.LogError("Facet.Export: No DbContext types were resolved. Aborting EF model export.");
                    return false;
                }

                var contexts = new List<object>();
                var failures = new List<string>();
                Log.LogMessage(MessageImportance.High, $"Facet.Export: About to iterate through {resolvedList.Count} contexts");
                foreach (var contextType in resolvedList)
                {
                try
                {
                    Log.LogMessage(MessageImportance.High, $"Facet.Export: Creating DbContext for '{contextType.FullName}'...");
                    object? ctx = null;
                    try
                    {
                        ctx = CreateDbContext(assembly, contextType, Provider);
                    }
                    catch (Exception createEx)
                    {
                        var createMsg = $"Exception creating DbContext {contextType.FullName}: {createEx.Message}";
                        Log.LogError($"Facet.Export: {createMsg}");
                        failures.Add(createMsg);
                        continue;
                    }

                    if (ctx == null)
                    {
                        var msg = $"Could not create instance of DbContext: {contextType.FullName}";
                        Log.LogError($"Facet.Export: {msg}");
                        failures.Add(msg);
                        continue;
                    }

                    try
                    {
                        Log.LogMessage(MessageImportance.High, $"Facet.Export: Accessing Model for '{contextType.FullName}'...");

                        // Use reflection to access Model property and cast to interface
                        // This avoids assembly loading context issues where DbContext types don't match
                        var modelProperty = ctx.GetType().GetProperty("Model");
                        if (modelProperty == null)
                        {
                            var msg = $"Context {contextType.FullName} does not have a Model property.";
                            Log.LogError($"Facet.Export: {msg}");
                            failures.Add(msg);
                            continue;
                        }

                        var modelValue = modelProperty.GetValue(ctx);
                        if (modelValue == null)
                        {
                            var msg = $"Context {contextType.FullName} returned null Model.";
                            Log.LogError($"Facet.Export: {msg}");
                            failures.Add(msg);
                            continue;
                        }

                        // Try to cast to IReadOnlyModel interface - this should work across assembly loading contexts
                        IReadOnlyModel? model = null;
                        try
                        {
                            model = modelValue as IReadOnlyModel;
                        }
                        catch (Exception castEx)
                        {
                            Log.LogMessage(MessageImportance.High, $"Facet.Export: Failed to cast Model to IReadOnlyModel: {castEx.Message}");
                        }

                        if (model == null)
                        {
                            // If direct cast fails, try using reflection to access the interface methods
                            Log.LogMessage(MessageImportance.High, $"Facet.Export: Direct cast to IReadOnlyModel failed, attempting reflection-based access...");
                            
                            // Check if the object implements the interface methods we need
                            var modelType = modelValue.GetType();
                            Log.LogMessage(MessageImportance.High, $"Facet.Export: Model type: {modelType.FullName}");
                            
                            // List all available methods for debugging
                            var allMethods = modelType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                            var entityMethods = allMethods.Where(m => m.Name.Contains("Entity")).ToArray();
                            Log.LogMessage(MessageImportance.High, $"Facet.Export: Available entity-related methods: {string.Join(", ", entityMethods.Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})"))}");
                            
                            var getEntityTypesMethod = modelType.GetMethod("GetEntityTypes", Type.EmptyTypes);
                            if (getEntityTypesMethod == null)
                            {
                                // Try alternative method signatures
                                getEntityTypesMethod = modelType.GetMethod("GetEntityTypes");
                                if (getEntityTypesMethod == null)
                                {
                                    // Try on interfaces
                                    foreach (var iface in modelType.GetInterfaces())
                                    {
                                        Log.LogMessage(MessageImportance.High, $"Facet.Export: Checking interface: {iface.FullName}");
                                        getEntityTypesMethod = iface.GetMethod("GetEntityTypes", Type.EmptyTypes);
                                        if (getEntityTypesMethod == null)
                                            getEntityTypesMethod = iface.GetMethod("GetEntityTypes");
                                        if (getEntityTypesMethod != null)
                                        {
                                            Log.LogMessage(MessageImportance.High, $"Facet.Export: Found GetEntityTypes on interface: {iface.FullName}");
                                            break;
                                        }
                                    }
                                }
                            }
                            
                            if (getEntityTypesMethod == null)
                            {
                                var msg = $"Context {contextType.FullName} Model (type: {modelType.FullName}) does not have GetEntityTypes method.";
                                Log.LogError($"Facet.Export: {msg}");
                                failures.Add(msg);
                                continue;
                            }

                            // Use reflection to call GetEntityTypes
                            var entityTypesResult = getEntityTypesMethod.Invoke(modelValue, null);
                            if (entityTypesResult == null)
                            {
                                var msg = $"Context {contextType.FullName} GetEntityTypes returned null.";
                                Log.LogError($"Facet.Export: {msg}");
                                failures.Add(msg);
                                continue;
                            }

                            // Process entities using reflection
                            var reflectionEntityList = new List<object>();
                            foreach (var entity in (System.Collections.IEnumerable)entityTypesResult)
                            {
                                try
                                {
                                    var entityType = entity.GetType();
                                    var nameProperty = entityType.GetProperty("Name");
                                    var clrTypeProperty = entityType.GetProperty("ClrType");
                                    var getKeysMethod = entityType.GetMethod("GetKeys", Type.EmptyTypes);
                                    var getNavigationsMethod = entityType.GetMethod("GetNavigations", Type.EmptyTypes);

                                    var entName = nameProperty?.GetValue(entity)?.ToString() ?? "Unknown";
                                    var clrType = clrTypeProperty?.GetValue(entity) as Type;
                                    var clrName = clrType?.FullName ?? entName;

                                    var keysList = new List<string[]>();
                                    if (getKeysMethod != null)
                                    {
                                        var keysResult = getKeysMethod.Invoke(entity, null);
                                        if (keysResult != null)
                                        {
                                            foreach (var key in (System.Collections.IEnumerable)keysResult)
                                            {
                                                var propertiesProperty = key.GetType().GetProperty("Properties");
                                                if (propertiesProperty != null)
                                                {
                                                    var properties = propertiesProperty.GetValue(key);
                                                    if (properties != null)
                                                    {
                                                        var names = new List<string>();
                                                        foreach (var prop in (System.Collections.IEnumerable)properties)
                                                        {
                                                            var propNameProperty = prop.GetType().GetProperty("Name");
                                                            var propName = propNameProperty?.GetValue(prop)?.ToString();
                                                            if (!string.IsNullOrEmpty(propName))
                                                                names.Add(propName);
                                                        }
                                                        keysList.Add(names.ToArray());
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    var navs = new List<object>();
                                    if (getNavigationsMethod != null)
                                    {
                                        var navsResult = getNavigationsMethod.Invoke(entity, null);
                                        if (navsResult != null)
                                        {
                                            foreach (var nav in (System.Collections.IEnumerable)navsResult)
                                            {
                                                var navType = nav.GetType();
                                                var navNameProperty = navType.GetProperty("Name");
                                                var targetEntityTypeProperty = navType.GetProperty("TargetEntityType");
                                                var isCollectionProperty = navType.GetProperty("IsCollection");

                                                var navName = navNameProperty?.GetValue(nav)?.ToString() ?? "Unknown";
                                                var isCollection = isCollectionProperty?.GetValue(nav) as bool? ?? false;
                                                
                                                var targetName = "Unknown";
                                                if (targetEntityTypeProperty != null)
                                                {
                                                    var targetEntityType = targetEntityTypeProperty.GetValue(nav);
                                                    if (targetEntityType != null)
                                                    {
                                                        var targetClrTypeProperty = targetEntityType.GetType().GetProperty("ClrType");
                                                        var targetNameProperty = targetEntityType.GetType().GetProperty("Name");
                                                        
                                                        var targetClrType = targetClrTypeProperty?.GetValue(targetEntityType) as Type;
                                                        targetName = targetClrType?.FullName ?? targetNameProperty?.GetValue(targetEntityType)?.ToString() ?? "Unknown";
                                                    }
                                                }

                                                navs.Add(new { Name = navName, Target = targetName, IsCollection = isCollection });
                                            }
                                        }
                                    }

                                    reflectionEntityList.Add(new { Name = entName, Clr = clrName, Keys = keysList.ToArray(), Navigations = navs.ToArray() });
                                }
                                catch (Exception entityEx)
                                {
                                    Log.LogMessage(MessageImportance.High, $"Facet.Export: Error processing entity via reflection: {entityEx.Message}");
                                }
                            }

                            var reflectionContextData = new { Context = contextType.FullName ?? contextType.Name, Entities = reflectionEntityList.ToArray() };
                            contexts.Add(reflectionContextData);
                            Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Exported EF model for context via reflection: {contextType.FullName} (Entities={reflectionEntityList.Count})");
                            continue;
                        }

                        var entityList = new List<object>();

                        // Direct interface usage - no reflection needed
                        foreach (var entity in model.GetEntityTypes())
                        {
                            var entName = entity.Name;
                            var clrName = entity.ClrType?.FullName ?? entName;

                            var keysList = new List<string[]>();
                            foreach (var key in entity.GetKeys())
                            {
                                var names = key.Properties.Select(p => p.Name).ToArray();
                                keysList.Add(names);
                            }

                            var navs = new List<object>();
                            foreach (var nav in entity.GetNavigations())
                            {
                                var targetName = nav.TargetEntityType.ClrType?.FullName ?? nav.TargetEntityType.Name;
                                navs.Add(new { Name = nav.Name, Target = targetName, IsCollection = nav.IsCollection });
                            }

                            entityList.Add(new { Name = entName, Clr = clrName, Keys = keysList.ToArray(), Navigations = navs.ToArray() });
                        }

                        var contextData = new { Context = contextType.FullName ?? contextType.Name, Entities = entityList.ToArray() };
                        contexts.Add(contextData);
                        Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Exported EF model for context: {contextType.FullName} (Entities={entityList.Count})");
                    }
                    finally
                    {
                        if (ctx is IDisposable d) d.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    var details = GetExceptionDetails(ex);
                    Log.LogError($"Facet.Export: Failed to export model for context {contextType.FullName}: {details}");
                    failures.Add($"{contextType.FullName}: {details}");
                }
                }

                var rootModel = new { Contexts = contexts };
            var json = JsonSerializer.Serialize(rootModel, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var outputFullPath = Path.GetFullPath(OutputPath);
            var outputDir = Path.GetDirectoryName(outputFullPath);
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                throw new ArgumentException($"Invalid OutputPath: {OutputPath}");
            }
            Directory.CreateDirectory(outputDir!);
            File.WriteAllText(outputFullPath, json);

            if (failures.Count > 0 || contexts.Count == 0)
            {
                Log.LogError($"Facet.Export: EF model export failed. Contexts exported={contexts.Count}. Failures=\n - {string.Join("\n - ", failures)}");
                return false;
            }

            Log.LogMessage(MessageImportance.Normal, $"Facet.Export: EF model exported to: {outputFullPath} (Contexts={contexts.Count})");
            return true;
            }
            finally
            {
                AssemblyLoadContext.Default.Resolving -= ResolveHandler;
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to export EF model: {ex}");
            return false;
        }
    }

    private IEnumerable<Type> ResolveDbContextTypes(Assembly assembly, string? contextTypeNames)
    {
        Log.LogMessage(MessageImportance.High, $"Facet.Export: ResolveDbContextTypes called with contextTypeNames='{contextTypeNames}'");

        // Always get all types first to avoid Assembly.GetType() dependency issues
        Log.LogMessage(MessageImportance.High, $"Facet.Export: Getting all types from assembly '{assembly.FullName}'...");
        var allTypes = GetLoadableTypes(assembly);
        Log.LogMessage(MessageImportance.High, $"Facet.Export: GetLoadableTypes returned {allTypes.Count()} types");

        // Log all discovered types for debugging, specifically looking for ImmybotDbContext
        Log.LogMessage(MessageImportance.High, $"Facet.Export: All discovered types:");
        var immybotDbContextFound = false;
        foreach (var type in allTypes.Take(50)) // Limit to first 50 to avoid log spam
        {
            Log.LogMessage(MessageImportance.High, $"Facet.Export:   - {type.FullName} (Namespace: {type.Namespace}, IsAbstract: {type.IsAbstract})");
            if (string.Equals(type.FullName, "Immybot.Backend.Persistence.ImmybotDbContext", StringComparison.OrdinalIgnoreCase))
            {
                immybotDbContextFound = true;
                Log.LogMessage(MessageImportance.High, $"Facet.Export:   *** FOUND ImmybotDbContext in type enumeration! ***");
            }
        }
        if (allTypes.Count() > 50)
        {
            Log.LogMessage(MessageImportance.High, $"Facet.Export:   ... and {allTypes.Count() - 50} more types");
            // Check remaining types for ImmybotDbContext
            foreach (var type in allTypes.Skip(50))
            {
                if (string.Equals(type.FullName, "Immybot.Backend.Persistence.ImmybotDbContext", StringComparison.OrdinalIgnoreCase))
                {
                    immybotDbContextFound = true;
                    Log.LogMessage(MessageImportance.High, $"Facet.Export:   *** FOUND ImmybotDbContext in remaining types! ***");
                    break;
                }
            }
        }

        if (!immybotDbContextFound)
        {
            Log.LogMessage(MessageImportance.High, $"Facet.Export: *** ImmybotDbContext was NOT found in type enumeration - this is the root cause! ***");
        }

        // Find all DbContext types in the assembly
        var dbContextTypes = new List<Type>();
        foreach (var type in allTypes)
        {
            try
            {
                if (!type.IsAbstract && IsDbContextByName(type))
                {
                    dbContextTypes.Add(type);
                    Log.LogMessage(MessageImportance.High, $"Facet.Export: Found DbContext type: '{type.FullName}'");
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.High, $"Facet.Export: Exception checking type '{type.FullName}': {ex.Message}");
            }
        }

        Log.LogMessage(MessageImportance.High, $"Facet.Export: Found {dbContextTypes.Count} total DbContext types: [{string.Join(", ", dbContextTypes.Select(t => t.FullName))}]");

        if (!string.IsNullOrWhiteSpace(contextTypeNames))
        {
            var requestedNames = contextTypeNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .ToArray();

            Log.LogMessage(MessageImportance.High, $"Facet.Export: Requested names: [{string.Join(", ", requestedNames)}]");

            // Match requested names against found DbContext types
            var matches = new List<Type>();
            foreach (var requestedName in requestedNames)
            {
                Log.LogMessage(MessageImportance.High, $"Facet.Export: Looking for requested type '{requestedName}'...");

                var matchedType = dbContextTypes.FirstOrDefault(t =>
                    string.Equals(t.FullName, requestedName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.Name, requestedName, StringComparison.OrdinalIgnoreCase));

                if (matchedType != null)
                {
                    Log.LogMessage(MessageImportance.High, $"Facet.Export: Matched '{requestedName}' to '{matchedType.FullName}'");
                    matches.Add(matchedType);

                    // Log detailed analysis
                    var baseName = matchedType.BaseType?.FullName ?? "<null>";
                    var isAbstract = matchedType.IsAbstract;
                    var nameBasedDbContext = IsDbContextByName(matchedType);

                    bool assignableDefault = false;
                    try
                    {
                        assignableDefault = typeof(DbContext).IsAssignableFrom(matchedType);
                    }
                    catch (Exception ex)
                    {
                        Log.LogMessage(MessageImportance.High, $"Facet.Export: Exception checking IsAssignableFrom for '{matchedType.FullName}': {ex.Message}");
                    }

                    Log.LogMessage(MessageImportance.High, $"Facet.Export: Type '{matchedType.FullName}' analysis: BaseType='{baseName}', IsAbstract={isAbstract}, NameBasedDbContext={nameBasedDbContext}, AssignableToDefaultDbContext={assignableDefault}");
                }
                else
                {
                    Log.LogMessage(MessageImportance.High, $"Facet.Export: Could not find requested type '{requestedName}' among available DbContext types");
                }
            }

            Log.LogMessage(MessageImportance.High, $"Facet.Export: Matched {matches.Count} requested types");
            return matches;
        }

        // No explicit names provided: return all DbContext types
        Log.LogMessage(MessageImportance.High, $"Facet.Export: No explicit names provided, returning all {dbContextTypes.Count} DbContext types");
        return dbContextTypes;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    private bool IsDbContextByName(Type type)
    {
        try
        {
            // Special case: if this is the ImmybotDbContext type we're looking for, check it more thoroughly
            if (string.Equals(type.FullName, "Immybot.Backend.Persistence.ImmybotDbContext", StringComparison.OrdinalIgnoreCase))
            {
                Log.LogMessage(MessageImportance.High, $"Facet.Export: *** Found ImmybotDbContext type! Checking inheritance chain... ***");
            }

            Log.LogMessage(MessageImportance.High, $"Facet.Export: IsDbContextByName checking type '{type.FullName}'");

            var current = type;
            var depth = 0;
            while (current != null && depth < 10) // Prevent infinite loops
            {
                Log.LogMessage(MessageImportance.High, $"Facet.Export:   Depth {depth}: Checking '{current.FullName}' (Name: '{current.Name}', Namespace: '{current.Namespace}')");

                if (string.Equals(current.FullName, "Microsoft.EntityFrameworkCore.DbContext", StringComparison.Ordinal))
                {
                    Log.LogMessage(MessageImportance.High, $"Facet.Export:   Found DbContext by FullName match at depth {depth}");
                    return true;
                }
                if (string.Equals(current.Name, "DbContext", StringComparison.Ordinal)
                    && string.Equals(current.Namespace, "Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
                {
                    Log.LogMessage(MessageImportance.High, $"Facet.Export:   Found DbContext by Name+Namespace match at depth {depth}");
                    return true;
                }

                // Also check for IdentityDbContext which is a common base for DbContext
                if (current.Name.Contains("IdentityDbContext") &&
                    (current.Namespace?.Contains("Microsoft.AspNetCore.Identity.EntityFrameworkCore") == true ||
                     current.Namespace?.Contains("Microsoft.EntityFrameworkCore") == true))
                {
                    Log.LogMessage(MessageImportance.High, $"Facet.Export:   Found IdentityDbContext (which inherits from DbContext) at depth {depth}");
                    return true;
                }

                try
                {
                    current = current.BaseType;
                    Log.LogMessage(MessageImportance.High, $"Facet.Export:   Moving to base type: {current?.FullName ?? "null"}");
                }
                catch (Exception ex)
                {
                    Log.LogMessage(MessageImportance.High, $"Facet.Export:   Exception getting BaseType at depth {depth}: {ex.Message}");

                    // If we can't load the base type but this looks like a DbContext by name, assume it is
                    if (type.Name.Contains("DbContext") || type.FullName?.Contains("DbContext") == true)
                    {
                        Log.LogMessage(MessageImportance.High, $"Facet.Export:   Type name contains 'DbContext', assuming it's a DbContext despite BaseType loading failure");
                        return true;
                    }
                    break;
                }
                depth++;
            }

            Log.LogMessage(MessageImportance.High, $"Facet.Export:   No DbContext found in inheritance chain for '{type.FullName}' (checked {depth} levels)");
        }
        catch (Exception ex)
        {
            Log.LogMessage(MessageImportance.High, $"Facet.Export: Exception in IsDbContextByName for '{type.FullName}': {ex.Message}");

            // If we can't check the inheritance chain but this looks like a DbContext by name, assume it is
            if (type.Name.Contains("DbContext") || type.FullName?.Contains("DbContext") == true)
            {
                Log.LogMessage(MessageImportance.High, $"Facet.Export: Type name contains 'DbContext', assuming it's a DbContext despite exception");
                return true;
            }
        }
        return false;
    }

    private static string GetExceptionDetails(Exception ex)
    {
        string Render(Exception e)
        {
            if (e is TargetInvocationException tie && tie.InnerException != null)
                return Render(tie.InnerException);
            if (e is AggregateException agg)
            {
                var inners = agg.Flatten().InnerExceptions.Select(Render);
                return $"{e.Message}\n - {string.Join("\n - ", inners)}";
            }
            if (e is ReflectionTypeLoadException rtle)
            {
                var loader = rtle.LoaderExceptions?.Select(Render) ?? Enumerable.Empty<string>();
                return $"{e}\nLoaderExceptions:\n - {string.Join("\n - ", loader)}";
            }
            return e.ToString();
        }

        return Render(ex);
    }

    private object? CreateDbContext(Assembly assembly, Type contextType, string? providerHint)
    {
        try { Log.LogMessage(MessageImportance.High, $"Facet.Export: CreateDbContext called for {contextType.FullName} with provider hint: {providerHint ?? "none"}"); } catch { }
        var alc = AssemblyLoadContext.GetLoadContext(assembly);
        // 1) Try IDesignTimeDbContextFactory<TContext>
        try
        {
            var designAsm = alc != null ? alc.LoadFromAssemblyName(new AssemblyName("Microsoft.EntityFrameworkCore.Design")) : null;
            var genDef = designAsm?.GetType("Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory`1")
                        ?? Type.GetType("Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory`1");
            if (genDef != null)
            {
                var factoryInterfaceType = genDef.MakeGenericType(contextType);
                var factoryType = GetLoadableTypes(assembly).FirstOrDefault(t => !t.IsAbstract && factoryInterfaceType.IsAssignableFrom(t));
                if (factoryType != null)
                {
                    try
                    {
                        var factory = CreateInstance(factoryType);
                        var createMethod = factoryInterfaceType.GetMethod("CreateDbContext");
                        var created = createMethod?.Invoke(factory, new object[] { Array.Empty<string>() });
                        if (created != null)
                        {
                            try { Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Using design-time factory '{factoryType.FullName}' for {contextType.FullName}"); } catch { }
                            return created;
                        }
                    }
                    catch { /* fall through */ }
                }
            }
        }
        catch { /* fall through */ }

        // 1b) Convention fallback: any type with CreateDbContext(string[]) instance method
        try
        {
            var altFactories = GetLoadableTypes(assembly)
                .Where(t => !t.IsAbstract && t.GetMethod("CreateDbContext", new[] { typeof(string[]) }) != null)
                .OrderByDescending(t => string.Equals(t.Name, contextType.Name + "DesignFactory", StringComparison.OrdinalIgnoreCase)
                                          || t.Name.Contains(contextType.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var altFactory in altFactories)
            {
                var instance = CreateInstance(altFactory);
                var m = altFactory.GetMethod("CreateDbContext", new[] { typeof(string[]) });
                var created = m?.Invoke(instance, new object[] { Array.Empty<string>() });
                if (created != null && contextType.IsInstanceOfType(created))
                {
                    try { Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Using convention design factory '{altFactory.FullName}' for {contextType.FullName}"); } catch { }
                    return created;
                }
            }
        }
        catch { /* fall through */ }

        // 2) Try with provider options based on hint or defaults
        var preferInMemory = string.Equals(providerHint, "inmemory", StringComparison.OrdinalIgnoreCase);
        var preferNpgsql = string.Equals(providerHint, "npgsql", StringComparison.OrdinalIgnoreCase) || string.Equals(providerHint, "postgres", StringComparison.OrdinalIgnoreCase);

        // Try preferred provider first
        if (preferInMemory)
        {
            if (TryCreateWithInMemory(alc, contextType, out var created) && created != null)
            {
                try { Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Using InMemory provider for {contextType.FullName}"); } catch { }
                return created;
            }
        }
        else if (preferNpgsql)
        {
            if (TryCreateWithNpgsql(alc, contextType, out var created) && created != null)
            {
                try { Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Using Npgsql provider for {contextType.FullName}"); } catch { }
                return created;
            }
        }

        // Try default fallback order: InMemory first, then Npgsql
        if (!preferInMemory && TryCreateWithInMemory(alc, contextType, out var inmemCreated) && inmemCreated != null)
        {
            try { Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Using InMemory provider (fallback) for {contextType.FullName}"); } catch { }
            return inmemCreated;
        }

        if (!preferNpgsql && TryCreateWithNpgsql(alc, contextType, out var npgsqlCreated) && npgsqlCreated != null)
        {
            try { Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Using Npgsql provider (fallback) for {contextType.FullName}"); } catch { }
            return npgsqlCreated;
        }

        // 4) Give up – return null rather than a providerless context to avoid opaque runtime failures
        try { Log.LogMessage(MessageImportance.High, $"Facet.Export: Failed to create DbContext for {contextType.FullName} - all provider attempts failed"); } catch { }
        return null;
    }

    private static bool TryCreateWithInMemory(AssemblyLoadContext? alc, Type contextType, out object? created)
    {
        created = null;
        try
        {
            if (alc == null) return false;
            var efAsm = alc.LoadFromAssemblyName(new AssemblyName("Microsoft.EntityFrameworkCore"));
            var inMemAsm = alc.LoadFromAssemblyName(new AssemblyName("Microsoft.EntityFrameworkCore.InMemory"));
            var extensionsType = inMemAsm?.GetType("Microsoft.EntityFrameworkCore.InMemoryDbContextOptionsExtensions");
            var genericBuilderOpen = efAsm.GetType("Microsoft.EntityFrameworkCore.DbContextOptionsBuilder`1");
            if (genericBuilderOpen != null)
            {
                var typedBuilderType = genericBuilderOpen.MakeGenericType(contextType);
                var typedBuilder = CreateInstance(typedBuilderType);
                if (typedBuilder != null && extensionsType != null)
                {
                    // Find the provider method
                    var useInMemoryMethod = extensionsType.GetMethods()
                        .FirstOrDefault(m => m.Name == "UseInMemoryDatabase" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2);

                    if (useInMemoryMethod != null)
                    {
                        var closed = useInMemoryMethod.MakeGenericMethod(contextType);
                        closed.Invoke(null, new object[] { typedBuilder, "FacetDesignTime" });
                        var optionsProp = typedBuilderType.GetProperty("Options");
                        var options = optionsProp?.GetValue(typedBuilder);
                        if (options != null)
                        {
                            created = CreateInstance(contextType, options);
                            return created != null;
                        }
                    }
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateWithNpgsql(AssemblyLoadContext? alc, Type contextType, out object? created)
    {
        created = null;
        try
        {
            if (alc == null) return false;
            var efAsm = alc.LoadFromAssemblyName(new AssemblyName("Microsoft.EntityFrameworkCore"));
            Assembly? npgsqlAsm = null;
            try { npgsqlAsm = alc.LoadFromAssemblyName(new AssemblyName("Aspire.Npgsql.EntityFrameworkCore.PostgreSQL")); } catch { }
            if (npgsqlAsm == null) { try { npgsqlAsm = alc.LoadFromAssemblyName(new AssemblyName("Npgsql.EntityFrameworkCore.PostgreSQL")); } catch { } }
            if (npgsqlAsm == null) return false;

            var builderOpen = efAsm.GetType("Microsoft.EntityFrameworkCore.DbContextOptionsBuilder`1");
            if (builderOpen != null)
            {
                var typedBuilderType = builderOpen.MakeGenericType(contextType);
                var typedBuilder = CreateInstance(typedBuilderType);
                if (typedBuilder != null)
                {
                    // Find the provider method
                    var candidates = npgsqlAsm.GetTypes()
                        .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public))
                        .Where(m => m.Name == "UseNpgsql")
                        .ToList();
                    var methodGen = candidates.FirstOrDefault(m => m.IsGenericMethodDefinition && m.GetParameters().Length >= 2);

                    if (methodGen != null)
                    {
                        var cs = "Host=localhost;Database=FacetDesignTime;Username=facet;Password=facet";
                        var closed = methodGen.MakeGenericMethod(contextType);
                        closed.Invoke(null, new object[] { typedBuilder, cs });
                        var optionsProp = typedBuilderType.GetProperty("Options");
                        var options = optionsProp?.GetValue(typedBuilder);
                        if (options != null)
                        {
                            created = CreateInstance(contextType, options);
                            return created != null;
                        }
                    }
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }



    // Helper method to create instances using compiled delegates
    private static object CreateInstance(Type type, params object[] args)
    {
        if (args.Length == 0)
        {
            // Cache parameterless constructor
            var factory = ConstructorCache.GetOrAdd(type, t =>
            {
                var ctor = t.GetConstructor(Type.EmptyTypes);
                if (ctor == null) throw new InvalidOperationException($"No parameterless constructor found for {t}");

                var newExpr = Expression.New(ctor);
                var lambda = Expression.Lambda<Func<object, object>>(Expression.Convert(newExpr, typeof(object)), Expression.Parameter(typeof(object), "_"));
                return lambda.Compile();
            });
            return factory(null!);
        }

        // For all other cases, just use Activator.CreateInstance
        // The optimization for single-arg constructors was causing issues with DbContext options
        return Activator.CreateInstance(type, args)!;
    }
}
