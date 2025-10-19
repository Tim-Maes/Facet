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
    public bool IsCollection { get; }
    public string? CollectionWrapper { get; }

    public FacetMember(string name, string typeName, FacetMemberKind kind, bool isValueType, bool isInitOnly = false, bool isRequired = false, bool isReadOnly = false, string? xmlDocumentation = null, bool isNestedFacet = false, string? nestedFacetSourceTypeName = null, IReadOnlyList<string>? attributes = null, bool isCollection = false, string? collectionWrapper = null)
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
        IsCollection = isCollection;
        CollectionWrapper = collectionWrapper;
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
        Attributes.SequenceEqual(other.Attributes);

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
            hash = hash * 31 + Attributes.Count.GetHashCode();
            foreach (var attr in Attributes)
                hash = hash * 31 + (attr?.GetHashCode() ?? 0);
            return hash;
        }
    }
}


internal enum FacetMemberKind
{
    Property,
    Field
}
