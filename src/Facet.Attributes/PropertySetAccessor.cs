namespace Facet;

/// <summary>
/// Controls the set accessor emitted on all generated properties of a facet.
/// </summary>
public enum PropertySetAccessor
{
    /// <summary>
    /// Preserve the source property's accessor (default).
    /// A <c>set</c> source property stays <c>set</c>; an <c>init</c> source property stays <c>init</c>.
    /// </summary>
    Preserve = 0,

    /// <summary>
    /// Force all generated properties to use <c>{ get; set; }</c>.
    /// Useful for making an otherwise init-only type fully mutable.
    /// </summary>
    Set = 1,

    /// <summary>
    /// Force all generated properties to use <c>{ get; init; }</c>.
    /// Useful for building immutable DTOs that support object-initializer syntax.
    /// </summary>
    Init = 2,
}
