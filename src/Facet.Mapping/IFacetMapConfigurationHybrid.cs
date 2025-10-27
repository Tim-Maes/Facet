using System;
using System.Threading;
using System.Threading.Tasks;

namespace Facet.Mapping;

/// <summary>
/// Provides both synchronous and asynchronous mapping capabilities in a single interface.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
/// <remarks>
/// <para><strong>This interface is obsolete and will be removed in a future version.</strong></para>
/// <para>Instead of implementing this hybrid interface, directly implement both base interfaces:</para>
/// <code>
/// public class MyMapper :
///     IFacetMapConfiguration&lt;TSource, TTarget&gt;,
///     IFacetMapConfigurationAsync&lt;TSource, TTarget&gt;
/// {
///     public static void Map(TSource source, TTarget target) { ... }
///     public static Task MapAsync(TSource source, TTarget target, CancellationToken ct) { ... }
/// }
/// </code>
/// <para>This provides the same functionality with clearer intent and better discoverability.</para>
/// </remarks>
[Obsolete("Implement IFacetMapConfiguration<TSource, TTarget> and IFacetMapConfigurationAsync<TSource, TTarget> directly instead. This interface will be removed in a future version.")]
public interface IFacetMapConfigurationHybrid<TSource, TTarget> :
    IFacetMapConfiguration<TSource, TTarget>,
    IFacetMapConfigurationAsync<TSource, TTarget>
{
    // This interface combines both sync and async mapping capabilities.
    // Implementations must provide both Map() and MapAsync() methods.
    //
    // Typical usage pattern:
    // - Map(): Fast, synchronous operations (property copying, calculations)
    // - MapAsync(): Expensive, async operations (database queries, API calls)
}

/// <summary>
/// Instance-based interface that provides both synchronous and asynchronous mapping capabilities
/// with dependency injection support.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
/// <remarks>
/// <para><strong>This interface is obsolete and will be removed in a future version.</strong></para>
/// <para>Instead of implementing this hybrid interface, directly implement both base interfaces:</para>
/// <code>
/// public class MyMapper :
///     IFacetMapConfigurationInstance&lt;TSource, TTarget&gt;,
///     IFacetMapConfigurationAsyncInstance&lt;TSource, TTarget&gt;
/// {
///     public void Map(TSource source, TTarget target) { ... }
///     public Task MapAsync(TSource source, TTarget target, CancellationToken ct) { ... }
/// }
/// </code>
/// <para>This provides the same functionality with clearer intent and better discoverability.</para>
/// </remarks>
[Obsolete("Implement IFacetMapConfigurationInstance<TSource, TTarget> and IFacetMapConfigurationAsyncInstance<TSource, TTarget> directly instead. This interface will be removed in a future version.")]
public interface IFacetMapConfigurationHybridInstance<TSource, TTarget> :
    IFacetMapConfigurationInstance<TSource, TTarget>,
    IFacetMapConfigurationAsyncInstance<TSource, TTarget>
{
    // This interface combines both sync and async mapping capabilities for instance-based mappers.
    // Implementations must provide both Map() and MapAsync() methods.
    //
    // Typical usage pattern:
    // - Map(): Fast, synchronous operations (property copying, calculations)
    // - MapAsync(): Expensive, async operations (database queries, API calls)
}