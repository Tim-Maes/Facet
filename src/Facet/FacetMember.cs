using System;
using System.Collections.Generic;
using System.Linq;

namespace Facet;

internal sealed class FacetMember : IEquatable<FacetMember>
{
    public string Name { get; }
    public string TypeName { get; }
    public FacetMemberKind Kind { get; }
    public bool IsValueType { get; }
    public bool IsInitOnly { get; }
    public bool IsRequired { get; }
    public bool IsReadOnly { get; }
    public string? XmlDocumentation { get; }
    public bool IsNestedFacet { get; }
    public string? NestedFacetSourceTypeName { get; }
    public IReadOnlyList<string> Attributes { get; }
    public IReadOnlyList<string> AttributeNamespaces { get; }
    public bool IsCollection { get; }
    public string? CollectionWrapper { get; }
    public string? SourceMemberTypeName { get; }

    // MapFrom attribute properties
    public string? MapFromSource { get; }
    public bool MapFromReversible { get; }
    public bool MapFromIncludeInProjection { get; }
    public string SourcePropertyName { get; }
    public bool IsUserDeclared { get; }

    // MapWhen attribute properties
    public IReadOnlyList<string> MapWhenConditions { get; }
    public string? MapWhenDefault { get; }
    public bool MapWhenIncludeInProjection { get; }

    /// <summary>
    /// The default value/initializer expression from the source property (e.g., "= new()" or "= string.Empty").
    /// When set, this will be included in the generated property declaration.
    /// </summary>
    public string? DefaultValue { get; }

    public FacetMember(
        string name,
        string typeName,
        FacetMemberKind kind,
        bool isValueType,
        bool isInitOnly = false,
        bool isRequired = false,
        bool isReadOnly = false,
        string? xmlDocumentation = null,
        bool isNestedFacet = false,
        string? nestedFacetSourceTypeName = null,
        IReadOnlyList<string>? attributes = null,
        bool isCollection = false,
        string? collectionWrapper = null,
        string? sourceMemberTypeName = null,
        string? mapFromSource = null,
        bool mapFromReversible = false,
        bool mapFromIncludeInProjection = true,
        string? sourcePropertyName = null,
        bool isUserDeclared = false,
        IReadOnlyList<string>? mapWhenConditions = null,
        string? mapWhenDefault = null,
        bool mapWhenIncludeInProjection = true,
        IReadOnlyList<string>? attributeNamespaces = null,
        string? defaultValue = null)
    {
        Name = name;
        TypeName = typeName;
        Kind = kind;
        IsValueType = isValueType;
        IsInitOnly = isInitOnly;
        IsRequired = isRequired;
        IsReadOnly = isReadOnly;
        XmlDocumentation = xmlDocumentation;
        IsNestedFacet = isNestedFacet;
        NestedFacetSourceTypeName = nestedFacetSourceTypeName;
        Attributes = attributes ?? Array.Empty<string>();
        AttributeNamespaces = attributeNamespaces ?? Array.Empty<string>();
        IsCollection = isCollection;
        CollectionWrapper = collectionWrapper;
        SourceMemberTypeName = sourceMemberTypeName;
        MapFromSource = mapFromSource;
        MapFromReversible = mapFromReversible;
        MapFromIncludeInProjection = mapFromIncludeInProjection;
        SourcePropertyName = sourcePropertyName ?? name;
        IsUserDeclared = isUserDeclared;
        MapWhenConditions = mapWhenConditions ?? Array.Empty<string>();
        MapWhenDefault = mapWhenDefault;
        MapWhenIncludeInProjection = mapWhenIncludeInProjection;
        DefaultValue = defaultValue;
    }

    public bool Equals(FacetMember? other) =>
        other is not null &&
        Name == other.Name &&
        TypeName == other.TypeName &&
        Kind == other.Kind &&
        IsInitOnly == other.IsInitOnly &&
        IsRequired == other.IsRequired &&
        IsReadOnly == other.IsReadOnly &&
        XmlDocumentation == other.XmlDocumentation &&
        IsNestedFacet == other.IsNestedFacet &&
        NestedFacetSourceTypeName == other.NestedFacetSourceTypeName &&
        IsCollection == other.IsCollection &&
        CollectionWrapper == other.CollectionWrapper &&
        SourceMemberTypeName == other.SourceMemberTypeName &&
        MapFromSource == other.MapFromSource &&
        MapFromReversible == other.MapFromReversible &&
        MapFromIncludeInProjection == other.MapFromIncludeInProjection &&
        SourcePropertyName == other.SourcePropertyName &&
        IsUserDeclared == other.IsUserDeclared &&
        MapWhenDefault == other.MapWhenDefault &&
        MapWhenIncludeInProjection == other.MapWhenIncludeInProjection &&
        DefaultValue == other.DefaultValue &&
        Attributes.SequenceEqual(other.Attributes) &&
        AttributeNamespaces.SequenceEqual(other.AttributeNamespaces) &&
        MapWhenConditions.SequenceEqual(other.MapWhenConditions);

    public override bool Equals(object? obj) => obj is FacetMember other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Name.GetHashCode();
            hash = hash * 31 + TypeName.GetHashCode();
            hash = hash * 31 + Kind.GetHashCode();
            hash = hash * 31 + IsInitOnly.GetHashCode();
            hash = hash * 31 + IsRequired.GetHashCode();
            hash = hash * 31 + IsReadOnly.GetHashCode();
            hash = hash * 31 + (XmlDocumentation?.GetHashCode() ?? 0);
            hash = hash * 31 + IsNestedFacet.GetHashCode();
            hash = hash * 31 + (NestedFacetSourceTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + IsCollection.GetHashCode();
            hash = hash * 31 + (CollectionWrapper?.GetHashCode() ?? 0);
            hash = hash * 31 + (SourceMemberTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (MapFromSource?.GetHashCode() ?? 0);
            hash = hash * 31 + MapFromReversible.GetHashCode();
            hash = hash * 31 + MapFromIncludeInProjection.GetHashCode();
            hash = hash * 31 + SourcePropertyName.GetHashCode();
            hash = hash * 31 + IsUserDeclared.GetHashCode();
            hash = hash * 31 + (MapWhenDefault?.GetHashCode() ?? 0);
            hash = hash * 31 + MapWhenIncludeInProjection.GetHashCode();
            hash = hash * 31 + (DefaultValue?.GetHashCode() ?? 0);
            hash = hash * 31 + Attributes.Count.GetHashCode();
            foreach (var attr in Attributes)
                hash = hash * 31 + (attr?.GetHashCode() ?? 0);
            hash = hash * 31 + AttributeNamespaces.Count.GetHashCode();
            foreach (var ns in AttributeNamespaces)
                hash = hash * 31 + (ns?.GetHashCode() ?? 0);
            hash = hash * 31 + MapWhenConditions.Count.GetHashCode();
            foreach (var cond in MapWhenConditions)
                hash = hash * 31 + (cond?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

internal enum FacetMemberKind
{
    Property,
    Field
}
