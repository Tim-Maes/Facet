using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Facet.Extensions.EFCore.Tasks;

/// <summary>
/// MSBuild task that exports EF Core model metadata to JSON for source generation.
/// </summary>
public sealed class ExportEfModelTask : Task
{
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
            foreach (var contextType in resolvedList)
            {
                try
                {
                    Log.LogMessage(MessageImportance.High, $"Facet.Export: Creating DbContext for '{contextType.FullName}'...");
                    var ctx = CreateDbContext(assembly, contextType, Provider);
                    if (ctx == null)
                    {
                        var msg = $"Could not create instance of DbContext: {contextType.FullName}";
                        Log.LogError($"Facet.Export: {msg}");
                        failures.Add(msg);
                        continue;
                    }

                    try
                    {
                        var modelProp = ctx.GetType().GetProperty("Model", BindingFlags.Public | BindingFlags.Instance);
                        if (modelProp == null)
                        {
                            var msg = $"Context {contextType.FullName} does not expose a Model property.";
                            Log.LogError($"Facet.Export: {msg}");
                            failures.Add(msg);
                            continue;
                        }

                        Log.LogMessage(MessageImportance.High, $"Facet.Export: Accessing Model for '{contextType.FullName}'...");
                        var model = modelProp.GetValue(ctx);
                        if (model == null)
                        {
                            var msg = $"Context {contextType.FullName} returned null Model.";
                            Log.LogError($"Facet.Export: {msg}");
                            failures.Add(msg);
                            continue;
                        }

                        var entityList = new List<object>();
                        // Prefer interface methods; RuntimeModel implements IReadOnlyModel/IModel explicitly
                        var modelIface = model.GetType().GetInterface("Microsoft.EntityFrameworkCore.Metadata.IReadOnlyModel")
                                         ?? model.GetType().GetInterface("Microsoft.EntityFrameworkCore.Metadata.IModel");
                        var getEntitiesMi = modelIface?.GetMethod("GetEntityTypes", BindingFlags.Public | BindingFlags.Instance);
                        var entitiesEnumerable = getEntitiesMi?.Invoke(model, null) as System.Collections.IEnumerable;
                        if (entitiesEnumerable != null)
                        {
                            foreach (var entity in entitiesEnumerable)
                            {
                                var entType = entity.GetType();
                                var entName = entType.GetProperty("Name")?.GetValue(entity) as string ?? "<unknown>";
                                var clrType = entType.GetProperty("ClrType")?.GetValue(entity) as Type;
                                var clrName = clrType?.FullName ?? entName;

                                var keysList = new List<string[]>();
                                var entityIface = entType.GetInterface("Microsoft.EntityFrameworkCore.Metadata.IReadOnlyEntityType")
                                               ?? entType.GetInterface("Microsoft.EntityFrameworkCore.Metadata.IEntityType");
                                var getKeysMi = entityIface?.GetMethod("GetKeys", BindingFlags.Public | BindingFlags.Instance);
                                var keysEnum = getKeysMi?.Invoke(entity, null) as System.Collections.IEnumerable;
                                if (keysEnum != null)
                                {
                                    foreach (var key in keysEnum)
                                    {
                                        var propsProp = key.GetType().GetProperty("Properties");
                                        var propsEnum = propsProp?.GetValue(key) as System.Collections.IEnumerable;
                                        var names = new List<string>();
                                        if (propsEnum != null)
                                        {
                                            foreach (var prop in propsEnum)
                                            {
                                                var pName = prop.GetType().GetProperty("Name")?.GetValue(prop) as string;
                                                if (!string.IsNullOrEmpty(pName)) names.Add(pName!);
                                            }
                                        }
                                        keysList.Add(names.ToArray());
                                    }
                                }

                                var navs = new List<object>();
                                var getNavsMi = entityIface?.GetMethod("GetNavigations", BindingFlags.Public | BindingFlags.Instance);
                                var navsEnum = getNavsMi?.Invoke(entity, null) as System.Collections.IEnumerable;
                                if (navsEnum != null)
                                {
                                    foreach (var nav in navsEnum)
                                    {
                                        var nType = nav.GetType();
                                        var nName = nType.GetProperty("Name")?.GetValue(nav) as string ?? "<nav>";
                                        var isCollection = nType.GetProperty("IsCollection")?.GetValue(nav) as bool? ?? false;
                                        var targetEntityType = nType.GetProperty("TargetEntityType")?.GetValue(nav);
                                        var targetClr = targetEntityType?.GetType().GetProperty("ClrType")?.GetValue(targetEntityType) as Type;
                                        var targetName = targetClr?.FullName ?? targetEntityType?.GetType().GetProperty("Name")?.GetValue(targetEntityType) as string ?? "<unknown>";
                                        navs.Add(new { Name = nName, Target = targetName, IsCollection = isCollection });
                                    }
                                }

                                entityList.Add(new { Name = entName, Clr = clrName, Keys = keysList.ToArray(), Navigations = navs.ToArray() });
                            }
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
                        var factory = Activator.CreateInstance(factoryType);
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
                var instance = Activator.CreateInstance(altFactory);
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

        // 2) Try with InMemoryDatabase options using the same ALC as the context assembly (preferred over parameterless)
        var preferInMemory = string.Equals(providerHint, "inmemory", StringComparison.OrdinalIgnoreCase);
        var preferNpgsql = string.Equals(providerHint, "npgsql", StringComparison.OrdinalIgnoreCase) || string.Equals(providerHint, "postgres", StringComparison.OrdinalIgnoreCase);

        bool triedInMemory = false;
        bool triedNpgsql = false;

        if (preferInMemory)
        {
            var r = TryCreateWithInMemory(alc, contextType, out var created);
            triedInMemory = true;
            if (r && created != null) return created;
        }

        try
        {
            if (alc != null)
            {
                var efAsm = alc.LoadFromAssemblyName(new AssemblyName("Microsoft.EntityFrameworkCore"));
                var inMemAsm = alc.LoadFromAssemblyName(new AssemblyName("Microsoft.EntityFrameworkCore.InMemory"));
                var extensionsType = inMemAsm?.GetType("Microsoft.EntityFrameworkCore.InMemoryDbContextOptionsExtensions");

                // Try generic builder first to get strongly-typed DbContextOptions<TContext>
                var genericBuilderOpen = efAsm.GetType("Microsoft.EntityFrameworkCore.DbContextOptionsBuilder`1");
                if (genericBuilderOpen != null)
                {
                    var typedBuilderType = genericBuilderOpen.MakeGenericType(contextType);
                    var typedBuilder = Activator.CreateInstance(typedBuilderType);
                    if (typedBuilder != null && extensionsType != null)
                    {
                        // Try generic overload: UseInMemoryDatabase<TContext>(DbContextOptionsBuilder<TContext>, string)
                        var useInMemoryGeneric = extensionsType.GetMethods()
                            .FirstOrDefault(m => m.Name == "UseInMemoryDatabase" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2);
                        if (useInMemoryGeneric != null)
                        {
                            var closed = useInMemoryGeneric.MakeGenericMethod(contextType);
                            closed.Invoke(null, new object[] { typedBuilder, "FacetDesignTime" });
                            var optionsProp = typedBuilderType.GetProperty("Options");
                            var options = optionsProp?.GetValue(typedBuilder);
                            if (options != null)
                            {
                                try { Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Using InMemory provider (generic builder) for {contextType.FullName}"); } catch { }
                                return Activator.CreateInstance(contextType, options);
                            }
                        }

                        // Fallback: non-generic overload with typed builder instance (assignable to base builder)
                        var builderBaseType = efAsm.GetType("Microsoft.EntityFrameworkCore.DbContextOptionsBuilder");
                        var useInMemoryNonGeneric = extensionsType.GetMethods()
                            .FirstOrDefault(m => m.Name == "UseInMemoryDatabase"
                                                 && !m.IsGenericMethodDefinition
                                                 && m.GetParameters().Length == 2
                                                 && m.GetParameters()[0].ParameterType == builderBaseType);
                        if (useInMemoryNonGeneric != null && builderBaseType != null && builderBaseType.IsAssignableFrom(typedBuilderType))
                        {
                            useInMemoryNonGeneric.Invoke(null, new object[] { typedBuilder, "FacetDesignTime" });
                            var optionsProp = typedBuilderType.GetProperty("Options");
                            var options = optionsProp?.GetValue(typedBuilder);
                            if (options != null)
                            {
                            try { Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Using InMemory provider (non-generic ext + typed builder) for {contextType.FullName}"); } catch { }
                                return Activator.CreateInstance(contextType, options);
                            }
                        }
                    }
                }

                // Fallback: non-generic builder path
                var builderType = efAsm.GetType("Microsoft.EntityFrameworkCore.DbContextOptionsBuilder");
                var builder = builderType != null ? Activator.CreateInstance(builderType) : null;
                if (builderType != null && builder != null && extensionsType != null)
                {
                    var useInMemoryMethod = extensionsType.GetMethods()
                        .FirstOrDefault(m => m.Name == "UseInMemoryDatabase"
                                             && !m.IsGenericMethodDefinition
                                             && m.GetParameters().Length == 2
                                             && m.GetParameters()[0].ParameterType == builderType);

                    if (useInMemoryMethod != null)
                    {
                        useInMemoryMethod.Invoke(null, new object[] { builder, "FacetDesignTime" });
                        var optionsProperty = builderType.GetProperty("Options");
                        var options = optionsProperty?.GetValue(builder);
                        if (options != null)
                        {
                            try { Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Using InMemory provider (non-generic builder) for {contextType.FullName}"); } catch { }
                            return Activator.CreateInstance(contextType, options);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            try { Log.LogMessage(MessageImportance.Low, $"Facet.Export: InMemory options path failed for {contextType.FullName}: {ex.Message}"); } catch { }
            // Final fallback failed
        }

        // 3) Try Npgsql provider if present (model building does not require a live connection)
        if (preferNpgsql)
        {
            var rn = TryCreateWithNpgsql(alc, contextType, out var created);
            triedNpgsql = true;
            if (rn && created != null) return created;
        }
        try
        {
            if (alc != null)
            {
                var efAsm = alc.LoadFromAssemblyName(new AssemblyName("Microsoft.EntityFrameworkCore"));
                // Load any Npgsql EFCore provider assembly name variant (Aspire or standard)
                Assembly? npgsqlAsm = null;
                try { npgsqlAsm = alc.LoadFromAssemblyName(new AssemblyName("Aspire.Npgsql.EntityFrameworkCore.PostgreSQL")); } catch { }
                if (npgsqlAsm == null) { try { npgsqlAsm = alc.LoadFromAssemblyName(new AssemblyName("Npgsql.EntityFrameworkCore.PostgreSQL")); } catch { } }
                if (npgsqlAsm != null)
                {
                    // Find a static method named 'UseNpgsql' with first parameter DbContextOptionsBuilder or generic variant
                    var builderOpen = efAsm.GetType("Microsoft.EntityFrameworkCore.DbContextOptionsBuilder`1");
                    var nonGenericBuilderType = efAsm.GetType("Microsoft.EntityFrameworkCore.DbContextOptionsBuilder");
                    var candidates = npgsqlAsm.GetTypes()
                        .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public))
                        .Where(m => m.Name == "UseNpgsql")
                        .ToList();

                    // Prefer generic builder
                    if (builderOpen != null)
                    {
                        var typedBuilderType = builderOpen.MakeGenericType(contextType);
                        var typedBuilder = Activator.CreateInstance(typedBuilderType);
                        if (typedBuilder != null)
                        {
                            // dummy connection string; provider just needs to be wired for model building
                            var cs = "Host=localhost;Database=FacetDesignTime;Username=facet;Password=facet";
                            // Try generic extension first
                            var methodGen = candidates.FirstOrDefault(m => m.IsGenericMethodDefinition && m.GetParameters().Length >= 2);
                            if (methodGen != null)
                            {
                                var closed = methodGen.MakeGenericMethod(contextType);
                                closed.Invoke(null, new object[] { typedBuilder, cs });
                            }
                            else if (nonGenericBuilderType != null)
                            {
                                // Try non-generic overload with base builder parameter
                                var methodNg = candidates.FirstOrDefault(m => !m.IsGenericMethodDefinition && m.GetParameters().Length >= 2 && m.GetParameters()[0].ParameterType == nonGenericBuilderType);
                                if (methodNg != null)
                                {
                                    methodNg.Invoke(null, new object[] { typedBuilder, cs });
                                }
                            }
                            var optionsProp = typedBuilderType.GetProperty("Options");
                            var options = optionsProp?.GetValue(typedBuilder);
                            if (options != null)
                            {
                                try { Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Using Npgsql provider (generic/typed builder) for {contextType.FullName}"); } catch { }
                                return Activator.CreateInstance(contextType, options);
                            }
                        }
                    }

                    // Fallback: non-generic builder
                    if (nonGenericBuilderType != null)
                    {
                        var builder = Activator.CreateInstance(nonGenericBuilderType);
                        var method = candidates.FirstOrDefault(m => !m.IsGenericMethodDefinition && m.GetParameters().Length >= 2 && m.GetParameters()[0].ParameterType == nonGenericBuilderType);
                        if (builder != null && method != null)
                        {
                            var cs = "Host=localhost;Database=FacetDesignTime;Username=facet;Password=facet";
                            method.Invoke(null, new object[] { builder, cs });
                            var optionsProp = nonGenericBuilderType.GetProperty("Options");
                            var options = optionsProp?.GetValue(builder);
                            if (options != null)
                            {
                                try { Log.LogMessage(MessageImportance.Normal, $"Facet.Export: Using Npgsql provider (non-generic builder) for {contextType.FullName}"); } catch { }
                                return Activator.CreateInstance(contextType, options);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            try { Log.LogMessage(MessageImportance.Low, $"Facet.Export: Npgsql options path failed for {contextType.FullName}: {ex.Message}"); } catch { }
        }

        // 4) Give up – return null rather than a providerless context to avoid opaque runtime failures
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
                var typedBuilder = Activator.CreateInstance(typedBuilderType);
                if (typedBuilder != null && extensionsType != null)
                {
                    var useInMemoryGeneric = extensionsType.GetMethods()
                        .FirstOrDefault(m => m.Name == "UseInMemoryDatabase" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2);
                    if (useInMemoryGeneric != null)
                    {
                        var closed = useInMemoryGeneric.MakeGenericMethod(contextType);
                        closed.Invoke(null, new object[] { typedBuilder, "FacetDesignTime" });
                        var optionsProp = typedBuilderType.GetProperty("Options");
                        var options = optionsProp?.GetValue(typedBuilder);
                        if (options != null)
                        {
                            created = Activator.CreateInstance(contextType, options);
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
                var typedBuilder = Activator.CreateInstance(typedBuilderType);
                if (typedBuilder != null)
                {
                    var candidates = npgsqlAsm.GetTypes()
                        .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public))
                        .Where(m => m.Name == "UseNpgsql")
                        .ToList();
                    var cs = "Host=localhost;Database=FacetDesignTime;Username=facet;Password=facet";
                    var methodGen = candidates.FirstOrDefault(m => m.IsGenericMethodDefinition && m.GetParameters().Length >= 2);
                    if (methodGen != null)
                    {
                        var closed = methodGen.MakeGenericMethod(contextType);
                        closed.Invoke(null, new object[] { typedBuilder, cs });
                        var optionsProp = typedBuilderType.GetProperty("Options");
                        var options = optionsProp?.GetValue(typedBuilder);
                        if (options != null)
                        {
                            created = Activator.CreateInstance(contextType, options);
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

    private static MethodInfo? FindExtensionMethod(string name, params string[] firstParamTypeFullNames)
    {
        static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException rtle) { return rtle.Types.Where(t => t != null)!; }
            catch { return Array.Empty<Type>(); }
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in SafeGetTypes(asm))
            {
                if (!(type.IsSealed && type.IsAbstract)) continue; // static classes only
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!string.Equals(m.Name, name, StringComparison.Ordinal)) continue;
                    var pars = m.GetParameters();
                    if (pars.Length >= 1)
                    {
                        var p0 = pars[0].ParameterType;
                        var p0Name = p0.FullName ?? p0.Name;
                        if (firstParamTypeFullNames.Any(n => string.Equals(n, p0Name, StringComparison.Ordinal)))
                            return m;
                    }
                }
            }
        }
        return null;
    }

    private static MethodInfo? FindExtensionMethod(string name, Type firstParamType)
    {
        static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException rtle) { return rtle.Types.Where(t => t != null)!; }
            catch { return Array.Empty<Type>(); }
        }
        var asm = firstParamType.Assembly;
        foreach (var type in SafeGetTypes(asm))
        {
            if (!(type.IsSealed && type.IsAbstract)) continue; // static classes only
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (!string.Equals(m.Name, name, StringComparison.Ordinal)) continue;
                var pars = m.GetParameters();
                if (pars.Length >= 1 && pars[0].ParameterType == firstParamType)
                    return m;
            }
        }
        return null;
    }

    
}
