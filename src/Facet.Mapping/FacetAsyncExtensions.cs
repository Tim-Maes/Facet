using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
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
    /// Asynchronously maps a single instance using async configuration with simplified syntax.
    /// Creates a new target instance and applies async mapping logic.
    /// This overload uses type inference to determine the source type from the method call.
    /// </summary>
    /// <typeparam name="TTarget">The target type (must have parameterless constructor)</typeparam>
    /// <typeparam name="TAsyncMapper">The async mapper configuration type implementing IFacetMapConfigurationAsync&lt;TSource, TTarget&gt;</typeparam>
    /// <param name="source">The source instance to map</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped target instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
    /// <remarks>
    /// This method uses reflection to determine the source type at runtime.
    /// For better performance, use the overload with explicit type parameters.
    /// </remarks>
    [RequiresUnreferencedCode("This method uses reflection to discover mapper methods at runtime. Use the strongly-typed ToFacetAsync<TSource, TTarget, TAsyncMapper> overload instead.")]
    public static async Task<TTarget> ToFacetAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget, TAsyncMapper>(
        this object source, 
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        
        var sourceType = source.GetType();
        var targetType = typeof(TTarget);
        
        var target = CreateFacetInstance<TTarget>(source, sourceType, targetType);
        
        var mapperType = typeof(TAsyncMapper);
        var mapMethod = mapperType.GetMethod("MapAsync", new[] { sourceType, targetType, typeof(CancellationToken) });
        
        if (mapMethod == null)
        {
            throw new InvalidOperationException($"Mapper {mapperType.Name} does not implement IFacetMapConfigurationAsync<{sourceType.Name}, {targetType.Name}>");
        }
        
        await (Task)mapMethod.Invoke(null, new object[] { source, target, cancellationToken })!;
        return target;
    }

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
    public static async Task<TTarget> ToFacetAsync<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget, TAsyncMapper>(
        this TSource source, 
        CancellationToken cancellationToken = default)
        where TTarget : class
        where TAsyncMapper : IFacetMapConfigurationAsync<TSource, TTarget>
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        
        var target = CreateFacetInstance<TTarget>(source, typeof(TSource), typeof(TTarget));
        await TAsyncMapper.MapAsync(source, target, cancellationToken);
        return target;
    }

    /// <summary>
    /// Asynchronously maps a single instance using an async mapper instance with dependency injection support.
    /// Creates a new target instance and applies async mapping logic.
    /// </summary>
    /// <typeparam name="TSource">The source type</typeparam>
    /// <typeparam name="TTarget">The target type (must have parameterless constructor)</typeparam>
    /// <param name="source">The source instance to map</param>
    /// <param name="mapper">The async mapper instance (supports dependency injection)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped target instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or mapper is null</exception>
    public static async Task<TTarget> ToFacetAsync<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>(
        this TSource source,
        IFacetMapConfigurationAsyncInstance<TSource, TTarget> mapper,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));
        
        var target = CreateFacetInstance<TTarget>(source, typeof(TSource), typeof(TTarget));
        await mapper.MapAsync(source, target, cancellationToken);
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
    public static async Task<TTarget> ToFacetWithConstructorAsync<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget, TAsyncMapper>(
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
    /// Asynchronously maps a single instance using the generated Facet constructor and an async mapper instance.
    /// This method first uses the generated constructor, then applies async mapping.
    /// </summary>
    /// <typeparam name="TSource">The source type</typeparam>
    /// <typeparam name="TTarget">The target type (must have constructor accepting TSource)</typeparam>
    /// <param name="source">The source instance to map</param>
    /// <param name="mapper">The async mapper instance (supports dependency injection)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped target instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or mapper is null</exception>
    public static async Task<TTarget> ToFacetWithConstructorAsync<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>(
        this TSource source,
        IFacetMapConfigurationAsyncInstance<TSource, TTarget> mapper,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));
        
        // Use generated constructor first
        var target = (TTarget)Activator.CreateInstance(typeof(TTarget), source)!;
        
        // Then apply async mapping
        await mapper.MapAsync(source, target, cancellationToken);
        
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
        where TTarget : class
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
    /// Asynchronously maps a collection using async configuration with simplified syntax.
    /// Maps each item individually with proper cancellation support.
    /// This overload uses type inference to determine the source type from the method call.
    /// </summary>
    /// <typeparam name="TTarget">The target item type (must have parameterless constructor)</typeparam>
    /// <typeparam name="TAsyncMapper">The async mapper configuration type</typeparam>
    /// <param name="source">The source collection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped collection</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
    /// <remarks>
    /// This method uses reflection to determine the source type at runtime.
    /// For better performance, use the overload with explicit type parameters.
    /// </remarks>
    [RequiresUnreferencedCode("This method uses reflection to discover mapper methods at runtime. Use the strongly-typed ToFacetsAsync<TSource, TTarget, TAsyncMapper> overload instead.")]
    public static async Task<List<TTarget>> ToFacetsAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget, TAsyncMapper>(
        this IEnumerable source,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        
        var result = new List<TTarget>();
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapped = await item.ToFacetAsync<TTarget, TAsyncMapper>(cancellationToken);
            result.Add(mapped);
        }
        return result;
    }

    /// <summary>
    /// Asynchronously maps a collection using an async mapper instance with dependency injection support.
    /// Maps each item individually with proper cancellation support.
    /// </summary>
    /// <typeparam name="TSource">The source item type</typeparam>
    /// <typeparam name="TTarget">The target item type (must have parameterless constructor)</typeparam>
    /// <param name="source">The source collection</param>
    /// <param name="mapper">The async mapper instance (supports dependency injection)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped collection</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or mapper is null</exception>
    public static async Task<List<TTarget>> ToFacetsAsync<TSource, TTarget>(
        this IEnumerable<TSource> source,
        IFacetMapConfigurationAsyncInstance<TSource, TTarget> mapper,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));
        
        var result = new List<TTarget>();
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapped = await item.ToFacetAsync(mapper, cancellationToken);
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
        where TTarget : class
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
    /// Asynchronously maps a collection using parallel processing with simplified syntax.
    /// Use this method when individual mappings are independent and can be parallelized.
    /// This overload uses type inference to determine the source type from the method call.
    /// </summary>
    /// <typeparam name="TTarget">The target item type (must have parameterless constructor)</typeparam>
    /// <typeparam name="TAsyncMapper">The async mapper configuration type</typeparam>
    /// <param name="source">The source collection</param>
    /// <param name="maxDegreeOfParallelism">Maximum number of concurrent operations (default: Environment.ProcessorCount)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped collection</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
    /// <remarks>
    /// This method uses reflection to determine the source type at runtime.
    /// For better performance, use the overload with explicit type parameters.
    /// </remarks>
    [RequiresUnreferencedCode("This method uses reflection to discover mapper methods at runtime. Use the strongly-typed ToFacetsParallelAsync<TSource, TTarget, TAsyncMapper> overload instead.")]
    public static async Task<List<TTarget>> ToFacetsParallelAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget, TAsyncMapper>(
        this IEnumerable source,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        
        if (maxDegreeOfParallelism == -1)
            maxDegreeOfParallelism = Environment.ProcessorCount;

        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
        var tasks = source.Cast<object>().Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await item.ToFacetAsync<TTarget, TAsyncMapper>(cancellationToken);
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
    /// Asynchronously maps a collection using parallel processing with a mapper instance for better performance.
    /// Use this method when individual mappings are independent and can be parallelized.
    /// </summary>
    /// <typeparam name="TSource">The source item type</typeparam>
    /// <typeparam name="TTarget">The target item type (must have parameterless constructor)</typeparam>
    /// <param name="source">The source collection</param>
    /// <param name="mapper">The async mapper instance (supports dependency injection)</param>
    /// <param name="maxDegreeOfParallelism">Maximum number of concurrent operations (default: Environment.ProcessorCount)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped collection</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or mapper is null</exception>
    public static async Task<List<TTarget>> ToFacetsParallelAsync<TSource, TTarget>(
        this IEnumerable<TSource> source,
        IFacetMapConfigurationAsyncInstance<TSource, TTarget> mapper,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));
        
        if (maxDegreeOfParallelism == -1)
            maxDegreeOfParallelism = Environment.ProcessorCount;

        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
        var tasks = source.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await item.ToFacetAsync(mapper, cancellationToken);
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
    public static async Task<TTarget> ToFacetHybridAsync<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget, TSyncMapper, TAsyncMapper>(
        this TSource source,
        CancellationToken cancellationToken = default)
        where TTarget : class
        where TSyncMapper : IFacetMapConfiguration<TSource, TTarget>
        where TAsyncMapper : IFacetMapConfigurationAsync<TSource, TTarget>
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        
        var target = CreateFacetInstance<TTarget>(source, typeof(TSource), typeof(TTarget));
        
        TSyncMapper.Map(source, target);
        
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
    /// <remarks>
    /// This method uses the obsolete IFacetMapConfigurationHybrid interface.
    /// Consider implementing both IFacetMapConfiguration and IFacetMapConfigurationAsync directly.
    /// </remarks>
