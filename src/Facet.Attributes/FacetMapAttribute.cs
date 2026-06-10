using System;

namespace Facet;

/// <summary>
/// Generates extension methods for mapping between a source entity type and an externally-defined target DTO type.
/// Place this attribute on a <c>static partial class</c> to generate mapping extensions in that class.
/// Unlike <see cref="FacetAttribute"/>, this does not generate property declarations on the target type,
/// making it suitable for DDD architectures where the DTO lives in a separate shared/contracts assembly.
/// </summary>
/// <remarks>
/// <para>
/// Example usage:
/// <code>
/// // In your shared/contracts project (no Facet dependency):
/// public class UserDto
/// {
///     public int Id { get; set; }
///     public string FirstName { get; set; }
///     public string Email { get; set; }
/// }
///
/// // In your domain project (references shared project + Facet):
/// [FacetMap(typeof(User), typeof(UserDto), GenerateToSource = true)]
/// public static partial class UserMappings { }
///
/// // Generated extension methods:
/// // user.ToUserDto()
/// // userDto.ToUser()
/// // UserMappings.UserDtoProjection (Expression for LINQ/EF)
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class FacetMapAttribute : Attribute
{
    /// <summary>
    /// The source entity type to map from.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// The target DTO type to map to. This type should already exist (e.g., in a shared project).
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Property names to exclude from mapping.
    /// These are names of properties on the source type that should not be mapped to the target.
    /// </summary>
    public string[] Exclude { get; }

    /// <summary>
    /// Property names to include in mapping.
    /// When specified, only these source properties will be mapped to the target.
    /// Mutually exclusive with <see cref="Exclude"/>.
    /// </summary>
    public string[]? Include { get; set; }

    /// <summary>
    /// Whether to generate a reverse mapping extension method (target-to-source).
    /// Default is false.
    /// </summary>
    public bool GenerateToSource { get; set; } = false;

    /// <summary>
    /// Whether to generate a static projection expression for LINQ/EF Core queries.
    /// Default is true.
    /// </summary>
    public bool GenerateProjection { get; set; } = true;

    /// <summary>
    /// Optional type that provides custom mapping logic via a static Map(source, target) method.
    /// Must match the signature defined in IFacetMapConfiguration&lt;TSource, TTarget&gt;.
    /// </summary>
    public Type? Configuration { get; set; }

    /// <summary>
    /// Optional type that provides custom reverse-mapping logic via a static Map(target, source) method.
    /// </summary>
    public Type? ToSourceConfiguration { get; set; }

    /// <summary>
    /// Optional type that provides custom logic to run BEFORE automatic property mapping.
    /// Must implement IFacetBeforeMapConfiguration&lt;TSource, TTarget&gt; with a static BeforeMap method.
    /// </summary>
    public Type? BeforeMapConfiguration { get; set; }

    /// <summary>
    /// Optional type that provides custom logic to run AFTER automatic property mapping.
    /// Must implement IFacetAfterMapConfiguration&lt;TSource, TTarget&gt; with a static AfterMap method.
    /// </summary>
    public Type? AfterMapConfiguration { get; set; }

    /// <summary>
    /// The maximum depth for nested object mapping to prevent infinite recursion.
    /// Default is 10.
    /// </summary>
    public int MaxDepth { get; set; } = 10;

    /// <summary>
    /// Overrides the collection type used for all mapped collection properties.
    /// Use an open generic type like <c>typeof(List&lt;&gt;)</c>.
    /// </summary>
    public Type? CollectionTargetType { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FacetMapAttribute"/> class.
    /// </summary>
    /// <param name="sourceType">The source entity type.</param>
    /// <param name="targetType">The target DTO type (must already exist).</param>
    /// <param name="exclude">Property names to exclude from mapping.</param>
    public FacetMapAttribute(Type sourceType, Type targetType, params string[] exclude)
    {
        SourceType = sourceType;
        TargetType = targetType;
        Exclude = exclude;
    }
}
