using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Facet.Extensions;

/// <summary>
/// Discovers and caches projection expressions from FacetMap-generated marker classes.
/// This enables <see cref="FacetExtensions.SelectFacet{TTarget}(System.Linq.IQueryable)"/> and
/// <see cref="FacetExtensions.SelectFacet{TSource, TTarget}(System.Linq.IQueryable{TSource})"/>
/// to discover projections for FacetMap targets (plain POCOs that don't have [Facet] attributes).
/// </summary>
internal static class FacetProjectionRegistry
{
    // Cache of discovered projections: (SourceType, TargetType) -> LambdaExpression
    private static readonly ConcurrentDictionary<(Type SourceType, Type TargetType), LambdaExpression?> _projections = new();

    // Track which assemblies have been scanned
    private static readonly ConcurrentDictionary<Assembly, bool> _scannedAssemblies = new();

    /// <summary>
    /// Attempts to find a FacetMap-registered projection for the given source and target types.
    /// Scans relevant assemblies for [FacetMap] marker classes with matching projection properties.
    /// </summary>
    public static bool TryGetProjection(Type sourceType, Type targetType, out LambdaExpression? projection)
    {
        var key = (sourceType, targetType);
        if (_projections.TryGetValue(key, out projection))
            return projection != null;

        // Scan all currently loaded assemblies that could contain FacetMap marker classes.
        // We always do a full scan because marker classes can live in ANY assembly
        // (not just the source or target assembly - commonly in a separate "Mapper" project).
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            ScanAssembly(assembly);
        }

        if (_projections.TryGetValue(key, out projection))
            return projection != null;

        // If not found, the mapper assembly might not be loaded yet (.NET loads assemblies lazily).
        // Try to force-load assemblies referenced by loaded assemblies that reference Facet.
        // This handles the common pattern where:
        //   API project -> references Mapper project -> has [FacetMap]
        //   but Mapper assembly isn't loaded until first code access
        LoadFacetReferencingAssemblies();

