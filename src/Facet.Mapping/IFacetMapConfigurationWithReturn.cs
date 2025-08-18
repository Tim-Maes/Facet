namespace Facet.Mapping;

/// <summary>
/// Enhanced interface for defining custom mapping logic that supports init-only properties and records.
/// This interface allows the mapping method to return a new target instance with all properties set,
/// including init-only properties that cannot be modified after construction.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
public interface IFacetMapConfigurationWithReturn<TSource, TTarget>
{
    /// <summary>
    /// Maps source to target with custom logic, returning a new target instance.
    /// This method is called instead of the standard property copying when init-only properties need to be set.
    /// </summary>
    /// <param name="source">The source object</param>
    /// <param name="target">The initial target object (may be ignored for init-only scenarios)</param>
    /// <returns>A new target instance with all properties set, including init-only properties</returns>
    static abstract TTarget Map(TSource source, TTarget target);
}

/// <summary>
/// Instance-based interface for defining custom mapping logic that supports init-only properties with dependency injection.
/// This interface allows the mapping method to return a new target instance with all properties set,
/// including init-only properties that cannot be modified after construction.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
public interface IFacetMapConfigurationWithReturnInstance<TSource, TTarget>
{
    /// <summary>
    /// Maps source to target with custom logic, returning a new target instance.
    /// This method is called instead of the standard property copying when init-only properties need to be set.
    /// </summary>
    /// <param name="source">The source object</param>
    /// <param name="target">The initial target object (may be ignored for init-only scenarios)</param>
    /// <returns>A new target instance with all properties set, including init-only properties</returns>
    TTarget Map(TSource source, TTarget target);
}