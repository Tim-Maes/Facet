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
    public string Accessibility { get; }
    public bool GenerateConstructor { get; }
    public bool GenerateParameterlessConstructor { get; }
    public bool ChainToParameterlessConstructor { get; }
    public bool GenerateExpressionProjection { get; }
    public bool GenerateToSource { get; }
    public string SourceTypeName { get; }
    public ImmutableArray<string> SourceContainingTypes { get; }
    public string? ConfigurationTypeName { get; }
    public string? BeforeMapConfigurationTypeName { get; }
    public string? AfterMapConfigurationTypeName { get; }
    public ImmutableArray<FacetMember> Members { get; }
    public bool HasExistingPrimaryConstructor { get; }
    public bool SourceHasPositionalConstructor { get; }
    public string? TypeXmlDocumentation { get; }
    public ImmutableArray<string> ContainingTypes { get; }
    public bool UseFullName { get; }
    public ImmutableArray<FacetMember> ExcludedRequiredMembers { get; }
    public bool NullableProperties { get; }
    public bool CopyAttributes { get; }
    public int MaxDepth { get; }
    public bool PreserveReferences { get; }
    public ImmutableArray<string> BaseClassMemberNames { get; }
    public ImmutableArray<string> FlattenToTypes { get; }

    public FacetTargetModel(
        string name,
        string? @namespace,
        string fullName,
        TypeKind typeKind,
        bool isRecord,
        string accessibility,
        bool generateConstructor,
        bool generateParameterlessConstructor,
        bool generateExpressionProjection,
        bool generateToSource,
        string sourceTypeName,
        ImmutableArray<string> sourceContainingTypes,
        string? configurationTypeName,
        ImmutableArray<FacetMember> members,
        bool hasExistingPrimaryConstructor = false,
        bool sourceHasPositionalConstructor = false,
        string? typeXmlDocumentation = null,
        ImmutableArray<string> containingTypes = default,
        bool useFullName = false,
        ImmutableArray<FacetMember> excludedRequiredMembers = default,
        bool nullableProperties = false,
        bool copyAttributes = false,
        int maxDepth = 0,
        bool preserveReferences = false,
        ImmutableArray<string> baseClassMemberNames = default,
        ImmutableArray<string> flattenToTypes = default,
        string? beforeMapConfigurationTypeName = null,
        string? afterMapConfigurationTypeName = null,
        bool chainToParameterlessConstructor = false)
    {
        Name = name;
        Namespace = @namespace;
        FullName = fullName;
        TypeKind = typeKind;
        IsRecord = isRecord;
        Accessibility = accessibility;
        GenerateConstructor = generateConstructor;
        GenerateParameterlessConstructor = generateParameterlessConstructor;
        ChainToParameterlessConstructor = chainToParameterlessConstructor;
        GenerateExpressionProjection = generateExpressionProjection;
        GenerateToSource = generateToSource;
        SourceTypeName = sourceTypeName;
        SourceContainingTypes = sourceContainingTypes.IsDefault ? ImmutableArray<string>.Empty : sourceContainingTypes;
        ConfigurationTypeName = configurationTypeName;
        BeforeMapConfigurationTypeName = beforeMapConfigurationTypeName;
        AfterMapConfigurationTypeName = afterMapConfigurationTypeName;
        Members = members;
        HasExistingPrimaryConstructor = hasExistingPrimaryConstructor;
        SourceHasPositionalConstructor = sourceHasPositionalConstructor;
        TypeXmlDocumentation = typeXmlDocumentation;
        ContainingTypes = containingTypes.IsDefault ? ImmutableArray<string>.Empty : containingTypes;
        UseFullName = useFullName;
        ExcludedRequiredMembers = excludedRequiredMembers.IsDefault ? ImmutableArray<FacetMember>.Empty : excludedRequiredMembers;
        NullableProperties = nullableProperties;
        CopyAttributes = copyAttributes;
        MaxDepth = maxDepth;
        PreserveReferences = preserveReferences;
        BaseClassMemberNames = baseClassMemberNames.IsDefault ? ImmutableArray<string>.Empty : baseClassMemberNames;
        FlattenToTypes = flattenToTypes.IsDefault ? ImmutableArray<string>.Empty : flattenToTypes;
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
            && Accessibility == other.Accessibility
            && GenerateConstructor == other.GenerateConstructor
            && GenerateParameterlessConstructor == other.GenerateParameterlessConstructor
            && ChainToParameterlessConstructor == other.ChainToParameterlessConstructor
            && GenerateExpressionProjection == other.GenerateExpressionProjection
            && SourceTypeName == other.SourceTypeName
            && SourceContainingTypes.SequenceEqual(other.SourceContainingTypes)
            && ConfigurationTypeName == other.ConfigurationTypeName
            && BeforeMapConfigurationTypeName == other.BeforeMapConfigurationTypeName
            && AfterMapConfigurationTypeName == other.AfterMapConfigurationTypeName
            && HasExistingPrimaryConstructor == other.HasExistingPrimaryConstructor
            && SourceHasPositionalConstructor == other.SourceHasPositionalConstructor
            && TypeXmlDocumentation == other.TypeXmlDocumentation
            && Members.SequenceEqual(other.Members)
            && ContainingTypes.SequenceEqual(other.ContainingTypes)
            && ExcludedRequiredMembers.SequenceEqual(other.ExcludedRequiredMembers)
            && UseFullName == other.UseFullName
            && NullableProperties == other.NullableProperties
            && CopyAttributes == other.CopyAttributes
            && MaxDepth == other.MaxDepth
            && PreserveReferences == other.PreserveReferences
            && BaseClassMemberNames.SequenceEqual(other.BaseClassMemberNames)
            && FlattenToTypes.SequenceEqual(other.FlattenToTypes);
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
            hash = hash * 31 + (Accessibility?.GetHashCode() ?? 0);
            hash = hash * 31 + GenerateConstructor.GetHashCode();
            hash = hash * 31 + GenerateParameterlessConstructor.GetHashCode();
            hash = hash * 31 + ChainToParameterlessConstructor.GetHashCode();
            hash = hash * 31 + GenerateExpressionProjection.GetHashCode();
            hash = hash * 31 + (SourceTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (ConfigurationTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (BeforeMapConfigurationTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (AfterMapConfigurationTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + HasExistingPrimaryConstructor.GetHashCode();
            hash = hash * 31 + SourceHasPositionalConstructor.GetHashCode();
            hash = hash * 31 + (TypeXmlDocumentation?.GetHashCode() ?? 0);
            hash = hash * 31 + UseFullName.GetHashCode();
            hash = hash * 31 + NullableProperties.GetHashCode();
            hash = hash * 31 + CopyAttributes.GetHashCode();
            hash = hash * 31 + MaxDepth.GetHashCode();
            hash = hash * 31 + PreserveReferences.GetHashCode();
            hash = hash * 31 + Members.Length.GetHashCode();

            foreach (var member in Members)
                hash = hash * 31 + member.GetHashCode();

            foreach (var containingType in ContainingTypes)
                hash = hash * 31 + (containingType?.GetHashCode() ?? 0);

            foreach (var sourceContainingType in SourceContainingTypes)
                hash = hash * 31 + (sourceContainingType?.GetHashCode() ?? 0);

            foreach (var excludedMember in ExcludedRequiredMembers)
                hash = hash * 31 + excludedMember.GetHashCode();

            foreach (var baseClassMember in BaseClassMemberNames)
                hash = hash * 31 + (baseClassMember?.GetHashCode() ?? 0);

            foreach (var flattenToType in FlattenToTypes)
                hash = hash * 31 + (flattenToType?.GetHashCode() ?? 0);

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