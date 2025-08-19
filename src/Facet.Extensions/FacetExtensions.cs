using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Facet.Extensions;
/// <summary>
/// Provides extension methods for mapping source entities or sequences
/// to Facet-generated types (synchronous and provider-agnostic only).
/// </summary>
public static class FacetExtensions
{
    // Maps facet target type to declared source type from [Facet(typeof(...))].
    private static readonly ConcurrentDictionary<Type, Type> _declaredSourceTypeByTarget = new();

    // Cached MethodInfo for ToFacet<TSource, TTarget>(TSource)
    private static readonly MethodInfo _toFacetTwoGenericMethod =
        typeof(FacetExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m =>
            {
                if (m.Name != nameof(ToFacet)) return false;
                var ga = m.GetGenericArguments();
                if (ga.Length != 2) return false;
                var ps = m.GetParameters();
                return ps.Length == 1;
            });
    
    /// <summary>
    /// Maps a single source instance to the specified facet type by invoking its generated constructor.
    /// If the constructor fails (e.g., due to required init-only properties), attempts to use a static FromSource factory method.
    /// </summary>
    /// <typeparam name="TSource">The source entity type.</typeparam>
    /// <typeparam name="TTarget">The facet type, which must have a public constructor accepting <c>TSource</c> or a static FromSource method.</typeparam>
    /// <param name="source">The source instance to map.</param>
    /// <returns>A new <typeparamref name="TTarget"/> instance populated from <paramref name="source"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <c>null</c>.</exception>
    public static TTarget ToFacet<TSource, TTarget>(this TSource source)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        
        // Check for static FromSource factory method first (preferred for init-only properties)
        var fromSourceMethod = typeof(TTarget).GetMethod(
            "FromSource",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(TSource) },
            null);
            
        if (fromSourceMethod != null)
        {
            return (TTarget)fromSourceMethod.Invoke(null, new object[] { source })!;
        }
        
        // Fall back to constructor
        try
        {
            return (TTarget)Activator.CreateInstance(typeof(TTarget), source)!;
        }
        catch
        {
            // If neither works, provide a helpful error message
            throw new InvalidOperationException(
                $"Unable to map {typeof(TSource).Name} to {typeof(TTarget).Name}. " +
                $"Ensure {typeof(TTarget).Name} has either a constructor accepting {typeof(TSource).Name} " +
                $"or a static FromSource({typeof(TSource).Name}) method.");
        }
    }

    /// <summary>
    /// Converts the specified source object to an instance of the target type annotated as a facet.
    /// </summary>
    /// <typeparam name="TTarget">The target type to which the source object will be converted. Must be a reference type and annotated with
    /// <c>[Facet(typeof(...))]</c>.</typeparam>
    /// <param name="source">The source object to be converted. Cannot be <see langword="null"/>.</param>
    /// <returns>An instance of the target type <typeparamref name="TTarget"/> created from the source object.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if: <list type="bullet"> <item><description>The target type <typeparamref name="TTarget"/> is not
    /// annotated with <c>[Facet(typeof(...))]</c>.</description></item> <item><description>The source object's type is
    /// not assignable to the declared source type for the target facet.</description></item> <item><description>The
    /// conversion process fails due to a missing constructor or static <c>FromSource</c> method.</description></item>
    /// </list></exception>
    public static TTarget ToFacet<TTarget>(this object source)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var targetType = typeof(TTarget);

        var declaredSource = GetDeclaredSourceType(targetType)
            ?? throw new InvalidOperationException(
                $"Type '{targetType.FullName}' must be annotated with [Facet(typeof(...))] to use ToFacet<{targetType.Name}>().");

        if (!declaredSource.IsInstanceOfType(source))
        {
            throw new InvalidOperationException(
                $"Source instance type '{source.GetType().FullName}' is not assignable to declared Facet source '{declaredSource.FullName}' for target '{targetType.FullName}'.");
        }

        var forwarded = _toFacetTwoGenericMethod.MakeGenericMethod(declaredSource, targetType)
                                         .Invoke(null, new[] { source });
        if (forwarded is null)
        {
            throw new InvalidOperationException(
                $"Unable to map source '{declaredSource.FullName}' to '{targetType.FullName}'. Ensure a matching constructor or static FromSource exists.");
        }

        return (TTarget)forwarded;
    }

    /// <summary>
    /// Maps an <see cref="IEnumerable{TSource}"/> to an <see cref="IEnumerable{TTarget}"/>
    /// via the generated constructor of the facet type.
    /// </summary>
    /// <typeparam name="TSource">The source entity type.</typeparam>
    /// <typeparam name="TTarget">The facet type, which must have a public constructor accepting <c>TSource</c>.</typeparam>
    /// <param name="source">The enumerable source of entities.</param>
    /// <returns>An <see cref="IEnumerable{TTarget}"/> containing mapped facet instances.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <c>null</c>.</exception>
    public static IEnumerable<TTarget> SelectFacets<TSource, TTarget>(this IEnumerable<TSource> source)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return source.Select(item => item.ToFacet<TSource, TTarget>());
    }

    /// <summary>
    /// Projects an <see cref="IQueryable{TSource}"/> to an <see cref="IQueryable{TTarget}"/>
    /// using the static <c>Expression&lt;Func&lt;TSource,TTarget&gt;&gt;</c> named <c>Projection</c> defined on <typeparamref name="TTarget"/>.
    /// </summary>
    /// <typeparam name="TSource">The source entity type.</typeparam>
    /// <typeparam name="TTarget">The facet type, which must define a public static <c>Expression&lt;Func&lt;TSource,TTarget&gt;&gt; Projection</c>.</typeparam>
    /// <param name="source">The queryable source of entities.</param>
    /// <returns>An <see cref="IQueryable{TTarget}"/> representing the projection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TTarget"/> does not define a static <c>Projection</c> property.
    /// </exception>
    public static IQueryable<TTarget> SelectFacet<TSource, TTarget>(this IQueryable<TSource> source)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var prop = typeof(TTarget).GetProperty(
            "Projection",
            BindingFlags.Public | BindingFlags.Static);

        if (prop is null)
            throw new InvalidOperationException(
                $"Type {typeof(TTarget).Name} must define a public static Projection property.");

        var expr = (Expression<Func<TSource, TTarget>>)prop.GetValue(null)!;
        return source.Select(expr);
    }

    private static Type? GetDeclaredSourceType(Type targetType)
    {
        if (_declaredSourceTypeByTarget.TryGetValue(targetType, out var cached))
            return cached;

        var attr = targetType
            .GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == "Facet.FacetAttribute");

        var declared = attr?.ConstructorArguments.Count > 0
                       && attr.ConstructorArguments[0].ArgumentType == typeof(Type)
                       ? attr.ConstructorArguments[0].Value as Type
                       : null;

        if (declared != null)
        {
            _declaredSourceTypeByTarget[targetType] = declared;
        }

        return declared;
    }
}
