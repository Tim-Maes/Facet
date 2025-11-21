using System;

namespace Facet;

/// <summary>
/// Indicates that this class should be generated based on a source type.
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
    /// This property is mutually exclusive with <see cref="Include"/>.
    /// </summary>
    public string[] Exclude { get; }

    /// <summary>
    /// An array of property or field names to include in the generated class.
    /// When specified, only these properties will be included in the facet.
    /// This property is mutually exclusive with <see cref="Exclude"/>.
    /// </summary>
    public string[]? Include { get; set; }

    /// <summary>
    /// Whether to include public fields from the source type (default: true).
    /// </summary>
    public bool IncludeFields { get; set; } = false;

    /// <summary>
    /// Whether to generate a constructor that accepts the source type and copies over matching members.
    /// </summary>
    public bool GenerateConstructor { get; set; } = true;

    /// <summary>
    /// Whether to generate a parameterless constructor for easier unit testing and object initialization.
    /// When true, a public parameterless constructor will be generated. For record types without existing 
    /// primary constructors, this creates a standard class-like parameterless constructor. For positional records,
    /// the parameterless constructor initializes properties with default values.
    /// </summary>
    public bool GenerateParameterlessConstructor { get; set; } = true;

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
    /// Whether to generate a method to map from the facet type back to the source type.
    /// Default is false. Set to true to enable two-way mapping scenarios.
    /// </summary>
    public bool GenerateToSource { get; set; } = false;

    /// <summary>
    /// Whether to generate a method to map back from the facet type to the source type.
    /// Default is false. Set to true to enable two-way mapping scenarios.
    /// </summary>
    [Obsolete("Use GenerateToSource instead. This property will be removed in a future version.")]
    public bool GenerateBackTo { get => GenerateToSource; set => GenerateToSource = value; }

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
    /// If true, generated files will use the full type name (namespace + containing types)
    /// to avoid collisions. Default is false (shorter file names).
    /// </summary>
    public bool UseFullName { get; set; } = false;

    /// <summary>
    /// If true, all non-nullable properties from the source type will be made nullable in the generated facet.
    /// This is useful for query or patch models where all fields should be optional.
    /// Default is false (properties preserve their original nullability).
    /// </summary>
    public bool NullableProperties { get; set; } = false;

    /// <summary>
    /// An array of nested facet types that represent nested objects within the source type.
    /// When specified, the generator will automatically map properties of the source type to these nested facets.
    /// </summary>
    public Type[]? NestedFacets { get; set; }

    /// <summary>
    /// When true, copies attributes from the source type members to the generated facet members.
    /// Only copies attributes that are valid on the target (excludes internal compiler attributes and non-copiable attributes).
    /// Default is false.
    /// </summary>
    public bool CopyAttributes { get; set; } = false;

    /// <summary>
    /// The maximum depth for nested facet recursion. When set to a positive value, the generator will
    /// limit how deep nested facets can be instantiated to prevent stack overflow with circular references.
    /// A value of 0 means unlimited depth (not recommended - can cause stack overflow).
    /// A value of 1 allows one level of nesting, 2 allows two levels, etc.
    /// Default is 10, which handles most real-world scenarios including deep non-circular nesting.
    /// Set to 0 to disable (use with caution), or increase if you need deeper nesting.
    /// </summary>
    public int MaxDepth { get; set; } = 10;

    /// <summary>
    /// When true, the generator will track object references during facet construction to prevent
    /// infinite recursion when circular references exist in the object graph.
    /// This adds a small runtime overhead (HashSet lookups) but prevents processing the same
    /// object instance multiple times, which is critical for safety with circular references.
    /// Default is true for safety. Set to false only if you're certain your object graphs have no circular references
    /// and you need maximum performance.
    /// </summary>
    public bool PreserveReferences { get; set; } = true;

    /// <summary>
    /// Creates a new FacetAttribute that targets a given source type and excludes specified members.
    /// </summary>
    /// <param name="sourceType">The type to generate from.</param>
    /// <param name="exclude">The names of the properties or fields to exclude.</param>
    public FacetAttribute(Type sourceType, params string[] exclude)
    {
        SourceType = sourceType;
        Exclude = exclude ?? Array.Empty<string>();
        Include = null;
    }
}