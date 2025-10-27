using System;

namespace Facet.Mapping;

/// <summary>
/// Enhanced interface for defining custom mapping logic that supports init-only properties and records.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
/// <remarks>
/// <para><strong>This interface is obsolete and will be removed in a future version.</strong></para>
/// <para>For init-only properties, use the generated <c>FromSource</c> factory method instead:</para>
/// <code>
/// // Instead of implementing IFacetMapConfigurationWithReturn
/// var dto = MyDto.FromSource(source); // Uses generated factory method
/// </code>
/// <para>Or use object initializer syntax with the generated constructor for custom logic.</para>
/// </remarks>
[Obsolete("Use the generated FromSource factory method for init-only properties instead. This interface will be removed in a future version.")]
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
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
/// <remarks>
/// <para><strong>This interface is obsolete and will be removed in a future version.</strong></para>
/// <para>For init-only properties with DI, use the generated <c>FromSource</c> factory method instead:</para>
/// <code>
/// // Instead of implementing IFacetMapConfigurationWithReturnInstance
/// var dto = MyDto.FromSource(source); // Uses generated factory method
/// </code>
/// <para>Or use object initializer syntax with the generated constructor for custom logic.</para>
/// </remarks>
[Obsolete("Use the generated FromSource factory method for init-only properties instead. This interface will be removed in a future version.")]
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