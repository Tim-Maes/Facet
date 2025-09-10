using System;
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
    public bool GenerateParameterlessConstructor { get; }
    public bool GenerateExpressionProjection { get; }
    public string SourceTypeName { get; }
    public string? ConfigurationTypeName { get; }
    public ImmutableArray<FacetMember> Members { get; }
    public bool HasExistingPrimaryConstructor { get; }
    public string? TypeXmlDocumentation { get; }
    public ImmutableArray<string> ContainingTypes { get; }
    public string? OutputPath { get; }

    public FacetTargetModel(
        string name,
        string? @namespace,
        FacetKind kind,
        bool generateConstructor,
        bool generateParameterlessConstructor,
        bool generateExpressionProjection,
        string sourceTypeName,
        string? configurationTypeName,
        ImmutableArray<FacetMember> members,
        bool hasExistingPrimaryConstructor = false,
        string? typeXmlDocumentation = null,
        ImmutableArray<string> containingTypes = default,
        string? outputPath = null)
    {
        Name = name;
        Namespace = @namespace;
        Kind = kind;
        GenerateConstructor = generateConstructor;
        GenerateParameterlessConstructor = generateParameterlessConstructor;
        GenerateExpressionProjection = generateExpressionProjection;
        SourceTypeName = sourceTypeName;
        ConfigurationTypeName = configurationTypeName;
        Members = members;
        HasExistingPrimaryConstructor = hasExistingPrimaryConstructor;
        TypeXmlDocumentation = typeXmlDocumentation;
        ContainingTypes = containingTypes.IsDefault ? ImmutableArray<string>.Empty : containingTypes;
        OutputPath = outputPath;
    }

    public bool Equals(FacetTargetModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name == other.Name
            && Namespace == other.Namespace
            && Kind == other.Kind
            && GenerateConstructor == other.GenerateConstructor
            && GenerateParameterlessConstructor == other.GenerateParameterlessConstructor
            && GenerateExpressionProjection == other.GenerateExpressionProjection
            && SourceTypeName == other.SourceTypeName
            && ConfigurationTypeName == other.ConfigurationTypeName
            && HasExistingPrimaryConstructor == other.HasExistingPrimaryConstructor
            && TypeXmlDocumentation == other.TypeXmlDocumentation
            && OutputPath == other.OutputPath
            && Members.SequenceEqual(other.Members)
            && ContainingTypes.SequenceEqual(other.ContainingTypes);
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
            hash = hash * 31 + GenerateParameterlessConstructor.GetHashCode();
            hash = hash * 31 + GenerateExpressionProjection.GetHashCode();
            hash = hash * 31 + (SourceTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (ConfigurationTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + HasExistingPrimaryConstructor.GetHashCode();
            hash = hash * 31 + (TypeXmlDocumentation?.GetHashCode() ?? 0);
            hash = hash * 31 + (OutputPath?.GetHashCode() ?? 0);

            foreach (var member in Members)
                hash = hash * 31 + member.GetHashCode();

            foreach (var containingType in ContainingTypes)
                hash = hash * 31 + (containingType?.GetHashCode() ?? 0);

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