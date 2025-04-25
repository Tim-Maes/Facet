using System;

namespace Facet;

internal sealed class FacetMember : IEquatable<FacetMember>
{
    public string Name { get; }
    public string TypeName { get; }
    public FacetMemberKind Kind { get; }

    public FacetMember(string name, string typeName, FacetMemberKind kind)
    {
        Name = name;
        TypeName = typeName;
        Kind = kind;
    }

    public bool Equals(FacetMember? other) =>
        other is not null &&
        Name == other.Name &&
        TypeName == other.TypeName &&
        Kind == other.Kind;

    public override bool Equals(object? obj) => obj is FacetMember other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Name.GetHashCode();
            hash = hash * 31 + TypeName.GetHashCode();
            hash = hash * 31 + Kind.GetHashCode();
            return hash;
        }
    }
}


internal enum FacetMemberKind
{
    Property,
    Field
}