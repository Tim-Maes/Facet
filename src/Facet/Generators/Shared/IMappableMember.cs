using System.Collections.Generic;

namespace Facet.Generators.Shared;

/// <summary>
/// Common interface for member metadata used by the shared expression builder.
/// Implemented by both <c>FacetMember</c> and <c>FacetMapMember</c> to allow
/// unified code generation for property mapping expressions.
/// </summary>
internal interface IMappableMember
{
    /// <summary>The name of the member on the target/facet type.</summary>
    string Name { get; }

    /// <summary>The fully-qualified type name of the member (including nullable annotation).</summary>
    string TypeName { get; }

    /// <summary>The property name on the source type to map from.</summary>
    string SourcePropertyName { get; }

    /// <summary>Whether the member's type is a value type.</summary>
    bool IsValueType { get; }

    /// <summary>Whether this member is a nested facet that requires recursive mapping.</summary>
    bool IsNestedFacet { get; }

    /// <summary>The source type name of the nested facet (the entity type the nested facet maps from).</summary>
    string? NestedFacetSourceTypeName { get; }

    /// <summary>Whether this member is a collection type.</summary>
    bool IsCollection { get; }

    /// <summary>The collection wrapper kind (e.g., "List", "array", "ImmutableArray").</summary>
    string? CollectionWrapper { get; }

    /// <summary>
    /// The original source collection wrapper before any override was applied.
    /// Used by ToSource() to produce the correct source collection type.
    /// Null when no override was applied.
    /// </summary>
    string? SourceCollectionWrapper { get; }

    /// <summary>The fully-qualified type name of the source member (for nullable-to-non-nullable conversions).</summary>
    string? SourceMemberTypeName { get; }

    /// <summary>A custom mapping expression (e.g., "x => x.Foo.Bar"). Null for default property mapping.</summary>
    string? MapFromSource { get; }

    /// <summary>Whether this member's mapping is reversible (used in ToSource generation).</summary>
    bool MapFromReversible { get; }

    /// <summary>Whether this member should be included in the projection expression.</summary>
    bool MapFromIncludeInProjection { get; }

    /// <summary>Conditional expressions that gate whether the mapping is applied.</summary>
    IReadOnlyList<string> MapWhenConditions { get; }

    /// <summary>Default value when MapWhen condition is false.</summary>
    string? MapWhenDefault { get; }

    /// <summary>Whether the source property has an init-only setter.</summary>
    bool IsSourceInitOnly { get; }

    /// <summary>Whether this member's type was converted from an enum type.</summary>
    bool IsEnumConversion { get; }

    /// <summary>The original fully-qualified enum type name before conversion.</summary>
    string? OriginalEnumTypeName { get; }
}