        // Scan any newly loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            ScanAssembly(assembly);
        }

        if (_projections.TryGetValue(key, out projection))
            return projection != null;

        // Not found - don't cache the miss because the assembly might load later.
        // The performance cost of re-scanning is minimal due to _scannedAssemblies tracking.
        projection = null;
        return false;
    }

    /// <summary>
    /// Attempts to find a FacetMap-registered projection for the given target type and source element type.
    /// </summary>
    public static bool TryGetProjectionForTarget(Type targetType, Type sourceElementType, out LambdaExpression? projection)
    {
        return TryGetProjection(sourceElementType, targetType, out projection);
    }

    /// <summary>
    /// Explicitly registers a projection expression for the given source-to-target mapping.
    /// Can be called by user code to register custom projections for use with SelectFacet.
    /// </summary>
    public static void Register<TSource, TTarget>(Expression<Func<TSource, TTarget>> projection)
    {
        _projections[(typeof(TSource), typeof(TTarget))] = projection;
    }

    private static void ScanAssembly(Assembly assembly)
    {
        if (!_scannedAssemblies.TryAdd(assembly, true))
            return; // Already scanned

        try
        {
            // Skip dynamic assemblies
            if (assembly.IsDynamic)
                return;

            // Skip known framework assemblies that can't contain FacetMap attributes
            var assemblyName = assembly.GetName().Name;
            if (assemblyName != null && IsFrameworkAssembly(assemblyName))
                return;

            // Check if this assembly references Facet.Attributes (required to have [FacetMap])
            if (!ReferencesFacetAttributes(assembly))
                return;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Use whatever types we could load
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            foreach (var type in types)
            {
                if (type == null) continue;

                // Static classes are abstract + sealed in IL
                if (!type.IsAbstract || !type.IsSealed)
                    continue;

                ScanTypeForFacetMapProjections(type);
            }
        }
        catch
        {
            // Silently ignore assembly scanning failures
        }
    }

    private static void ScanTypeForFacetMapProjections(Type type)
    {
        try
        {
            // Check if this type has [FacetMap] attributes
            var attributes = type.GetCustomAttributesData();
            foreach (var attr in attributes)
            {
                if (attr.AttributeType.FullName != "Facet.FacetMapAttribute")
                    continue;

                if (attr.ConstructorArguments.Count < 2)
                    continue;

                var attrSourceType = attr.ConstructorArguments[0].Value as Type;
                var attrTargetType = attr.ConstructorArguments[1].Value as Type;

                if (attrSourceType == null || attrTargetType == null)
                    continue;

                // Check if GenerateProjection is explicitly set to false via named arguments
                bool generateProjection = true;
                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.MemberName == "GenerateProjection" && namedArg.TypedValue.Value is bool gp)
                    {
                        generateProjection = gp;
                        break;
                    }
                }

                if (!generateProjection)
                    continue;

                // Look for the projection property: {SourceSimpleName}To{TargetSimpleName}Projection
                var projectionPropertyName = $"{attrSourceType.Name}To{attrTargetType.Name}Projection";
                var prop = type.GetProperty(projectionPropertyName, BindingFlags.Public | BindingFlags.Static);

                if (prop == null)
                    continue;

                try
                {
                    var value = prop.GetValue(null);
                    if (value is LambdaExpression lambda)
                    {
                        _projections[(attrSourceType, attrTargetType)] = lambda;
                    }
                }
                catch
                {
                    // Skip properties that fail to evaluate (e.g., dependency not available)
                }
            }
        }
        catch
        {
            // Skip types that fail during attribute or property inspection
        }
    }

    private static bool ReferencesFacetAttributes(Assembly assembly)
    {
        try
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies();
            foreach (var reference in referencedAssemblies)
            {
                var name = reference.Name;
                if (name == null) continue;
                // Check for Facet.Attributes (normal NuGet/ProjectReference)
                // or Facet (direct reference in development/testing)
                if (name == "Facet.Attributes" || name == "Facet"
                    || name.StartsWith("Facet.Attributes,", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
        catch
        {
            // If we can't check references, scan it anyway to be safe
            return true;
        }
    }

    /// <summary>
    /// Loads any not-yet-loaded assemblies that are referenced by currently loaded assemblies
    /// and that might contain FacetMap marker classes. This handles the lazy assembly loading scenario
    /// where the mapper assembly hasn't been loaded yet because no code from it has been accessed.
    /// </summary>
    private static void LoadFacetReferencingAssemblies()
    {
        try
        {
            var loadedNames = new HashSet<string>(
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .Select(a => a.GetName().Name ?? ""),
                StringComparer.Ordinal);

            // For each loaded assembly, try to load its references that aren't loaded yet.
            // Focus on assemblies that reference Facet.Attributes (they might have [FacetMap] marker classes).
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .ToArray();

            foreach (var assembly in assemblies)
            {
                try
                {
                    foreach (var reference in assembly.GetReferencedAssemblies())
                    {
                        if (reference.Name == null || loadedNames.Contains(reference.Name))
                            continue;

                        if (IsFrameworkAssembly(reference.Name))
                            continue;

                        try
                        {
                            var loaded = Assembly.Load(reference);
                            loadedNames.Add(reference.Name);

                            // Only recursively load if this assembly references Facet.Attributes
                            if (ReferencesFacetAttributes(loaded))
                            {
                                // This assembly might be the mapper assembly - it's now loaded
                                // and will be scanned in the next ScanAssembly pass
                            }
                        }
                        catch
                        {
                            // Ignore load failures - the assembly might not be available
                        }
                    }
                }
                catch
                {
                    // Ignore per-assembly failures
                }
            }
        }
        catch
        {
            // Ignore failures
        }
    }

    private static bool IsFrameworkAssembly(string name)
    {
        return name.StartsWith("System", StringComparison.Ordinal)
            || name.StartsWith("Microsoft", StringComparison.Ordinal)
            || name.StartsWith("netstandard", StringComparison.Ordinal)
            || name.StartsWith("mscorlib", StringComparison.Ordinal)
            || name.StartsWith("WindowsBase", StringComparison.Ordinal)
            || name.StartsWith("PresentationCore", StringComparison.Ordinal)
            || name.StartsWith("PresentationFramework", StringComparison.Ordinal)
            || name.StartsWith("testhost", StringComparison.Ordinal)
            || name.StartsWith("xunit", StringComparison.Ordinal)
            || name.StartsWith("NuGet", StringComparison.Ordinal)
            || name.StartsWith("Newtonsoft", StringComparison.Ordinal)
            || name == "Facet.Attributes"
            || name == "Facet.Extensions"
            || name == "Facet.Extensions.EFCore"
            || name == "Facet.Extensions.EFCore.Mapping"
            || name == "Facet.Mapping"
            || name == "Facet.Mapping.Expressions";
    }
}
