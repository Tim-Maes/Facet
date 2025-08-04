using System.Threading;
using System.Threading.Tasks;

namespace Facet.Mapping;

/// <summary>
/// Provides both synchronous and asynchronous mapping capabilities in a single interface.
/// Implement this interface when you need both sync operations (for performance-critical paths) 
/// and async operations (for I/O-bound operations) in the same mapper.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
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