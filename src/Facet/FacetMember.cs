using System;

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
    public bool IsChildFacet { get; }
    public string? ChildFacetSourceTypeName { get; }

    public FacetMember(string name, string typeName, FacetMemberKind kind, bool isValueType, bool isInitOnly = false, bool isRequired = false, bool isReadOnly = false, string? xmlDocumentation = null, bool isChildFacet = false, string? childFacetSourceTypeName = null)
    {
        Name = name;
        TypeName = typeName;
        Kind = kind;
        IsValueType = isValueType;
        IsInitOnly = isInitOnly;
        IsRequired = isRequired;
        IsReadOnly = isReadOnly;
        XmlDocumentation = xmlDocumentation;
        IsChildFacet = isChildFacet;
        ChildFacetSourceTypeName = childFacetSourceTypeName;
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
        IsChildFacet == other.IsChildFacet &&
        ChildFacetSourceTypeName == other.ChildFacetSourceTypeName;

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
            hash = hash * 31 + IsChildFacet.GetHashCode();
            hash = hash * 31 + (ChildFacetSourceTypeName?.GetHashCode() ?? 0);
            return hash;
        }
    }
}


internal enum FacetMemberKind
{
    Property,
    Field
}
