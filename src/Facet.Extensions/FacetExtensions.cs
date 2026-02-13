using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    // For a facet target type TTarget, cache the [Facet(typeof(TSource))] declared source type (TSource).
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

    // Cached MethodInfo for ToSource<TFacet, TFacetSource>(TFacet)
    private static readonly MethodInfo _toFacetSourceTwoGenericMethod =
        typeof(FacetExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m =>
            {
                if (m.Name != nameof(ToSource)) return false;
                var ga = m.GetGenericArguments();
                if (ga.Length != 2) return false;
                var ps = m.GetParameters();
                return ps.Length == 1;
            });

    // Cached Expression<Func<DeclaredSource, TTarget>> from TTarget.Projection.
    private static readonly ConcurrentDictionary<Type, LambdaExpression> _declaredProjectionByTarget = new();

    // Cache of adapted Expression<Func<TElement, TTarget>> shapes per (element, target).
    private static readonly ConcurrentDictionary<(Type ElementType, Type TargetType), LambdaExpression>
        _adaptedProjectionByElementAndTarget = new();

    /// <summary>
    /// Maps a single source instance to the specified facet type by invoking its generated constructor.
    /// If the constructor fails (e.g., due to required init-only properties), attempts to use a static FromSource factory method.
    /// </summary>
    /// <typeparam name="TSource">The source entity type.</typeparam>
    /// <typeparam name="TTarget">The facet type, which must have a public constructor accepting <c>TSource</c> or a static FromSource method.</typeparam>
    /// <param name="source">The source instance to map.</param>
    /// <returns>A new <typeparamref name="TTarget"/> instance populated from <paramref name="source"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <c>null</c>.</exception>
    public static TTarget ToFacet<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>(this TSource source)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));    
        return FacetCache<TSource, TTarget>.Mapper(source);
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
    [RequiresUnreferencedCode("This method uses MakeGenericMethod which is not compatible with trimming. Use the strongly-typed ToFacet<TSource, TTarget> overload instead.")]
    [RequiresDynamicCode("This method uses MakeGenericMethod which requires dynamic code generation. Use the strongly-typed ToFacet<TSource, TTarget> overload instead.")]
    public static TTarget ToFacet<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>(this object source)
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
    /// Maps a single facet instance to the specified source type by invoking its generated ToSource method.
    /// </summary>
    /// <typeparam name="TFacet">The facet type that is annotated with [Facet(typeof(TFacetSource))].</typeparam>
    /// <typeparam name="TFacetSource">The entity type to map to.</typeparam>
    /// <param name="facet">The facet instance to map.</param>
    /// <returns>A new <typeparamref name="TFacetSource"/> instance populated from <paramref name="facet"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facet"/> is <c>null</c>.</exception>
    public static TFacetSource ToSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TFacet, TFacetSource>(this TFacet facet)
        where TFacet : class
        where TFacetSource : class
    {
        if (facet is null) throw new ArgumentNullException(nameof(facet));
        return FacetSourceCache<TFacet, TFacetSource>.Mapper(facet);
    }

    /// <summary>
    /// Maps a single facet instance to the specified source type by invoking its generated ToSource method.
    /// </summary>
    /// <typeparam name="TFacet">The facet type that is annotated with [Facet(typeof(TFacetSource))].</typeparam>
    /// <typeparam name="TFacetSource">The entity type to map to.</typeparam>
    /// <param name="facet">The facet instance to map.</param>
    /// <returns>A new <typeparamref name="TFacetSource"/> instance populated from <paramref name="facet"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facet"/> is <c>null</c>.</exception>
    [Obsolete("Use ToSource instead. This method will be removed in a future version.")]
    public static TFacetSource BackTo<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TFacet, TFacetSource>(this TFacet facet)
        where TFacet : class
        where TFacetSource : class
        => ToSource<TFacet, TFacetSource>(facet);

    /// <summary>
    /// Converts the specified facet object to an instance of the source type that the facet was created from.
    /// </summary>
    /// <typeparam name="TFacetSource">The source type to which the facet object will be converted. Must be a reference type.</typeparam>
    /// <param name="facet">The facet object to be converted. Cannot be <see langword="null"/>.</param>
    /// <returns>An instance of the source type <typeparamref name="TFacetSource"/> created from the facet object.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="facet"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if: <list type="bullet"> <item><description>The facet type is not
    /// annotated with <c>[Facet(typeof(...))]</c>.</description></item> <item><description>The target type does not match
    /// the declared source type for the facet.</description></item> <item><description>The
    /// conversion process fails due to a missing ToSource method on the facet.</description></item>
    /// </list></exception>
    [RequiresUnreferencedCode("This method uses MakeGenericMethod which is not compatible with trimming. Use the strongly-typed ToSource<TFacet, TFacetSource> overload instead.")]
    [RequiresDynamicCode("This method uses MakeGenericMethod which requires dynamic code generation. Use the strongly-typed ToSource<TFacet, TFacetSource> overload instead.")]
    public static TFacetSource ToSource<TFacetSource>(this object facet)
        where TFacetSource : class
    {
        if (facet is null) throw new ArgumentNullException(nameof(facet));

        var facetType = facet.GetType();
        var declaredSource = GetDeclaredSourceType(facetType)
            ?? throw new InvalidOperationException(
                $"Type '{facetType.FullName}' must be annotated with [Facet(typeof(...))] to use ToSource<{typeof(TFacetSource).Name}>().");

        if (declaredSource != typeof(TFacetSource))
        {
            throw new InvalidOperationException(
                $"Target type '{typeof(TFacetSource).FullName}' does not match declared Facet source '{declaredSource.FullName}' for facet '{facetType.FullName}'.");
        }

        var forwarded = _toFacetSourceTwoGenericMethod.MakeGenericMethod(facetType, typeof(TFacetSource))
                                         .Invoke(null, new[] { facet });
        if (forwarded is null)
        {
            throw new InvalidOperationException(
                $"Unable to map facet '{facetType.FullName}' to '{typeof(TFacetSource).FullName}'. Ensure the facet has a generated ToSource method.");
        }

        return (TFacetSource)forwarded;
    }

    /// <summary>
    /// Converts the specified facet object to an instance of the source type that the facet was created from.
    /// </summary>
    /// <typeparam name="TFacetSource">The source type to which the facet object will be converted. Must be a reference type.</typeparam>
    /// <param name="facet">The facet object to be converted. Cannot be <see langword="null"/>.</param>
    /// <returns>An instance of the source type <typeparamref name="TFacetSource"/> created from the facet object.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="facet"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if: <list type="bullet"> <item><description>The facet type is not
    /// annotated with <c>[Facet(typeof(...))]</c>.</description></item> <item><description>The target type does not match
    /// the declared source type for the facet.</description></item> <item><description>The
    /// conversion process fails due to a missing ToSource method on the facet.</description></item>
    /// </list></exception>
    [Obsolete("Use ToSource instead. This method will be removed in a future version.")]
    [RequiresUnreferencedCode("This method uses MakeGenericMethod which is not compatible with trimming. Use the strongly-typed ToSource<TFacet, TFacetSource> overload instead.")]
    [RequiresDynamicCode("This method uses MakeGenericMethod which requires dynamic code generation. Use the strongly-typed ToSource<TFacet, TFacetSource> overload instead.")]
    public static TFacetSource BackTo<TFacetSource>(this object facet)
        where TFacetSource : class
        => ToSource<TFacetSource>(facet);

    /// <summary>
    /// Maps an <see cref="IEnumerable{TSource}"/> to an <see cref="IEnumerable{TTarget}"/>
    /// via the generated constructor of the facet type.
    /// </summary>
    /// <typeparam name="TSource">The source entity type.</typeparam>
    /// <typeparam name="TTarget">The facet type, which must have a public constructor accepting <c>TSource</c>.</typeparam>
    /// <param name="source">The enumerable source of entities.</param>
    /// <returns>An <see cref="IEnumerable{TTarget}"/> containing mapped facet instances.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <c>null</c>.</exception>
    public static IEnumerable<TTarget> SelectFacets<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>(this IEnumerable<TSource> source)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var mapper = FacetCache<TSource, TTarget>.Mapper;

        if (source is ICollection<TSource> collection)
        {
            var result = new List<TTarget>(collection.Count);
            foreach (var item in source)
            {
                result.Add(mapper(item));
            }
            return result;
        }

        var list = new List<TTarget>();
        foreach (var item in source)
        {
            list.Add(mapper(item));
        }
        return list;
    }

    /// <summary>
    /// Maps an <see cref="IEnumerable{TFacet}"/> to an <see cref="IEnumerable{TFacetSource}"/>
    /// via the generated ToSource method of the facet type.
    /// </summary>
    /// <typeparam name="TFacet">The facet type, which must be annotated with [Facet(typeof(TFacetSource))].</typeparam>
    /// <typeparam name="TFacetSource">The facet source type.</typeparam>
    /// <param name="facets">The source collection of facets.</param>
    /// <returns>An <see cref="IEnumerable{TFacetSource}"/> mapped from the input.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facets"/> is <c>null</c>.</exception>
    public static IEnumerable<TFacetSource> SelectFacetSources<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TFacet, TFacetSource>(this IEnumerable<TFacet> facets)
        where TFacet : class
        where TFacetSource : class
    {
        if (facets is null) throw new ArgumentNullException(nameof(facets));

        var mapper = FacetSourceCache<TFacet, TFacetSource>.Mapper;

        if (facets is ICollection<TFacet> collection)
        {
            var result = new List<TFacetSource>(collection.Count);
            foreach (var facet in facets)
            {
                result.Add(mapper(facet));
            }
            return result;
        }

        var list = new List<TFacetSource>();
        foreach (var facet in facets)
        {
            list.Add(mapper(facet));
        }
        return list;
    }
    
    /// <summary>
    /// Maps an <see cref="IEnumerable"/> of facet objects to an <see cref="IEnumerable{TFacetSource}"/>
    /// via the generated ToSource method of each facet type.
    /// </summary>
    /// <remarks>
    /// This method lazily converts each non-null facet object by calling <see cref="ToSource{TFacetSource}(object)"/> on each element.
    /// Only non-null elements are processed; nulls are skipped. The operation uses deferred execution and
    /// preserves the order of the source sequence.
    /// <para>
    /// Note: Each facet object must be annotated with <c>[Facet(typeof(TFacetSource))]</c> and have a generated ToSource method.
    /// If a facet type is not properly annotated or lacks the required ToSource method, the underlying
    /// <see cref="ToSource{TFacetSource}(object)"/> may throw <see cref="InvalidOperationException"/> at iteration time.
    /// </para>
    /// </remarks>
    /// <typeparam name="TFacetSource">The facet source type to map back to. Must be a reference type.</typeparam>
    /// <param name="facets">The source collection of facet objects. Cannot be <see langword="null"/>.</param>
    /// <returns>An <see cref="IEnumerable{TFacetSource}"/> containing source instances mapped from the non-null facet objects in the input collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facets"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown at iteration time if any facet object is not annotated with <c>[Facet(typeof(TFacetSource))]</c> or lacks a generated ToSource method.
    /// </exception>
    [RequiresUnreferencedCode("This method uses MakeGenericMethod which is not compatible with trimming. Use the strongly-typed SelectFacetSources<TFacet, TFacetSource> overload instead.")]
    [RequiresDynamicCode("This method uses MakeGenericMethod which requires dynamic code generation. Use the strongly-typed SelectFacetSources<TFacet, TFacetSource> overload instead.")]
    public static IEnumerable<TFacetSource> SelectFacetSources<TFacetSource>(this IEnumerable facets)
        where TFacetSource : class
    {
        if (facets is null) throw new ArgumentNullException(nameof(facets));
        foreach (var item in facets)
        {
            if (item is null) continue;
            yield return item.ToSource<TFacetSource>();
        }
    }

    /// <summary>
    /// Projects each non-null element of the source sequence into <typeparamref name="TTarget"/>.
    /// </summary>
    /// <remarks>
    /// This method lazily converts items by calling <see cref="ToFacet{TTarget}(object)"/> on each element.
    /// Only non-null elements are processed; nulls are skipped. The operation uses deferred execution and
    /// preserves the order of the source sequence.
    /// <para>
    /// Note: <typeparamref name="TTarget"/> must be a Facet-generated type (annotated with <c>[Facet]</c>).
    /// If the target type is not annotated or lacks a matching constructor/factory, the underlying
    /// <see cref="ToFacet{TTarget}(object)"/> may throw <see cref="InvalidOperationException"/> at iteration time.
    /// </para>
    /// </remarks>
    /// <typeparam name="TTarget">
    /// The facet target type to project to (reference type).
    /// </typeparam>
    /// <param name="source">The source sequence. Cannot be <see langword="null"/>.</param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> of <typeparamref name="TTarget"/> created from the non-null elements
    /// of <paramref name="source"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    [RequiresUnreferencedCode("This method uses MakeGenericMethod which is not compatible with trimming. Use the strongly-typed SelectFacets<TSource, TTarget> overload instead.")]
    [RequiresDynamicCode("This method uses MakeGenericMethod which requires dynamic code generation. Use the strongly-typed SelectFacets<TSource, TTarget> overload instead.")]
    public static IEnumerable<TTarget> SelectFacets<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>(this IEnumerable source)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        foreach (var item in source)
        {
            if (item is null) continue;
            yield return item.ToFacet<TTarget>();
        }
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
    public static IQueryable<TTarget> SelectFacet<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TTarget>(this IQueryable<TSource> source)
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

    /// <summary>
    /// Projects the elements of the source query into <typeparamref name="TTarget"/> using the facet's generated projection.
    /// </summary>
    /// <remarks>
    /// Uses <c>TTarget.Projection</c> (an <see cref="Expression{TDelegate}"/> of type
    /// <c>Expression&lt;Func&lt;DeclaredSource, TTarget&gt;&gt;</c>) and adapts the parameter to the query's
    /// element type if necessary (by inserting a cast). This builds an expression tree only (no materialization)
    /// and therefore uses deferred execution; translation behavior is provider-dependent.
    /// </remarks>
    /// <typeparam name="TTarget">
    /// The facet target type (class) annotated with <c>[Facet]</c> and exposing a public static <c>Projection</c> property.
    /// </typeparam>
    /// <param name="source">The source <see cref="IQueryable"/>. Cannot be <see langword="null"/>.</param>
    /// <returns>An <see cref="IQueryable{T}"/> of <typeparamref name="TTarget"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if <typeparamref name="TTarget"/> is not annotated with a <c>[Facet]</c> attribute or does not define a
    /// static <c>Projection</c> property.</exception>
    public static IQueryable<TTarget> SelectFacet<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TTarget>(this IQueryable source)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var targetType = typeof(TTarget);        

        var declaredProjection = GetDeclaredProjectionLambda(targetType);

        // Adapt the declared projection to the source's actual element type, if needed.
        var adapted = GetOrBuildAdaptedProjection(source.ElementType, targetType, declaredProjection);

        // Build Queryable.Select<TElement, TTarget>(source.Expression, adapted)
        var selectCall = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Select),
            new[] { source.ElementType, targetType },
            source.Expression,
            adapted);

        return source.Provider.CreateQuery<TTarget>(selectCall);
    }

    private static Type? GetDeclaredSourceType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type targetType)
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

    private static LambdaExpression GetDeclaredProjectionLambda([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type targetType)
    {
        if (_declaredProjectionByTarget.TryGetValue(targetType, out var cached))
            return cached;

        var prop = targetType.GetProperty("Projection", BindingFlags.Public | BindingFlags.Static)
                  ?? throw new InvalidOperationException(
                      $"Type {targetType.Name} must define a public static Projection property.");

        var value = prop.GetValue(null)
                   ?? throw new InvalidOperationException($"{targetType.Name}.Projection returned null.");

        if (value is not LambdaExpression lambda)
            throw new InvalidOperationException($"{targetType.Name}.Projection must be an Expression<Func<..., {targetType.Name}>>.");
        
        _declaredProjectionByTarget[targetType] = lambda;
        return lambda;
    }

    private static LambdaExpression GetOrBuildAdaptedProjection(Type elementType, Type targetType, LambdaExpression declaredProjection)
    {
        var key = (elementType, targetType);
        if (_adaptedProjectionByElementAndTarget.TryGetValue(key, out var cached))
            return cached;

        // If element type matches the projection's parameter type, use it as-is.
        var declaredParam = declaredProjection.Parameters[0];
        if (declaredParam.Type == elementType)
        {
            _adaptedProjectionByElementAndTarget[key] = declaredProjection;
            return declaredProjection;
        }

        // Otherwise, rebuild: (TElement e) => [declaredProjection.Body with param replaced by (DeclaredSource)e]
        var newParam = Expression.Parameter(elementType, declaredParam.Name);
        var replacement = Expression.Convert(newParam, declaredParam.Type); // cast to declared source
        var body = new ReplaceParameterVisitor(declaredParam, replacement).Visit(declaredProjection.Body)
                   ?? throw new InvalidOperationException("Failed to adapt Projection expression.");

        var adapted = Expression.Lambda(body, newParam);
        _adaptedProjectionByElementAndTarget[key] = adapted;
        return adapted;
    }

    /// <summary>
    /// Applies changed properties from a facet DTO back to the source object.
    /// Only updates properties that exist in both the facet and the source and have different values.
    /// </summary>
    /// <typeparam name="TSource">The source entity type being updated</typeparam>
    /// <typeparam name="TFacet">The facet DTO type containing the new values</typeparam>
    /// <param name="source">The source instance to update</param>
    /// <param name="facet">The facet DTO containing the new property values</param>
    /// <returns>The updated source instance (for fluent chaining)</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or facet is null</exception>
    /// <example>
    /// <code>
    /// var user = new User { Id = 1, Name = "John", Email = "john@example.com" };
    /// var facet = new UserFacet { Name = "Jane", Email = "john@example.com" };
    ///
    /// user.ApplyFacet&lt;User, UserFacet&gt;(facet);
    /// // user.Name is now "Jane", Email unchanged
    /// </code>
    /// </example>
    public static TSource ApplyFacet<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TFacet>(this TSource source, TFacet facet)
        where TSource : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (facet is null) throw new ArgumentNullException(nameof(facet));

        // Get properties that exist in both Facet and Source
        var facetProperties = typeof(TFacet).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, p => p);

        var sourceProperties = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite);

        // Update source properties from facet where values differ
        foreach (var sourceProperty in sourceProperties)
        {
            if (facetProperties.TryGetValue(sourceProperty.Name, out var facetProperty))
            {
                var facetValue = facetProperty.GetValue(facet);
                var sourceValue = sourceProperty.GetValue(source);

                // Only update if values are different
                if (!Equals(facetValue, sourceValue))
                {
                    sourceProperty.SetValue(source, facetValue);
                }
            }
        }

        return source;
    }

    /// <summary>
    /// Applies changed properties from a facet DTO back to the source object.
    /// The facet type is inferred from the TFacet parameter.
    /// Only updates properties that exist in both the facet and the source and have different values.
    /// </summary>
    /// <typeparam name="TFacet">The facet DTO type containing the new values</typeparam>
    /// <param name="source">The source instance to update</param>
    /// <param name="facet">The facet DTO containing the new property values</param>
    /// <returns>The updated source instance (for fluent chaining)</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or facet is null</exception>
    /// <example>
    /// <code>
    /// var user = new User { Id = 1, Name = "John", Email = "john@example.com" };
    /// var facet = new UserFacet { Name = "Jane", Email = "john@example.com" };
    ///
    /// user.ApplyFacet(facet);
    /// // user.Name is now "Jane", Email unchanged
    /// </code>
    /// </example>
    [RequiresUnreferencedCode("This method uses reflection on the runtime type of the source object. Use the strongly-typed ApplyFacet<TSource, TFacet> overload instead.")]
    public static object ApplyFacet<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TFacet>(this object source, TFacet facet)
        where TFacet : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (facet is null) throw new ArgumentNullException(nameof(facet));

        var sourceType = source.GetType();

        // Get properties that exist in both Facet and Source
        var facetProperties = typeof(TFacet).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, p => p);

        var sourceProperties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite);

        // Update source properties from facet where values differ
        foreach (var sourceProperty in sourceProperties)
        {
            if (facetProperties.TryGetValue(sourceProperty.Name, out var facetProperty))
            {
                var facetValue = facetProperty.GetValue(facet);
                var sourceValue = sourceProperty.GetValue(source);

                // Only update if values are different
                if (!Equals(facetValue, sourceValue))
                {
                    sourceProperty.SetValue(source, facetValue);
                }
            }
        }

        return source;
    }

    /// <summary>
    /// Applies changed properties from a facet DTO back to the source object and returns information about which properties were changed.
    /// This is useful for auditing, logging, or conditional logic based on what actually changed.
    /// </summary>
    /// <typeparam name="TSource">The source entity type being updated</typeparam>
    /// <typeparam name="TFacet">The facet DTO type containing the new values</typeparam>
    /// <param name="source">The source instance to update</param>
    /// <param name="facet">The facet DTO containing the new property values</param>
    /// <returns>A result containing the updated source and a list of property names that were changed</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or facet is null</exception>
    /// <example>
    /// <code>
    /// var result = user.ApplyFacetWithChanges&lt;User, UserFacet&gt;(facet);
    /// if (result.HasChanges)
    /// {
    ///     Console.WriteLine($"Changed properties: {string.Join(", ", result.ChangedProperties)}");
    /// }
    /// </code>
    /// </example>
    public static FacetApplyResult<TSource> ApplyFacetWithChanges<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TFacet>(this TSource source, TFacet facet)
        where TSource : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (facet is null) throw new ArgumentNullException(nameof(facet));

        var changedProperties = new List<string>();

        // Get properties that exist in both Facet and Source
        var facetProperties = typeof(TFacet).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, p => p);

        var sourceProperties = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite);

        // Update source properties from facet where values differ
        foreach (var sourceProperty in sourceProperties)
        {
            if (facetProperties.TryGetValue(sourceProperty.Name, out var facetProperty))
            {
                var facetValue = facetProperty.GetValue(facet);
                var sourceValue = sourceProperty.GetValue(source);

                // Only update if values are different
                if (!Equals(facetValue, sourceValue))
                {
                    sourceProperty.SetValue(source, facetValue);
                    changedProperties.Add(sourceProperty.Name);
                }
            }
        }

        return new FacetApplyResult<TSource>(source, changedProperties);
    }

    private sealed class ReplaceParameterVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParam;
        private readonly Expression _newExpr;
        public ReplaceParameterVisitor(ParameterExpression oldParam, Expression newExpr)
        {
            _oldParam = oldParam ?? throw new ArgumentNullException(nameof(oldParam));
            _newExpr = newExpr ?? throw new ArgumentNullException(nameof(newExpr));
        }
        protected override Expression VisitParameter(ParameterExpression node)
            => node == _oldParam ? _newExpr : base.VisitParameter(node);
    }
}

/// <summary>
/// Represents the result of a facet apply operation, containing the updated source and information about what changed.
/// </summary>
/// <typeparam name="TSource">The type of source that was updated</typeparam>
public readonly record struct FacetApplyResult<TSource>(
    TSource Source,
    IReadOnlyList<string> ChangedProperties)
    where TSource : class
{
    /// <summary>
    /// Gets a value indicating whether any properties were changed during the apply operation.
    /// </summary>
    public bool HasChanges => ChangedProperties.Count > 0;
}
