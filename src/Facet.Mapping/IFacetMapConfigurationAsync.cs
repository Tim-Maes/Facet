using System.Threading;
using System.Threading.Tasks;

namespace Facet.Mapping;

/// <summary>
/// Allows defining async custom mapping logic between a source and target Facet-generated type.
/// Use this interface when your mapping logic requires async operations like database calls, API calls, or I/O operations.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
public interface IFacetMapConfigurationAsync<TSource, TTarget>
{
    /// <summary>
    /// Asynchronously maps source to target with custom logic.
    /// This method is called after the standard property copying is completed.
    /// </summary>
    /// <param name="source">The source object</param>
    /// <param name="target">The target object with basic properties already copied</param>
    /// <param name="cancellationToken">Cancellation token to cancel the async operation</param>
    /// <returns>A task representing the async mapping operation</returns>
    static abstract Task MapAsync(TSource source, TTarget target, CancellationToken cancellationToken = default);
}