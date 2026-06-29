using System;
using System.Collections.Concurrent;
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

    // Whether a full AppDomain scan has been performed
    private static volatile bool _fullScanDone;

    /// <summary>
    /// Attempts to find a FacetMap-registered projection for the given source and target types.
    /// Scans relevant assemblies for [FacetMap] marker classes with matching projection properties.
    /// </summary>
    public static bool TryGetProjection(Type sourceType, Type targetType, out LambdaExpression? projection)
    {
        var key = (sourceType, targetType);
        if (_projections.TryGetValue(key, out projection))
            return projection != null;

        // Try targeted scan first (source and target assemblies)
        ScanAssembly(targetType.Assembly);
        if (_projections.TryGetValue(key, out projection))
            return projection != null;

        ScanAssembly(sourceType.Assembly);
        if (_projections.TryGetValue(key, out projection))
            return projection != null;

        // Full scan of all loaded assemblies as last resort
        if (!_fullScanDone)
        {
            _fullScanDone = true;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                ScanAssembly(assembly);
            }

            if (_projections.TryGetValue(key, out projection))
                return projection != null;
        }

        // Cache the miss so we don't scan again for this pair
        _projections.TryAdd(key, null);
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
            // Skip known framework assemblies that can't contain FacetMap attributes
            var assemblyName = assembly.GetName().Name;
            if (assemblyName != null && (
                assemblyName.StartsWith("System", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Microsoft", StringComparison.Ordinal) ||
                assemblyName.StartsWith("netstandard", StringComparison.Ordinal) ||
                assemblyName.StartsWith("mscorlib", StringComparison.Ordinal)))
            {
                return;
            }

            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                // Static classes are abstract + sealed in IL
                if (!type.IsAbstract || !type.IsSealed)
                    continue;

                // Check if this type has [FacetMap] attributes
                CustomAttributeData[]? facetMapAttrs = null;
                try
                {
                    facetMapAttrs = type.GetCustomAttributesData()
                        .Where(a => a.AttributeType.FullName == "Facet.FacetMapAttribute")
                        .ToArray();
                }
                catch
                {
                    continue; // Skip types that fail to load attributes
                }

                if (facetMapAttrs.Length == 0)
                    continue;

                // For each FacetMap attribute, find and cache the projection property
                foreach (var attr in facetMapAttrs)
                {
                    if (attr.ConstructorArguments.Count < 2)
                        continue;

                    var attrSourceType = attr.ConstructorArguments[0].Value as Type;
                    var attrTargetType = attr.ConstructorArguments[1].Value as Type;

                    if (attrSourceType == null || attrTargetType == null)
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
                        // Skip properties that fail to evaluate
                    }
                }
            }
        }
        catch
        {
            // Silently ignore assembly scanning failures (e.g., ReflectionTypeLoadException)
        }
    }
}
