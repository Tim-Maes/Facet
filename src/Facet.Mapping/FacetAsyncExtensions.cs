using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Facet.Mapping;

/// <summary>
/// Async extension methods for Facet mapping operations.
/// These methods enable async mapping scenarios for custom mapping configurations.
/// </summary>
public static class FacetAsyncExtensions
{
    /// <summary>
    /// Asynchronously maps a single instance using async configuration.
    /// Creates a new target instance and applies async mapping logic.
    /// </summary>
    /// <typeparam name="TSource">The source type</typeparam>
    /// <typeparam name="TTarget">The target type (must have parameterless constructor)</typeparam>
    /// <typeparam name="TAsyncMapper">The async mapper configuration type</typeparam>
    /// <param name="source">The source instance to map</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped target instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
    public static async Task<TTarget> ToFacetAsync<TSource, TTarget, TAsyncMapper>(
        this TSource source, 
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
        where TAsyncMapper : IFacetMapConfigurationAsync<TSource, TTarget>
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        
        var target = new TTarget();
        await TAsyncMapper.MapAsync(source, target, cancellationToken);
        return target;
    }

    /// <summary>
    /// Asynchronously maps a single instance using the generated Facet constructor and async configuration.
    /// This method first uses the generated constructor, then applies async mapping.
    /// </summary>
    /// <typeparam name="TSource">The source type</typeparam>
    /// <typeparam name="TTarget">The target type (must have constructor accepting TSource)</typeparam>
    /// <typeparam name="TAsyncMapper">The async mapper configuration type</typeparam>
    /// <param name="source">The source instance to map</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped target instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
    public static async Task<TTarget> ToFacetWithConstructorAsync<TSource, TTarget, TAsyncMapper>(
        this TSource source,
        CancellationToken cancellationToken = default)
        where TTarget : class
        where TAsyncMapper : IFacetMapConfigurationAsync<TSource, TTarget>
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        
        // Use generated constructor first
        var target = (TTarget)Activator.CreateInstance(typeof(TTarget), source)!;
        
        // Then apply async mapping
        await TAsyncMapper.MapAsync(source, target, cancellationToken);
        
        return target;
    }

    /// <summary>
    /// Asynchronously maps a collection with async configuration.
    /// Maps each item individually with proper cancellation support.
    /// </summary>
    /// <typeparam name="TSource">The source item type</typeparam>
    /// <typeparam name="TTarget">The target item type (must have parameterless constructor)</typeparam>
    /// <typeparam name="TAsyncMapper">The async mapper configuration type</typeparam>
    /// <param name="source">The source collection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped collection</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
    public static async Task<List<TTarget>> ToFacetsAsync<TSource, TTarget, TAsyncMapper>(
        this IEnumerable<TSource> source,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
        where TAsyncMapper : IFacetMapConfigurationAsync<TSource, TTarget>
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        
        var result = new List<TTarget>();
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapped = await item.ToFacetAsync<TSource, TTarget, TAsyncMapper>(cancellationToken);
            result.Add(mapped);
        }
        return result;
    }

    /// <summary>
    /// Asynchronously maps a collection using parallel processing for better performance.
    /// Use this method when individual mappings are independent and can be parallelized.
    /// </summary>
    /// <typeparam name="TSource">The source item type</typeparam>
    /// <typeparam name="TTarget">The target item type (must have parameterless constructor)</typeparam>
    /// <typeparam name="TAsyncMapper">The async mapper configuration type</typeparam>
    /// <param name="source">The source collection</param>
    /// <param name="maxDegreeOfParallelism">Maximum number of concurrent operations (default: Environment.ProcessorCount)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped collection</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
    public static async Task<List<TTarget>> ToFacetsParallelAsync<TSource, TTarget, TAsyncMapper>(
        this IEnumerable<TSource> source,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
        where TAsyncMapper : IFacetMapConfigurationAsync<TSource, TTarget>
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        
        if (maxDegreeOfParallelism == -1)
            maxDegreeOfParallelism = Environment.ProcessorCount;

        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
        var tasks = source.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await item.ToFacetAsync<TSource, TTarget, TAsyncMapper>(cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// Asynchronously maps using both sync and async configurations.
    /// Applies sync mapping first, then async mapping for hybrid scenarios.
    /// </summary>
    /// <typeparam name="TSource">The source type</typeparam>
    /// <typeparam name="TTarget">The target type (must have parameterless constructor)</typeparam>
    /// <typeparam name="TSyncMapper">The sync mapper configuration type</typeparam>
    /// <typeparam name="TAsyncMapper">The async mapper configuration type</typeparam>
    /// <param name="source">The source instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped target instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
    public static async Task<TTarget> ToFacetHybridAsync<TSource, TTarget, TSyncMapper, TAsyncMapper>(
        this TSource source,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
        where TSyncMapper : IFacetMapConfiguration<TSource, TTarget>
        where TAsyncMapper : IFacetMapConfigurationAsync<TSource, TTarget>
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        
        var target = new TTarget();
        
        // Apply sync mapping first
        TSyncMapper.Map(source, target);
        
        // Then apply async mapping
        await TAsyncMapper.MapAsync(source, target, cancellationToken);
        
        return target;
    }

    /// <summary>
    /// Asynchronously maps using a hybrid configuration that implements both sync and async interfaces.
    /// </summary>
    /// <typeparam name="TSource">The source type</typeparam>
    /// <typeparam name="TTarget">The target type (must have parameterless constructor)</typeparam>
    /// <typeparam name="THybridMapper">The hybrid mapper configuration type</typeparam>
    /// <param name="source">The source instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped target instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
    public static async Task<TTarget> ToFacetHybridAsync<TSource, TTarget, THybridMapper>(
        this TSource source,
        CancellationToken cancellationToken = default)
        where TTarget : class, new()
        where THybridMapper : IFacetMapConfigurationHybrid<TSource, TTarget>
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        
        var target = new TTarget();
        
        // Apply sync mapping first
        THybridMapper.Map(source, target);
        
        // Then apply async mapping
        await THybridMapper.MapAsync(source, target, cancellationToken);
        
        return target;
    }
}