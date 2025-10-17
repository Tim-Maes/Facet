using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet;

internal sealed class FacetTargetModel : IEquatable<FacetTargetModel>
{
    public string Name { get; }
    public string? Namespace { get; }
    public string FullName { get; }
    public TypeKind TypeKind { get; }
    public bool IsRecord { get; }
    public bool GenerateConstructor { get; }
    public bool GenerateParameterlessConstructor { get; }
    public bool GenerateExpressionProjection { get; }
    public bool GenerateBackTo { get; }
    public string SourceTypeName { get; }
    public string? ConfigurationTypeName { get; }
    public ImmutableArray<FacetMember> Members { get; }
    public bool HasExistingPrimaryConstructor { get; }
    public bool SourceHasPositionalConstructor { get; }
    public string? TypeXmlDocumentation { get; }
    public ImmutableArray<string> ContainingTypes { get; }
    public bool UseFullName { get; }
    public ImmutableArray<FacetMember> ExcludedRequiredMembers { get; }
    public bool NullableProperties { get; }
    public bool CopyAttributes { get; }

    public FacetTargetModel(
        string name,
        string? @namespace,
        string fullName,
        TypeKind typeKind,
        bool isRecord,
        bool generateConstructor,
        bool generateParameterlessConstructor,
        bool generateExpressionProjection,
        bool generateBackTo,
        string sourceTypeName,
        string? configurationTypeName,
        ImmutableArray<FacetMember> members,
        bool hasExistingPrimaryConstructor = false,
        bool sourceHasPositionalConstructor = false,
        string? typeXmlDocumentation = null,
        ImmutableArray<string> containingTypes = default,
        bool useFullName = false,
        ImmutableArray<FacetMember> excludedRequiredMembers = default,
        bool nullableProperties = false,
        bool copyAttributes = false)
    {
        Name = name;
        Namespace = @namespace;
        FullName = fullName;
        TypeKind = typeKind;
        IsRecord = isRecord;
        GenerateConstructor = generateConstructor;
        GenerateParameterlessConstructor = generateParameterlessConstructor;
        GenerateExpressionProjection = generateExpressionProjection;
        GenerateBackTo = generateBackTo;
        SourceTypeName = sourceTypeName;
        ConfigurationTypeName = configurationTypeName;
        Members = members;
        HasExistingPrimaryConstructor = hasExistingPrimaryConstructor;
        SourceHasPositionalConstructor = sourceHasPositionalConstructor;
        TypeXmlDocumentation = typeXmlDocumentation;
        ContainingTypes = containingTypes.IsDefault ? ImmutableArray<string>.Empty : containingTypes;
        UseFullName = useFullName;
        ExcludedRequiredMembers = excludedRequiredMembers.IsDefault ? ImmutableArray<FacetMember>.Empty : excludedRequiredMembers;
        NullableProperties = nullableProperties;
        CopyAttributes = copyAttributes;
    }

    public bool Equals(FacetTargetModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name == other.Name
            && Namespace == other.Namespace
            && FullName == other.FullName
            && TypeKind == other.TypeKind
            && IsRecord == other.IsRecord
            && GenerateConstructor == other.GenerateConstructor
            && GenerateParameterlessConstructor == other.GenerateParameterlessConstructor
            && GenerateExpressionProjection == other.GenerateExpressionProjection
            && SourceTypeName == other.SourceTypeName
            && ConfigurationTypeName == other.ConfigurationTypeName
            && HasExistingPrimaryConstructor == other.HasExistingPrimaryConstructor
            && SourceHasPositionalConstructor == other.SourceHasPositionalConstructor
            && TypeXmlDocumentation == other.TypeXmlDocumentation
            && Members.SequenceEqual(other.Members)
            && ContainingTypes.SequenceEqual(other.ContainingTypes)
            && ExcludedRequiredMembers.SequenceEqual(other.ExcludedRequiredMembers)
            && UseFullName == other.UseFullName
            && NullableProperties == other.NullableProperties
            && CopyAttributes == other.CopyAttributes;
    }

    public override bool Equals(object? obj) => obj is FacetTargetModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (Name?.GetHashCode() ?? 0);
            hash = hash * 31 + (Namespace?.GetHashCode() ?? 0);
            hash = hash * 31 + (FullName?.GetHashCode() ?? 0);
            hash = hash * 31 + TypeKind.GetHashCode();
            hash = hash * 31 + IsRecord.GetHashCode();
            hash = hash * 31 + GenerateConstructor.GetHashCode();
            hash = hash * 31 + GenerateParameterlessConstructor.GetHashCode();
            hash = hash * 31 + GenerateExpressionProjection.GetHashCode();
            hash = hash * 31 + (SourceTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (ConfigurationTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + HasExistingPrimaryConstructor.GetHashCode();
            hash = hash * 31 + SourceHasPositionalConstructor.GetHashCode();
            hash = hash * 31 + (TypeXmlDocumentation?.GetHashCode() ?? 0);
            hash = hash * 31 + UseFullName.GetHashCode();
            hash = hash * 31 + NullableProperties.GetHashCode();
            hash = hash * 31 + CopyAttributes.GetHashCode();
            hash = hash * 31 + Members.Length.GetHashCode();

            foreach (var member in Members)
                hash = hash * 31 + member.GetHashCode();

            foreach (var containingType in ContainingTypes)
                hash = hash * 31 + (containingType?.GetHashCode() ?? 0);

            foreach (var excludedMember in ExcludedRequiredMembers)
                hash = hash * 31 + excludedMember.GetHashCode();

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