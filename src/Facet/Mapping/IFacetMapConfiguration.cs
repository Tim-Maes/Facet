namespace Facet.Mapping;

/// <summary>
/// Allows defining custom mapping logic between a source and target Facet-generated type.
/// </summary>
/// <typeparam name="TSource">The source type</typeparam>
/// <typeparam name="TTarget">The target Facet type</typeparam>
public interface IFacetMapConfiguration<TSource, TTarget>
{
    static abstract void Map(TSource source, TTarget target);
}