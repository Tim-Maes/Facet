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
                var path = resolver.ResolveAssemblyToPath(name);
                if (path != null && File.Exists(path))
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);

                // Fallback: probe next to the context assembly
                var local = Path.Combine(ctxDir, name.Name + ".dll");
                if (File.Exists(local))
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(local);

                // Fallback: probe an optional directory with richer transitive outputs
                if (!string.IsNullOrWhiteSpace(ProbeDirectory))
                {
                    var probe = Path.Combine(ProbeDirectory!, name.Name + ".dll");
                    if (!Path.IsPathFullyQualified(probe))
                    {
                        try { probe = Path.GetFullPath(probe); } catch { }
                    }
                    if (File.Exists(probe))
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(probe);
                }

                // Fallback: probe the task assembly directory (contains EFCore + InMemory via CopyLocalLockFileAssemblies)
                if (!string.IsNullOrWhiteSpace(taskAsmDir))
                {
                    var taskLocal = Path.Combine(taskAsmDir!, name.Name + ".dll");
                    if (File.Exists(taskLocal))
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(taskLocal);
                }

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
                        
                        // Direct cast to DbContext and access Model property
                        var dbContext = ctx as DbContext;
                        if (dbContext == null)
                        {
                            var msg = $"Context {contextType.FullName} is not a DbContext.";
                            Log.LogError($"Facet.Export: {msg}");
                            failures.Add(msg);
                            continue;
                        }

                        var model = dbContext.Model as IReadOnlyModel;
                        if (model == null)
                        {
                            var msg = $"Context {contextType.FullName} returned null Model.";
                            Log.LogError($"Facet.Export: {msg}");
                            failures.Add(msg);
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
        if (!string.IsNullOrWhiteSpace(contextTypeNames))
        {
            var requestedNames = contextTypeNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .ToArray();

            // Try to resolve by full name first to avoid scanning all types
            var resolved = new List<Type>();
            foreach (var name in requestedNames)
            {
                var type = assembly.GetType(name, throwOnError: false, ignoreCase: false)
                           ?? assembly.GetType(name, throwOnError: false, ignoreCase: true);

                if (type == null)
                {
                    Log.LogMessage(MessageImportance.High, $"Facet.Export: Could not resolve type by name '{name}' in assembly '{assembly.FullName}'.");
                    continue;
                }

                var baseName = type.BaseType?.FullName ?? "<null>";
                var isAbstract = type.IsAbstract;
                var nameBasedDbContext = IsDbContextByName(type);
                var assignableDefault = typeof(DbContext).IsAssignableFrom(type);
                Log.LogMessage(MessageImportance.High, $"Facet.Export: Candidate '{type.FullName}' BaseType='{baseName}', IsAbstract={isAbstract}, NameBasedDbContext={nameBasedDbContext}, AssignableToDefaultDbContext={assignableDefault}");

                // Accept explicit type if it's not abstract and looks like a DbContext by name-based inheritance
                if (!isAbstract && nameBasedDbContext)
                    resolved.Add(type);
            }

            if (resolved.Count > 0)
                return resolved;

            // Fallback: scan for matches by simple name if full name resolution failed
            var allTypes = GetLoadableTypes(assembly);
            var matches = allTypes.Where(t => !t.IsAbstract && IsDbContextByName(t))
                                  .Where(t => requestedNames.Any(r => string.Equals(r, t.Name, StringComparison.OrdinalIgnoreCase)
                                                            || string.Equals(r, t.FullName, StringComparison.OrdinalIgnoreCase)))
                                  .ToList();
            foreach (var m in matches)
            {
                Log.LogMessage(MessageImportance.High, $"Facet.Export: Fallback matched '{m.FullName}'.");
            }
            return matches;
        }

        // No explicit names provided: scan for all DbContext types (may require additional dependencies loaded)
        return GetLoadableTypes(assembly)
            .Where(type => !type.IsAbstract && IsDbContextByName(type))
            .ToList();
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

    private static bool IsDbContextByName(Type type)
    {
        try
        {
            var current = type;
            while (current != null)
            {
                if (string.Equals(current.FullName, "Microsoft.EntityFrameworkCore.DbContext", StringComparison.Ordinal))
                    return true;
                if (string.Equals(current.Name, "DbContext", StringComparison.Ordinal)
                    && string.Equals(current.Namespace, "Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
                    return true;
                current = current.BaseType;
            }
        }
        catch
        {
            // ignore type load issues, treat as not a DbContext
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
