using System;

namespace Facet;

/// <summary>
/// Specifies that a property should be mapped from a different source property or expression.
/// This attribute allows declarative property renaming and simple transformations without
/// requiring a full IFacetMapConfiguration implementation.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="MapFromAttribute"/> can be used in several ways:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>Simple property rename: <c>[MapFrom("FirstName")]</c> maps from source.FirstName</description>
/// </item>
/// <item>
/// <description>Nested property access: <c>[MapFrom("Company.Name")]</c> maps from source.Company.Name</description>
/// </item>
/// <item>
/// <description>Expression: <c>[MapFrom("FirstName + \" \" + LastName")]</c> for computed values</description>
/// </item>
/// </list>
/// <para>
/// When used together with <see cref="FacetAttribute.Configuration"/>, the auto-generated mappings
/// (including MapFrom) are applied first, then the custom mapper is called, allowing it to override
/// any values if needed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Facet(typeof(User))]
/// public partial class UserDto
/// {
///     [MapFrom("FirstName")]
///     public string Name { get; set; }
///
///     [MapFrom("Company.Name")]
///     public string CompanyName { get; set; }
///
///     [MapFrom("FirstName + \" \" + LastName", Reversible = false)]
///     public string FullName { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class MapFromAttribute : Attribute
{
    /// <summary>
    /// The source property name or expression to map from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This can be:
    /// </para>
    /// <list type="bullet">
    /// <item><description>A simple property name: "FirstName"</description></item>
    /// <item><description>A nested property path: "Company.Address.City"</description></item>
    /// <item><description>A C# expression: "FirstName + \" \" + LastName"</description></item>
    /// </list>
    /// <para>
    /// When using expressions, the source variable is implicitly available. For example,
    /// "FirstName" is equivalent to accessing source.FirstName.
    /// </para>
    /// </remarks>
    public string Source { get; }

    /// <summary>
    /// Whether this mapping can be reversed in the ToSource method.
    /// Default is true for simple property mappings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set to false for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Computed expressions that cannot be reversed</description></item>
    /// <item><description>Navigation property paths (e.g., "Company.Name")</description></item>
    /// <item><description>One-way mappings where reverse mapping doesn't make sense</description></item>
    /// </list>
    /// <para>
    /// When false, the property will not be included in the ToSource method output.
    /// </para>
    /// </remarks>
    public bool Reversible { get; set; } = true;

    /// <summary>
    /// Whether to include this mapping in the generated Projection expression.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set to false for mappings that cannot be translated to SQL by Entity Framework Core,
    /// such as method calls or complex expressions that require client-side evaluation.
    /// </para>
    /// <para>
    /// When false, the property will not be included in the static Projection expression,
    /// but will still be mapped in the constructor.
    /// </para>
    /// </remarks>
    public bool IncludeInProjection { get; set; } = true;

    /// <summary>
    /// Creates a new MapFromAttribute that maps from the specified source property or expression.
    /// </summary>
    /// <param name="source">The source property name or expression to map from.</param>
    public MapFromAttribute(string source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }
}
