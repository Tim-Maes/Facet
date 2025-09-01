using System;

namespace Facet;

internal sealed class FacetMember : IEquatable<FacetMember>
{
    public string Name { get; }
    public string TypeName { get; }
    public FacetMemberKind Kind { get; }
    public bool IsInitOnly { get; }
    public bool IsRequired { get; }
    public string? XmlDocumentation { get; }

    public FacetMember(string name, string typeName, FacetMemberKind kind, bool isInitOnly = false, bool isRequired = false, string? xmlDocumentation = null)
    {
        Name = name;
        TypeName = typeName;
        Kind = kind;
        IsInitOnly = isInitOnly;
        IsRequired = isRequired;
        XmlDocumentation = xmlDocumentation;
    }

    public bool Equals(FacetMember? other) =>
        other is not null &&
        Name == other.Name &&
        TypeName == other.TypeName &&
        Kind == other.Kind &&
        IsInitOnly == other.IsInitOnly &&
        IsRequired == other.IsRequired &&
        XmlDocumentation == other.XmlDocumentation;

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
            hash = hash * 31 + (XmlDocumentation?.GetHashCode() ?? 0);
            return hash;
        }
    }
}


internal enum FacetMemberKind
{
    Property,
    Field
}