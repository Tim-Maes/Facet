﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet;

internal sealed class FacetTargetModel : IEquatable<FacetTargetModel>
{
    public string Name { get; }
    public string? Namespace { get; }
    public FacetKind Kind { get; }
    public bool GenerateConstructor { get; }
    public string SourceTypeName { get; }
    public string? ConfigurationTypeName { get; }
    public ImmutableArray<FacetMember> Members { get; }

    public FacetTargetModel(
        string name,
        string? @namespace,
        FacetKind kind,
        bool generateConstructor,
        string sourceTypeName,
        string? configurationTypeName,
        ImmutableArray<FacetMember> members)
    {
        Name = name;
        Namespace = @namespace;
        Kind = kind;
        GenerateConstructor = generateConstructor;
        SourceTypeName = sourceTypeName;
        ConfigurationTypeName = configurationTypeName;
        Members = members;
    }

    public bool Equals(FacetTargetModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name == other.Name
            && Namespace == other.Namespace
            && Kind == other.Kind
            && GenerateConstructor == other.GenerateConstructor
            && SourceTypeName == other.SourceTypeName
            && ConfigurationTypeName == other.ConfigurationTypeName
            && Members.SequenceEqual(other.Members);
    }

    public override bool Equals(object? obj) => obj is FacetTargetModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (Name?.GetHashCode() ?? 0);
            hash = hash * 31 + (Namespace?.GetHashCode() ?? 0);
            hash = hash * 31 + Kind.GetHashCode();
            hash = hash * 31 + GenerateConstructor.GetHashCode();
            hash = hash * 31 + (SourceTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (ConfigurationTypeName?.GetHashCode() ?? 0);

            foreach (var member in Members)
                hash = hash * 31 + member.GetHashCode();

            return hash;
        }
    }

    internal static IEqualityComparer<FacetTargetModel> Comparer { get; } = new FacetTargetModelEqualityComparer();

    internal sealed class FacetTargetModelEqualityComparer : IEqualityComparer<FacetTargetModel>
    {
        public bool Equals(FacetTargetModel? x, FacetTargetModel? y) => x?.Equals(y) ?? y is null;
        public int GetHashCode(FacetTargetModel obj) => obj.GetHashCode();
    }

}