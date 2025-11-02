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
/// <para>
/// Example:
/// <code>
/// public class Person
/// {
///     public string FirstName { get; set; }
///     public Address Address { get; set; }
/// }
///
/// public class Address
/// {
///     public string Street { get; set; }
///     public string City { get; set; }
/// }
///
/// [Flatten(typeof(Person))]
/// public partial class PersonDto
/// {
///     // Generator automatically creates:
///     // public string FirstName { get; set; }
///     // public string AddressStreet { get; set; }
///     // public string AddressCity { get; set; }
/// }
/// </code>
/// </para>
/// <para>
/// <strong>Note:</strong> Flattened types do not support BackTo methods as unflattening
/// is inherently ambiguous and error-prone. Flattening is a one-way operation intended
/// for read-only projections like API responses.
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
    LeafOnly = 1
}
