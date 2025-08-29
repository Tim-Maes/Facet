using System;

namespace Facet;

internal sealed class FacetPrimaryConstuctorParameter : IEquatable<FacetPrimaryConstuctorParameter>
{
    public string Name { get; }
    public string TypeName { get; }

    public FacetPrimaryConstuctorParameter(string name, string typeName)
    {
        Name = name;
        TypeName = typeName;
    }

    public bool Equals(FacetPrimaryConstuctorParameter? other) =>
        other is not null &&
        Name == other.Name &&
        TypeName == other.TypeName;

    public override bool Equals(object? obj) => obj is FacetPrimaryConstuctorParameter other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Name.GetHashCode();
            hash = hash * 31 + TypeName.GetHashCode();
            return hash;
        }
    }
}