#pragma warning disable CS0618 // Type or member is obsolete
    public static async Task<TTarget> ToFacetHybridAsync<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget, THybridMapper>(
        this TSource source,
        CancellationToken cancellationToken = default)
        where TTarget : class
        where THybridMapper : IFacetMapConfigurationHybrid<TSource, TTarget>
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var target = CreateFacetInstance<TTarget>(source, typeof(TSource), typeof(TTarget));

        // Apply sync mapping first
        THybridMapper.Map(source, target);

        // Then apply async mapping
        await THybridMapper.MapAsync(source, target, cancellationToken);

        return target;
    }
#pragma warning restore CS0618 // Type or member is obsolete

    /// <summary>
    /// Asynchronously maps using hybrid configuration with simplified syntax.
    /// The hybrid mapper implements both sync and async interfaces for optimal performance.
    /// This overload uses type inference to determine the source type from the method call.
    /// </summary>
    /// <typeparam name="TTarget">The target type (must have parameterless constructor)</typeparam>
    /// <typeparam name="THybridMapper">The hybrid mapper configuration type</typeparam>
    /// <param name="source">The source instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped target instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
    /// <remarks>
    /// This method uses reflection to determine the source type at runtime.
    /// For better performance, use the overload with explicit type parameters.
    /// This method uses the obsolete IFacetMapConfigurationHybrid interface pattern.
    /// </remarks>
