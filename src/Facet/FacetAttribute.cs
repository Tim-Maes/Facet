using System;

namespace Facet;

/// <summary>
/// Indicates that this class should be generated based on a source type, optionally excluding properties or including fields.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class FacetAttribute : Attribute
{
    /// <summary>
    /// The type to project from.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// An array of property or field names to exclude from the generated class.
    /// </summary>
    public string[] Exclude { get; }

    /// <summary>
    /// Whether to include public fields from the source type (default: true).
    /// </summary>
    public bool IncludeFields { get; set; } = false;

    /// <summary>
    /// Whether to generate a constructor that accepts the source type and copies over matching members.
    /// </summary>
    public bool GenerateConstructor { get; set; } = true;

    /// <summary>
    /// Optional type that provides custom mapping logic via a static Map(source, target) method.
    /// Must match the signature defined in IFacetMapConfiguration&lt;TSource, TTarget&gt;.
    /// </summary>
    /// <remarks>
    /// The type must define a static method with one of the following signatures:
    /// <c>public static void Map(TSource source, TTarget target)</c> for mutable properties, or
    /// <c>public static TTarget Map(TSource source, TTarget target)</c> for init-only properties and records.
    /// This allows injecting custom projections, formatting, or derived values at compile time.
    /// </remarks>
    public Type? Configuration { get; set; }

    /// <summary>
    /// Whether to generate the static Expression&lt;Func&lt;TSource,TTarget&gt;&gt; Projection.
    /// Default is true so you always get a Projection by default.
    /// </summary>
    public bool GenerateProjection { get; set; } = true;

    /// <summary>
    /// Which facet to generate: Class, Record (class), RecordStruct, or Struct.
    /// When set to Auto, the generator will attempt to infer the kind from the target type declaration.
    /// </summary>
    public FacetKind Kind { get; set; } = FacetKind.Auto;

    /// <summary>
    /// Controls whether generated properties should preserve init-only modifiers from source properties.
    /// When true, properties with init accessors in the source will be generated as init-only in the target.
    /// Defaults to true for record and record struct types, false for class and struct types.
    /// </summary>
    public bool PreserveInitOnlyProperties { get; set; } = false;

    /// <summary>
    /// Controls whether generated properties should preserve required modifiers from source properties.
    /// When true, properties marked as required in the source will be generated as required in the target.
    /// Defaults to true for record and record struct types, false for class and struct types.
    /// </summary>
    public bool PreserveRequiredProperties { get; set; } = false;

    /// <summary>
    /// Creates a new FacetAttribute that targets a given source type and excludes specified members.
    /// </summary>
    /// <param name="sourceType">The type to generate from.</param>
    /// <param name="exclude">The names of the properties or fields to exclude.</param>
    public FacetAttribute(Type sourceType, params string[] exclude)
    {
        SourceType = sourceType;
        Exclude = exclude ?? Array.Empty<string>();
    }
}