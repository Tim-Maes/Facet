namespace Facet.Mapping;

/// <summary>
/// Allows defining custom logic that runs BEFORE the automatic property mapping.
/// Use this to validate input, transform source data, or prepare the target.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
/// <remarks>
/// <para>
/// BeforeMap is called before any properties are copied from source to target.
/// This allows you to:
/// - Validate the source object
/// - Set up default values on the target
/// - Prepare state for the mapping operation
/// </para>
/// <para>
/// Example usage:
/// <code>
/// public class UserBeforeMapConfig : IFacetBeforeMapConfiguration&lt;User, UserDto&gt;
/// {
///     public static void BeforeMap(User source, UserDto target)
///     {
///         // Validate source
///         if (source == null) throw new ArgumentNullException(nameof(source));
///         
///         // Set defaults on target
///         target.MappedAt = DateTime.UtcNow;
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public interface IFacetBeforeMapConfiguration<TSource, TTarget>
{
    /// <summary>
    /// Called before automatic property mapping occurs.
    /// </summary>
    /// <param name="source">The source object being mapped from.</param>
    /// <param name="target">The target object being mapped to (properties not yet populated).</param>
    static abstract void BeforeMap(TSource source, TTarget target);
}

/// <summary>
/// Allows defining custom logic that runs AFTER the automatic property mapping.
/// Use this to compute derived values, apply transformations, or validate the result.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
/// <remarks>
/// <para>
/// AfterMap is called after all properties are copied from source to target.
/// This allows you to:
/// - Compute derived/calculated properties
/// - Apply business rules
/// - Transform mapped values
/// - Validate the final result
/// </para>
/// <para>
/// Example usage:
/// <code>
/// public class UserAfterMapConfig : IFacetAfterMapConfiguration&lt;User, UserDto&gt;
/// {
///     public static void AfterMap(User source, UserDto target)
///     {
///         // Compute derived value
///         target.FullName = $"{target.FirstName} {target.LastName}";
///         target.Age = CalculateAge(source.DateOfBirth);
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public interface IFacetAfterMapConfiguration<TSource, TTarget>
{
    /// <summary>
    /// Called after automatic property mapping completes.
    /// </summary>
    /// <param name="source">The source object that was mapped from.</param>
    /// <param name="target">The target object with all properties now populated.</param>
    static abstract void AfterMap(TSource source, TTarget target);
}

/// <summary>
/// Combines BeforeMap and AfterMap hooks in a single interface.
/// Use this when you need both pre and post mapping logic.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
public interface IFacetMapHooksConfiguration<TSource, TTarget>
    : IFacetBeforeMapConfiguration<TSource, TTarget>,
      IFacetAfterMapConfiguration<TSource, TTarget>
{
}

/// <summary>
/// Instance-based interface for BeforeMap with dependency injection support.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
public interface IFacetBeforeMapConfigurationInstance<TSource, TTarget>
{
    /// <summary>
    /// Called before automatic property mapping occurs.
    /// </summary>
    /// <param name="source">The source object being mapped from.</param>
    /// <param name="target">The target object being mapped to (properties not yet populated).</param>
    void BeforeMap(TSource source, TTarget target);
}

/// <summary>
/// Instance-based interface for AfterMap with dependency injection support.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
public interface IFacetAfterMapConfigurationInstance<TSource, TTarget>
{
    /// <summary>
    /// Called after automatic property mapping completes.
    /// </summary>
    /// <param name="source">The source object that was mapped from.</param>
    /// <param name="target">The target object with all properties now populated.</param>
    void AfterMap(TSource source, TTarget target);
}

/// <summary>
/// Instance-based interface combining BeforeMap and AfterMap hooks with DI support.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
public interface IFacetMapHooksConfigurationInstance<TSource, TTarget>
    : IFacetBeforeMapConfigurationInstance<TSource, TTarget>,
      IFacetAfterMapConfigurationInstance<TSource, TTarget>
{
}
