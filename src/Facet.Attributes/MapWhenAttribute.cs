using System;

namespace Facet;

/// <summary>
/// Specifies that a property should only be mapped when a condition is met.
/// The condition is evaluated against the source object's properties.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="MapWhenAttribute"/> allows conditional property mapping based on source values:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>Boolean check: <c>[MapWhen("IsActive")]</c></description>
/// </item>
/// <item>
/// <description>Equality: <c>[MapWhen("Status == OrderStatus.Completed")]</c></description>
/// </item>
/// <item>
/// <description>Null check: <c>[MapWhen("Email != null")]</c></description>
/// </item>
/// <item>
/// <description>Comparison: <c>[MapWhen("Age >= 18")]</c></description>
/// </item>
/// </list>
/// <para>
/// When the condition is false, the property is set to its default value (or the specified <see cref="Default"/>).
/// Multiple <see cref="MapWhenAttribute"/>s on the same property are combined with AND logic.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Facet(typeof(Order))]
/// public partial class OrderDto
/// {
///     public OrderStatus Status { get; set; }
///
///     [MapWhen("Status == OrderStatus.Completed")]
///     public DateTime? CompletedAt { get; set; }
///
///     [MapWhen("Price != null", Default = 0)]
///     public decimal Price { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class MapWhenAttribute : Attribute
{
    /// <summary>
    /// The condition expression to evaluate against the source object.
    /// Uses C# expression syntax with source property names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Supported operators:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Comparison: ==, !=, &lt;, &gt;, &lt;=, &gt;=</description></item>
    /// <item><description>Logical: &amp;&amp;, ||, !</description></item>
    /// <item><description>Null: ??, ?.</description></item>
    /// </list>
    /// <para>
    /// Property names refer to source object properties. For example, "IsActive"
    /// checks source.IsActive.
    /// </para>
    /// </remarks>
    public string Condition { get; }

    /// <summary>
    /// Default value when the condition is false.
    /// If not specified, uses default(T) for value types or null for reference types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For non-nullable value types, you should specify a default value to avoid
    /// compilation errors. For example:
    /// </para>
    /// <code>
    /// [MapWhen("HasPrice", Default = 0)]
    /// public decimal Price { get; set; }
    /// </code>
    /// </remarks>
    public object? Default { get; set; }

    /// <summary>
    /// Whether to include this condition in the generated Projection expression.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set to false for conditions that cannot be translated to SQL by Entity Framework Core.
    /// When false, the condition will only be evaluated in the constructor, not in LINQ projections.
    /// </para>
    /// </remarks>
    public bool IncludeInProjection { get; set; } = true;

    /// <summary>
    /// Creates a new MapWhenAttribute with the specified condition.
    /// </summary>
    /// <param name="condition">The condition expression to evaluate against the source object.</param>
    public MapWhenAttribute(string condition)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }
}
