using System;

namespace Facet;

/// <summary>
/// Indicates that this class should be generated as a wrapper that delegates to a source type instance.
/// Unlike <see cref="FacetAttribute"/> which creates value copies, Wrapper creates a reference-based
/// delegate pattern where property access forwards to the wrapped source object.
/// </summary>
/// <remarks>
/// Use cases include:
/// - Decorator pattern: Add behavior/validation without modifying domain models
/// - Facade pattern: Hide sensitive properties while exposing others
/// - ViewModel layers: Expose subset of domain model properties with reference semantics
/// - Memory efficiency: Avoid duplicating large object graphs
///
/// Note: Wrappers maintain a reference to the source object, so changes to wrapper properties
/// affect the underlying source object.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class WrapperAttribute : Attribute
{
    /// <summary>
    /// The type to wrap and delegate to.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// An array of property or field names to exclude from the generated wrapper.
    /// This property is mutually exclusive with <see cref="Include"/>.
    /// </summary>
    public string[] Exclude { get; }

    /// <summary>
    /// An array of property or field names to include in the generated wrapper.
    /// When specified, only these properties will be included.
    /// This property is mutually exclusive with <see cref="Exclude"/>.
    /// </summary>
    public string[]? Include { get; set; }

    /// <summary>
    /// Whether to include public fields from the source type (default: false).
    /// </summary>
    public bool IncludeFields { get; set; } = false;

    /// <summary>
    /// When true, generates read-only properties (getters only) that cannot modify the source object.
    /// When false (default), generates mutable properties with both getters and setters.
    /// Useful for creating immutable facades or read-only views of domain objects.
    /// </summary>
    public bool ReadOnly { get; set; } = false;

    /// <summary>
    /// An array of nested wrapper types for complex/nested properties.
    /// When specified, properties with matching types will be automatically wrapped.
    /// </summary>
    public Type[]? NestedWrappers { get; set; }

    /// <summary>
    /// Additional attributes that <c>CopyAttributes</c> must not copy, on top of the built-in list
    /// of persistence attributes. See <see cref="FacetAttribute.CopyAttributesExclude"/>.
    /// </summary>
    /// <summary>
    /// Which attributes <c>CopyAttributes</c> carries across
    /// (default: <see cref="AttributeCopyMode.Preserve"/>, the behaviour Facet had before this
    /// existed). Set <see cref="AttributeCopyMode.ContractSafe"/> to skip attributes whose
    /// assembly is a package the consumer may not have. FAC109 reports the ones that would be.
    /// </summary>
    public AttributeCopyMode CopyAttributesMode { get; set; } = AttributeCopyMode.Preserve;

    public string[]? CopyAttributesExclude { get; set; }

    /// <summary>
    /// Attributes that <c>CopyAttributes</c> must copy even though something would otherwise
    /// exclude them. See <see cref="FacetAttribute.CopyAttributesInclude"/>.
    /// </summary>
    public string[]? CopyAttributesInclude { get; set; }

    /// <summary>
    /// When true, copies attributes from the source type members to the generated wrapper members.
    /// Only copies attributes that are valid on the target (excludes internal compiler attributes and non-copiable attributes).
    /// Default is false.
    /// </summary>
    public bool CopyAttributes { get; set; } = false;

    /// <summary>
    /// If true, generated files will use the full type name (namespace + containing types)
    /// to avoid collisions. Default is false (shorter file names).
    /// </summary>
    public bool UseFullName { get; set; } = false;

    /// <summary>
    /// Creates a new WrapperAttribute that targets a given source type and excludes specified members.
    /// </summary>
    /// <param name="sourceType">The type to wrap and delegate to.</param>
    /// <param name="exclude">The names of the properties or fields to exclude.</param>
    public WrapperAttribute(Type sourceType, params string[] exclude)
    {
        SourceType = sourceType;
        Exclude = exclude ?? Array.Empty<string>();
        Include = null;
    }
}