#pragma warning disable CS0618 // Type or member is obsolete
    [RequiresUnreferencedCode("This method uses reflection to discover mapper methods at runtime. Use the strongly-typed ToFacetHybridAsync<TSource, TTarget, THybridMapper> overload instead.")]
    public static async Task<TTarget> ToFacetHybridAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget, THybridMapper>(
        this object source,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var sourceType = source.GetType();
        var targetType = typeof(TTarget);

        var target = CreateFacetInstance<TTarget>(source, sourceType, targetType);

        var mapperType = typeof(THybridMapper);
        var mapMethod = mapperType.GetMethod("Map", new[] { sourceType, targetType });

        if (mapMethod != null)
        {
            mapMethod.Invoke(null, new object[] { source, target });
        }

        var mapAsyncMethod = mapperType.GetMethod("MapAsync", new[] { sourceType, targetType, typeof(CancellationToken) });

        if (mapAsyncMethod != null)
        {
            await (Task)mapAsyncMethod.Invoke(null, new object[] { source, target, cancellationToken })!;
        }

        return target;
    }
#pragma warning restore CS0618 // Type or member is obsolete

    /// <summary>
    /// Asynchronously maps using a hybrid mapper instance that implements both sync and async interfaces.
    /// Supports dependency injection.
    /// </summary>
    /// <typeparam name="TSource">The source type</typeparam>
    /// <typeparam name="TTarget">The target type (must have parameterless constructor)</typeparam>
    /// <param name="source">The source instance</param>
    /// <param name="mapper">The hybrid mapper instance (supports dependency injection)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task containing the mapped target instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or mapper is null</exception>
    /// <remarks>
    /// This method uses the obsolete IFacetMapConfigurationHybridInstance interface.
    /// Consider implementing both IFacetMapConfigurationInstance and IFacetMapConfigurationAsyncInstance directly.
    /// </remarks>
#pragma warning disable CS0618 // Type or member is obsolete
    public static async Task<TTarget> ToFacetHybridAsync<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>(
        this TSource source,
        IFacetMapConfigurationHybridInstance<TSource, TTarget> mapper,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));

        var target = CreateFacetInstance<TTarget>(source, typeof(TSource), typeof(TTarget));

        // Apply sync mapping first
        mapper.Map(source, target);

        // Then apply async mapping
        await mapper.MapAsync(source, target, cancellationToken);

        return target;
    }
#pragma warning restore CS0618 // Type or member is obsolete

    private static TTarget CreateFacetInstance<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>(object source, Type sourceType, Type targetType)
        where TTarget : class
    {
        var fromSource = targetType.GetMethod(
            "FromSource",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { sourceType },
            null);

        if (fromSource != null)
        {
            return (TTarget)fromSource.Invoke(null, new object[] { source })!;
        }

        var ctor = targetType.GetConstructor(new[] { sourceType });
        if (ctor != null)
        {
            return (TTarget)Activator.CreateInstance(targetType, source)!;
        }

        try
        {
            return (TTarget)Activator.CreateInstance(targetType)!;
        }
        catch
        {
            throw new InvalidOperationException(
                $"Unable to create instance of {targetType.Name} from {sourceType.Name}: " +
                $"no compatible FromSource factory, constructor, or parameterless constructor found.");
        }
    }
}