using Facet.Generators.Shared;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet.Generators.FacetMapGenerators;

internal sealed class FacetMapTargetModel : IEquatable<FacetMapTargetModel>
{
    public string MarkerClassName { get; }
    public string? Namespace { get; }
    public string FullName { get; }
    public string Accessibility { get; }

    public string SourceTypeName { get; }
    public string TargetTypeName { get; }
    public string SourceTypeSimpleName { get; }
    public string TargetTypeSimpleName { get; }

    public bool GenerateToSource { get; }
    public bool GenerateProjection { get; }
    public int MaxDepth { get; }

    public string? ConfigurationTypeName { get; }
    public string? ToSourceConfigurationTypeName { get; }
    public string? BeforeMapConfigurationTypeName { get; }
    public string? AfterMapConfigurationTypeName { get; }

    public bool SourceHasPositionalConstructor { get; }
    public bool TargetHasParameterlessConstructor { get; }

    public ImmutableArray<FacetMapMember> Members { get; }
    public ImmutableArray<string> ContainingTypes { get; }

    public FacetMapTargetModel(
        string markerClassName,
        string? ns,
        string fullName,
        string accessibility,
        string sourceTypeName,
        string targetTypeName,
        string sourceTypeSimpleName,
        string targetTypeSimpleName,
        bool generateToSource,
        bool generateProjection,
        int maxDepth,
        string? configurationTypeName,
        string? toSourceConfigurationTypeName,
        string? beforeMapConfigurationTypeName,
        string? afterMapConfigurationTypeName,
        bool sourceHasPositionalConstructor,
        bool targetHasParameterlessConstructor,
        ImmutableArray<FacetMapMember> members,
        ImmutableArray<string> containingTypes)
    {
        MarkerClassName = markerClassName;
        Namespace = ns;
        FullName = fullName;
        Accessibility = accessibility;
        SourceTypeName = sourceTypeName;
        TargetTypeName = targetTypeName;
        SourceTypeSimpleName = sourceTypeSimpleName;
        TargetTypeSimpleName = targetTypeSimpleName;
        GenerateToSource = generateToSource;
        GenerateProjection = generateProjection;
        MaxDepth = maxDepth;
        ConfigurationTypeName = configurationTypeName;
        ToSourceConfigurationTypeName = toSourceConfigurationTypeName;
        BeforeMapConfigurationTypeName = beforeMapConfigurationTypeName;
        AfterMapConfigurationTypeName = afterMapConfigurationTypeName;
        SourceHasPositionalConstructor = sourceHasPositionalConstructor;
        TargetHasParameterlessConstructor = targetHasParameterlessConstructor;
        Members = members;
        ContainingTypes = containingTypes;
    }

    public bool Equals(FacetMapTargetModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return MarkerClassName == other.MarkerClassName
            && Namespace == other.Namespace
            && FullName == other.FullName
            && Accessibility == other.Accessibility
            && SourceTypeName == other.SourceTypeName
            && TargetTypeName == other.TargetTypeName
            && GenerateToSource == other.GenerateToSource
            && GenerateProjection == other.GenerateProjection
            && MaxDepth == other.MaxDepth
            && ConfigurationTypeName == other.ConfigurationTypeName
            && ToSourceConfigurationTypeName == other.ToSourceConfigurationTypeName
            && BeforeMapConfigurationTypeName == other.BeforeMapConfigurationTypeName
            && AfterMapConfigurationTypeName == other.AfterMapConfigurationTypeName
            && SourceHasPositionalConstructor == other.SourceHasPositionalConstructor
            && TargetHasParameterlessConstructor == other.TargetHasParameterlessConstructor
            && Members.SequenceEqual(other.Members)
            && ContainingTypes.SequenceEqual(other.ContainingTypes);
    }

    public override bool Equals(object? obj) => obj is FacetMapTargetModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + MarkerClassName.GetHashCode();
            hash = hash * 31 + (Namespace?.GetHashCode() ?? 0);
            hash = hash * 31 + FullName.GetHashCode();
            hash = hash * 31 + SourceTypeName.GetHashCode();
            hash = hash * 31 + TargetTypeName.GetHashCode();
            hash = hash * 31 + GenerateToSource.GetHashCode();
            hash = hash * 31 + GenerateProjection.GetHashCode();
            hash = hash * 31 + Members.Length.GetHashCode();
            return hash;
        }
    }
}

