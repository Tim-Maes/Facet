namespace Facet.Mapping;

/// <summary>
/// Allows defining custom mapping logic when converting a Facet-generated type back to its source type.
/// Used with <c>ToSourceConfiguration</c> on <see cref="global::Facet.FacetAttribute"/>.
/// </summary>
/// <typeparam name="TFacet">The Facet-generated DTO type</typeparam>
/// <typeparam name="TSource">The original source type</typeparam>
/// <remarks>
/// <para>
/// The <c>Map</c> method is called after all automatically-mapped properties have been copied to the
/// newly-created source object. This lets you override or supplement the reverse mapping for
/// properties that require custom logic (e.g., deserializing a JSON column, computing a derived value).
/// </para>
/// <para>
/// Example:
/// <code>
/// public class UnitDtoToSourceConfig : IFacetToSourceConfiguration&lt;UnitDto, UnitEntity&gt;
/// {
///     public static void Map(UnitDto facet, UnitEntity target)
///     {
///         target.PrinterSettings = facet.PrinterSettings.ToPrinterSettingsAsJson();
///     }
/// }
///
/// [Facet(typeof(UnitEntity),
///     GenerateToSource = true,
///     ToSourceConfiguration = typeof(UnitDtoToSourceConfig))]
/// public partial class UnitDto { ... }
/// </code>
/// </para>
/// </remarks>
public interface IFacetToSourceConfiguration<TFacet, TSource>
{
    static abstract void Map(TFacet facet, TSource target);
}

/// <summary>
/// Instance-based interface for defining custom reverse-mapping logic with dependency injection support.
/// </summary>
/// <typeparam name="TFacet">The Facet-generated DTO type</typeparam>
/// <typeparam name="TSource">The original source type</typeparam>
public interface IFacetToSourceConfigurationInstance<TFacet, TSource>
{
    void Map(TFacet facet, TSource target);
}
