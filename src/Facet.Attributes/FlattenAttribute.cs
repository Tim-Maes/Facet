using System;

namespace Facet;

/// <summary>
/// Specifies that the target type should be automatically generated as a flattened projection
/// of the source type, with all nested properties expanded into top-level properties.
/// </summary>
/// <remarks>
/// <para>
/// The Flatten attribute generates all properties automatically by traversing the source type
/// and creating flattened properties for all primitive types, strings, and value types found
/// in nested objects.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class FlattenAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlattenAttribute"/> class.
    /// </summary>
    /// <param name="sourceType">The source type to flatten from.</param>
    public FlattenAttribute(Type sourceType)
    {
        SourceType = sourceType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlattenAttribute"/> class with property exclusions.
    /// </summary>
    /// <param name="sourceType">The source type to flatten from.</param>
    /// <param name="exclude">Property paths to exclude from flattening (e.g., "Address.Country", "Password").</param>
    public FlattenAttribute(Type sourceType, params string[] exclude)
    {
        SourceType = sourceType;
        Exclude = exclude;
    }

    /// <summary>
    /// Gets the source type to flatten from.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// Gets or sets the property paths to exclude from flattening.
    /// Use dot notation to exclude nested paths (e.g., "Address.Country", "User.Password").
    /// </summary>
    public string[]? Exclude { get; set; }

    /// <summary>
    /// Gets or sets the maximum depth to traverse when flattening nested objects.
    /// Default is 3 levels deep. Set to 0 for unlimited depth (not recommended).
    /// </summary>
    public int MaxDepth { get; set; } = 3;

    /// <summary>
    /// Gets or sets the naming strategy for flattened properties.
    /// Default is <see cref="FlattenNamingStrategy.Prefix"/> (e.g., "AddressStreet").
    /// </summary>
    public FlattenNamingStrategy NamingStrategy { get; set; } = FlattenNamingStrategy.Prefix;

    /// <summary>
    /// Gets or sets whether to include fields in addition to properties.
    /// Default is false.
    /// </summary>
    public bool IncludeFields { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to generate a parameterless constructor.
    /// Default is true.
    /// </summary>
    public bool GenerateParameterlessConstructor { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to generate a LINQ projection expression.
    /// Default is true.
    /// </summary>
    public bool GenerateProjection { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use the fully qualified type name in generated file names.
    /// Useful for avoiding file name collisions when multiple types have the same name.
    /// Default is false.
    /// </summary>
    public bool UseFullName { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to ignore nested ID properties (properties named "Id" or ending with "Id").
    /// When true, only the root-level Id property is included; all nested entity IDs are excluded.
    /// This is useful for flattening entities with foreign key relationships.
    /// Default is false.
    /// </summary>
    /// <remarks>
    /// Example: When true, "User.Id" is kept but "User.Company.Id" and "User.CompanyId" are excluded.
    /// </remarks>
    public bool IgnoreNestedIds { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to ignore foreign key clashes when flattening.
    /// When true, if a foreign key property (e.g., "AddressId") exists and a navigation property
    /// (e.g., "Address") is flattened, the nested Id property (Address.Id) will be skipped
    /// since both represent the same data.
    /// Default is false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example: Given Person with AddressId (FK) and Address (navigation property),
    /// when true, only AddressId is included (not Address.Id flattened to AddressId).
    /// </para>
    /// </remarks>
    public bool IgnoreForeignKeyClashes { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to include collection properties in the flattened type.
    /// When true, collection properties (List, Array, IEnumerable, etc.) are included as-is
    /// without flattening their contents. The collection type is preserved exactly as declared
    /// in the source type.
    /// Default is false (collections are skipped).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is useful for API responses where you want a flattened structure for scalar nested
    /// properties, but still need to include collection data without modification.
    /// </para>
    /// </remarks>
    public bool IncludeCollections { get; set; } = false;
}

/// <summary>
/// Specifies the naming strategy for flattened properties.
/// </summary>
public enum FlattenNamingStrategy
{
    /// <summary>
    /// Prefix flattened properties with their parent path.
    /// Example: Address.Street becomes "AddressStreet"
    /// </summary>
    Prefix = 0,

    /// <summary>
    /// Use only the leaf property name without parent path.
    /// Example: Address.Street becomes "Street"
    /// Warning: This can cause name collisions if multiple nested objects have properties with the same name.
    /// </summary>
    LeafOnly = 1,

    /// <summary>
    /// Use leaf property name, but add immediate parent name prefix when collisions occur.
    /// Example: Position.Name and Type.Name become "PositionName" and "TypeName"
    /// Non-colliding properties use leaf names only.
    /// </summary>
    SmartLeaf = 2
}