internal sealed class FacetMapMember : IEquatable<FacetMapMember>, IMappableMember
{
    public string Name { get; }
    public string TypeName { get; }
    public string SourcePropertyName { get; }
    public bool IsValueType { get; }
    public bool IsCollection { get; }
    public string? CollectionWrapper { get; }
    public string? SourceCollectionWrapper { get; }
    public string? CollectionElementType { get; }
    public bool IsNullable { get; }
    public bool IsTargetInitOnly { get; }
    public bool IsSourceInitOnly { get; }

    // Extended properties for unified expression generation
    public bool IsNestedFacet { get; }
    public string? NestedFacetSourceTypeName { get; }
    public string? SourceMemberTypeName { get; }
    public string? MapFromSource { get; }
    public bool MapFromReversible { get; }
    public bool MapFromIncludeInProjection { get; }
    public IReadOnlyList<string> MapWhenConditions { get; }
    public string? MapWhenDefault { get; }
    public bool IsEnumConversion { get; }
    public string? OriginalEnumTypeName { get; }

    public FacetMapMember(
        string name,
        string typeName,
        string sourcePropertyName,
        bool isValueType,
        bool isCollection,
        string? collectionWrapper,
        string? sourceCollectionWrapper,
        string? collectionElementType,
        bool isNullable,
        bool isTargetInitOnly,
        bool isSourceInitOnly,
        bool isNestedFacet = false,
        string? nestedFacetSourceTypeName = null,
        string? sourceMemberTypeName = null,
        string? mapFromSource = null,
        bool mapFromReversible = true,
        bool mapFromIncludeInProjection = true,
        IReadOnlyList<string>? mapWhenConditions = null,
        string? mapWhenDefault = null,
        bool isEnumConversion = false,
        string? originalEnumTypeName = null)
    {
        Name = name;
        TypeName = typeName;
        SourcePropertyName = sourcePropertyName;
        IsValueType = isValueType;
        IsCollection = isCollection;
        CollectionWrapper = collectionWrapper;
        SourceCollectionWrapper = sourceCollectionWrapper;
        CollectionElementType = collectionElementType;
        IsNullable = isNullable;
        IsTargetInitOnly = isTargetInitOnly;
        IsSourceInitOnly = isSourceInitOnly;
        IsNestedFacet = isNestedFacet;
        NestedFacetSourceTypeName = nestedFacetSourceTypeName;
        SourceMemberTypeName = sourceMemberTypeName;
        MapFromSource = mapFromSource;
        MapFromReversible = mapFromReversible;
        MapFromIncludeInProjection = mapFromIncludeInProjection;
        MapWhenConditions = mapWhenConditions ?? Array.Empty<string>();
        MapWhenDefault = mapWhenDefault;
        IsEnumConversion = isEnumConversion;
        OriginalEnumTypeName = originalEnumTypeName;
    }

    public bool Equals(FacetMapMember? other)
    {
        if (other is null) return false;
        return Name == other.Name
            && TypeName == other.TypeName
            && SourcePropertyName == other.SourcePropertyName
            && IsValueType == other.IsValueType
            && IsCollection == other.IsCollection
            && CollectionWrapper == other.CollectionWrapper
            && SourceCollectionWrapper == other.SourceCollectionWrapper
            && CollectionElementType == other.CollectionElementType
            && IsNullable == other.IsNullable
            && IsTargetInitOnly == other.IsTargetInitOnly
            && IsSourceInitOnly == other.IsSourceInitOnly
            && IsNestedFacet == other.IsNestedFacet
            && NestedFacetSourceTypeName == other.NestedFacetSourceTypeName
            && SourceMemberTypeName == other.SourceMemberTypeName
            && MapFromSource == other.MapFromSource
            && MapFromReversible == other.MapFromReversible
            && MapFromIncludeInProjection == other.MapFromIncludeInProjection
            && IsEnumConversion == other.IsEnumConversion
            && OriginalEnumTypeName == other.OriginalEnumTypeName;
    }

    public override bool Equals(object? obj) => obj is FacetMapMember other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Name.GetHashCode();
            hash = hash * 31 + TypeName.GetHashCode();
            hash = hash * 31 + SourcePropertyName.GetHashCode();
            hash = hash * 31 + IsValueType.GetHashCode();
            hash = hash * 31 + IsCollection.GetHashCode();
            hash = hash * 31 + (CollectionWrapper?.GetHashCode() ?? 0);
            hash = hash * 31 + IsNestedFacet.GetHashCode();
            hash = hash * 31 + IsEnumConversion.GetHashCode();
            return hash;
        }
    }
}
