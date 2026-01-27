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
    /// Whether to include public fields from the source type (default: false).
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
    /// Optional signature to track source entity structure changes.
    /// When set, emits a compile-time warning if the source entity's properties change.
    /// This helps detect unintended API changes when source models are modified.
    /// The signature is a hash computed from property names and types.
    /// </summary>
    public string? SourceSignature { get; set; }

    /// <summary>
    /// An array of flattened types that can be generated from this facet's collection properties.
    /// When specified, the generator will create FlattenTo() methods that unpack collection properties
    /// into multiple rows of the specified type, combining parent properties with each collection item.
    /// The target types should be classes/records that define the properties you want from both
    /// the parent and the collection items.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example usage:
    /// <code>
    /// // Define the entities
    /// public class Data
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///     public ICollection&lt;Extended&gt; Extended { get; set; }
    /// }
    ///
    /// public class Extended
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///     public int DataValue { get; set; }
    /// }
    ///
    /// // Define the facets
    /// [Facet(typeof(Extended))]
    /// public partial class ExtendedDto;
    ///
    /// [Facet(typeof(Data), NestedFacets = [typeof(ExtendedDto)], FlattenTo = [typeof(DataFlattened)])]
    /// public partial class DataDto;
    ///
    /// // Define the flattened target (manually specify all properties you want)
    /// public partial class DataFlattened
    /// {
    ///     // From Data (parent)
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///
    ///     // From Extended (collection item) - prefix to avoid Name collision
    ///     public string ExtendedName { get; set; }
    ///     public int DataValue { get; set; }
    /// }
    /// </code>
    /// This generates a FlattenTo() method on DataDto that returns List&lt;DataFlattened&gt;,
    /// with one row per Extended item, combining Data properties with each Extended item's properties.
    /// </para>
    /// </remarks>
    public Type[]? FlattenTo { get; set; }

    /// <summary>
    /// Optional type that provides custom logic to run BEFORE automatic property mapping.
    /// Must implement IFacetBeforeMapConfiguration&lt;TSource, TTarget&gt; with a static BeforeMap method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The type must define a static method with the signature:
    /// <c>public static void BeforeMap(TSource source, TTarget target)</c>
    /// </para>
    /// <para>
    /// BeforeMap is called before any properties are copied, allowing you to:
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
    ///         if (source == null) throw new ArgumentNullException(nameof(source));
    ///         target.MappedAt = DateTime.UtcNow;
    ///     }
    /// }
    ///
    /// [Facet(typeof(User), BeforeMapConfiguration = typeof(UserBeforeMapConfig))]
    /// public partial class UserDto
    /// {
    ///     public DateTime MappedAt { get; set; }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public Type? BeforeMapConfiguration { get; set; }

    /// <summary>
    /// Optional type that provides custom logic to run AFTER automatic property mapping.
    /// Must implement IFacetAfterMapConfiguration&lt;TSource, TTarget&gt; with a static AfterMap method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The type must define a static method with the signature:
    /// <c>public static void AfterMap(TSource source, TTarget target)</c>
    /// </para>
    /// <para>
    /// AfterMap is called after all properties are copied, allowing you to:
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
    ///         target.FullName = $"{target.FirstName} {target.LastName}";
    ///         target.Age = CalculateAge(source.DateOfBirth);
    ///     }
    /// }
    ///
    /// [Facet(typeof(User), AfterMapConfiguration = typeof(UserAfterMapConfig))]
    /// public partial class UserDto
    /// {
    ///     public string FullName { get; set; }
    ///     public int Age { get; set; }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public Type? AfterMapConfiguration { get; set; }

    /// <summary>
    /// When true, the generated constructor that takes the source type will chain to the 
    /// parameterless constructor using `: this()`. This ensures any custom initialization 
    /// logic in your parameterless constructor runs before property mapping.
    /// Default is false. Set to true when you have initialization logic in your parameterless 
    /// constructor that needs to execute during mapping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is useful when you need to initialize computed properties or run setup logic
    /// that isn't simply copying values from the source.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// public class ModelType
    /// {
    ///     public int MaxValue { get; set; }
    /// }
    ///
    /// [Facet(typeof(ModelType), GenerateParameterlessConstructor = false, ChainToParameterlessConstructor = true)]
    /// public partial class MyType
    /// {
    ///     public int Value { get; set; }
    ///
    ///     public MyType()
    ///     {
    ///         // Custom initialization logic
    ///         Value = 100; // Default value
    ///     }
    /// }
    /// </code>
    /// The generated constructor will be:
    /// <code>
    /// public MyType(ModelType source) : this()
    /// {
    ///     this.MaxValue = source.MaxValue;
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public bool ChainToParameterlessConstructor { get; set; } = false;

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
