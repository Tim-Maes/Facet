using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Facet.Mapping;

/// <summary>
/// Synchronous extension methods for Facet mapping operations that support dependency injection.
/// These methods enable custom mapping scenarios with injected services.
/// </summary>
public static class FacetSyncExtensions
{
    /// <summary>
    /// Maps a single instance using a synchronous mapper instance with dependency injection support.
    /// Creates a new target instance and applies custom mapping logic.
    /// </summary>
    /// <typeparam name="TSource">The source type</typeparam>
    /// <typeparam name="TTarget">The target type (must have parameterless constructor)</typeparam>
    /// <param name="source">The source instance to map</param>
    /// <param name="mapper">The synchronous mapper instance (supports dependency injection)</param>
    /// <returns>The mapped target instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or mapper is null</exception>
    public static TTarget ToFacet<TSource, TTarget>(
        this TSource source,
        IFacetMapConfigurationInstance<TSource, TTarget> mapper)
        where TTarget : class, new()
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));
        
        var target = new TTarget();
        mapper.Map(source, target);
        return target;
    }

    /// <summary>
    /// Maps a single instance using the generated Facet constructor and a synchronous mapper instance.
    /// This method first uses the generated constructor, then applies custom mapping logic.
    /// </summary>
    /// <typeparam name="TSource">The source type</typeparam>
    /// <typeparam name="TTarget">The target type (must have constructor accepting TSource)</typeparam>
    /// <param name="source">The source instance to map</param>
    /// <param name="mapper">The synchronous mapper instance (supports dependency injection)</param>
    /// <returns>The mapped target instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or mapper is null</exception>
    public static TTarget ToFacetWithConstructor<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>(
        this TSource source,
        IFacetMapConfigurationInstance<TSource, TTarget> mapper)
        where TTarget : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));
        
        // Use generated constructor first
        var target = (TTarget)Activator.CreateInstance(typeof(TTarget), source)!;
        
        // Then apply custom mapping
        mapper.Map(source, target);
        
        return target;
    }

    /// <summary>
    /// Maps a collection using a synchronous mapper instance with dependency injection support.
    /// Maps each item individually.
    /// </summary>
    /// <typeparam name="TSource">The source item type</typeparam>
    /// <typeparam name="TTarget">The target item type (must have parameterless constructor)</typeparam>
    /// <param name="source">The source collection</param>
    /// <param name="mapper">The synchronous mapper instance (supports dependency injection)</param>
    /// <returns>The mapped collection</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or mapper is null</exception>
    public static List<TTarget> ToFacets<TSource, TTarget>(
        this IEnumerable<TSource> source,
        IFacetMapConfigurationInstance<TSource, TTarget> mapper)
        where TTarget : class, new()
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));
        
        return source.Select(item => item.ToFacet(mapper)).ToList();
    }

    /// <summary>
    /// Maps a single instance using a hybrid mapper instance that implements both sync and async interfaces.
    /// Only applies the synchronous mapping part.
    /// </summary>
    /// <typeparam name="TSource">The source type</typeparam>
    /// <typeparam name="TTarget">The target type (must have parameterless constructor)</typeparam>
    /// <param name="source">The source instance</param>
    /// <param name="mapper">The hybrid mapper instance (supports dependency injection)</param>
    /// <returns>The mapped target instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or mapper is null</exception>
    /// <remarks>
    /// This method uses the obsolete IFacetMapConfigurationHybridInstance interface.
    /// Consider implementing both IFacetMapConfigurationInstance and IFacetMapConfigurationAsyncInstance directly.
    /// </remarks>
#pragma warning disable CS0618 // Type or member is obsolete
    public static TTarget ToFacetSync<TSource, TTarget>(
        this TSource source,
        IFacetMapConfigurationHybridInstance<TSource, TTarget> mapper)
        where TTarget : class, new()
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));

        var target = new TTarget();
        mapper.Map(source, target);
        return target;
    }
#pragma warning restore CS0618 // Type or member is obsolete
}