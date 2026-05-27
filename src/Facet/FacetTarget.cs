using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet;

/// <summary>
/// Information about a base Facet class that the current Facet inherits from.
/// </summary>
internal sealed class BaseFacetInfo
{
    /// <summary>
    /// The fully qualified name of the base Facet class.
    /// </summary>
    public string BaseTypeName { get; }

    /// <summary>
    /// The fully qualified name of the base Facet's source type.
    /// </summary>
    public string BaseSourceTypeName { get; }

    /// <summary>
    /// The configuration type name for the nearest base Facet ancestor (if it has ConfigureProjection).
    /// </summary>
    public string? BaseConfigurationTypeName { get; }

    /// <summary>
    /// The source type name that the nearest Configuration's ConfigureProjection expects.
    /// May differ from BaseSourceTypeName when the Configuration is on a grandparent Facet.
    /// </summary>
    public string? BaseConfigurationSourceTypeName { get; }

    /// <summary>
    /// The target type name that the nearest Configuration's ConfigureProjection expects.
    /// May differ from BaseTypeName when the Configuration is on a grandparent Facet.
    /// </summary>
    public string? BaseConfigurationTargetTypeName { get; }

    /// <summary>
    /// All projection configurations from the full ancestor chain, nearest first.
    /// Includes configs from every ancestor Facet that has one, even when intermediate ancestors lack one.
    /// </summary>
    public ImmutableArray<(string ConfigTypeName, string SourceTypeName, string TargetTypeName)> AllBaseProjectionConfigs { get; }

    /// <summary>
    /// The Include properties specified in the base Facet's [Facet] attribute.
    /// </summary>
    public ImmutableArray<string> IncludedMembers { get; }

    /// <summary>
    /// The nested facet mappings from the base Facet's NestedFacets parameter.
    /// Maps source type full names to nested facet type information.
    /// </summary>
    public ImmutableDictionary<string, (string childFacetTypeName, string sourceTypeName)> NestedFacetMappings { get; }

    /// <summary>
    /// True when the nearest ancestor Facet has exactly one [Facet] attribute (single-source).
    /// Only single-source facets generate <c>ProjectionFor(string)</c> — multi-source facets do not.
    /// Used to avoid emitting spurious <c>new</c> on <c>ProjectionFor</c> in derived facets.
    /// </summary>
    public bool IsBaseSingleSource { get; }

    public BaseFacetInfo(string baseTypeName, string baseSourceTypeName, string? baseConfigurationTypeName, ImmutableArray<string> includedMembers, ImmutableDictionary<string, (string childFacetTypeName, string sourceTypeName)> nestedFacetMappings, bool isBaseSingleSource = true, string? baseConfigurationSourceTypeName = null, string? baseConfigurationTargetTypeName = null, ImmutableArray<(string ConfigTypeName, string SourceTypeName, string TargetTypeName)> allBaseProjectionConfigs = default)
    {
        BaseTypeName = baseTypeName;
        BaseSourceTypeName = baseSourceTypeName;
        BaseConfigurationTypeName = baseConfigurationTypeName;
        BaseConfigurationSourceTypeName = baseConfigurationSourceTypeName ?? baseSourceTypeName;
        BaseConfigurationTargetTypeName = baseConfigurationTargetTypeName ?? baseTypeName;
        IncludedMembers = includedMembers;
        NestedFacetMappings = nestedFacetMappings;
        IsBaseSingleSource = isBaseSingleSource;

        if (!allBaseProjectionConfigs.IsDefault)
        {
            AllBaseProjectionConfigs = allBaseProjectionConfigs;
        }
        else if (BaseConfigurationTypeName != null)
        {
            AllBaseProjectionConfigs = ImmutableArray.Create((BaseConfigurationTypeName, BaseConfigurationSourceTypeName!, BaseConfigurationTargetTypeName!));
        }
        else
        {
            AllBaseProjectionConfigs = ImmutableArray<(string, string, string)>.Empty;
        }
    }
}

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
    public int MaxDepthToSource { get; }
    public ImmutableArray<string> BaseClassMemberNames { get; }
    public ImmutableArray<string> FlattenToTypes { get; }

    /// <summary>
    /// Controls the set accessor emitted on all generated properties.
    /// </summary>
    public PropertySetAccessor SetAccessor { get; }

    /// <summary>
    /// Fully-qualified names (with <c>global::</c> prefix) of the user-declared base class
    /// and interfaces on the target type. Emitted in the <c>.Properties.g.cs</c> partial
    /// declaration so that the type's inheritance chain is preserved even when the
    /// <c>.Mappings.g.cs</c> file is excluded from compilation (e.g. to avoid back-end references).
    /// </summary>
    public ImmutableArray<string> DeclaredBaseTypeNames { get; }

    /// <summary>
    /// The top-level property and field names of the source type (including inherited members).
    /// Used to disambiguate navigation properties from static type names in MapFrom expressions.
    /// </summary>
    public ImmutableArray<string> SourcePropertyNames { get; }

    /// <summary>
    /// The target type for enum conversion. "string" or "int", or null if no conversion.
    /// </summary>
    public string? ConvertEnumsTo { get; }

    /// <summary>
    /// Whether to generate a copy constructor that accepts another instance of the same facet type.
    /// </summary>
    public bool GenerateCopyConstructor { get; }

    /// <summary>
    /// Whether to generate value-based equality members (Equals, GetHashCode, ==, !=).
    /// </summary>
    public bool GenerateEquality { get; }

    /// <summary>
    /// Optional type name for custom reverse-mapping logic called inside <c>ToSource()</c>.
    /// </summary>
    public string? ToSourceConfigurationTypeName { get; }

    /// <summary>
    /// When true, the base class of this facet already declares one or more of the generated
    /// members (<c>ToSource</c>, <c>Projection</c>, <c>BackTo</c>) that are hidden purely by name.
    /// The <c>new</c> modifier will be emitted on those members to suppress CS0108.
    /// </summary>
    public bool BaseHidesFacetMembers { get; }

    /// <summary>
    /// When true, the base class of this facet also has <c>GenerateToSource = true</c>, meaning
    /// it generates <c>ToSource()</c>, <c>BackTo()</c>, and <c>ApplyToSource()</c> methods.
    /// The <c>new</c> modifier should be emitted on these members only when this is true;
    /// otherwise the base doesn't have these methods and emitting <c>new</c> causes CS0109.
    /// </summary>
    public bool BaseHidesToSource { get; }

    /// <summary>
    /// When true, the base class of this facet already declares a <c>FromSource</c> method
    /// whose parameter type matches this facet's source type. Only in this case does the
    /// derived <c>FromSource</c> actually hide the base method and require the <c>new</c> modifier.
    /// When the parameter types differ (different source entities), the methods are overloads
    /// and <c>new</c> is not needed (emitting it would produce CS0109).
    /// </summary>
    public bool BaseHidesFromSource { get; }

    /// <summary>
    /// When true, the <see cref="ConfigurationTypeName"/> type implements
    /// <c>IFacetProjectionMapConfiguration&lt;TSource, TTarget&gt;</c>.
    /// The generator will emit a lazily-built <c>Projection</c> that inlines the
    /// <c>ConfigureProjection</c> bindings instead of a static expression literal.
    /// </summary>
    public bool HasProjectionMapConfiguration { get; }

    /// <summary>
    /// When true, the <see cref="ConfigurationTypeName"/> type implements
    /// <c>IFacetMapConfiguration&lt;TSource, TTarget&gt;</c> and provides an imperative <c>Map()</c> method.
    /// When false but <see cref="HasProjectionMapConfiguration"/> is true, the constructor will
    /// compile the projection expressions into a cached <c>Action&lt;TSource, TTarget&gt;</c> instead.
    /// </summary>
    public bool HasMapConfiguration { get; }

    /// <summary>
    /// Information about the base Facet class, if the target inherits from another Facet.
    /// </summary>
    public BaseFacetInfo? BaseFacetInfo { get; }

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
        bool chainToParameterlessConstructor = false,
        string? convertEnumsTo = null,
        bool generateCopyConstructor = false,
        bool generateEquality = false,
        string? toSourceConfigurationTypeName = null,
        bool baseHidesFacetMembers = false,
        bool hasProjectionMapConfiguration = false,
        bool baseHidesFromSource = false,
        bool hasMapConfiguration = false,
        BaseFacetInfo? baseFacetInfo = null,
        int maxDepthToSource = 0,
        ImmutableArray<string> sourcePropertyNames = default,
        bool baseHidesToSource = false,
        PropertySetAccessor setAccessor = PropertySetAccessor.Preserve,
        ImmutableArray<string> declaredBaseTypeNames = default)
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
        MaxDepthToSource = maxDepthToSource;
        BaseClassMemberNames = baseClassMemberNames.IsDefault ? ImmutableArray<string>.Empty : baseClassMemberNames;
        FlattenToTypes = flattenToTypes.IsDefault ? ImmutableArray<string>.Empty : flattenToTypes;
        ConvertEnumsTo = convertEnumsTo;
        GenerateCopyConstructor = generateCopyConstructor;
        GenerateEquality = generateEquality;
        ToSourceConfigurationTypeName = toSourceConfigurationTypeName;
        BaseHidesFacetMembers = baseHidesFacetMembers;
        HasProjectionMapConfiguration = hasProjectionMapConfiguration;
        HasMapConfiguration = hasMapConfiguration;
        BaseHidesFromSource = baseHidesFromSource;
        BaseFacetInfo = baseFacetInfo;
        SourcePropertyNames = sourcePropertyNames.IsDefault ? ImmutableArray<string>.Empty : sourcePropertyNames;
        BaseHidesToSource = baseHidesToSource;
        SetAccessor = setAccessor;
        DeclaredBaseTypeNames = declaredBaseTypeNames.IsDefault ? ImmutableArray<string>.Empty : declaredBaseTypeNames;
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
            && MaxDepthToSource == other.MaxDepthToSource
            && BaseClassMemberNames.SequenceEqual(other.BaseClassMemberNames)
            && FlattenToTypes.SequenceEqual(other.FlattenToTypes)
            && ConvertEnumsTo == other.ConvertEnumsTo
            && GenerateCopyConstructor == other.GenerateCopyConstructor
            && GenerateEquality == other.GenerateEquality
            && ToSourceConfigurationTypeName == other.ToSourceConfigurationTypeName
            && BaseHidesFacetMembers == other.BaseHidesFacetMembers
            && BaseHidesToSource == other.BaseHidesToSource
            && HasProjectionMapConfiguration == other.HasProjectionMapConfiguration
            && HasMapConfiguration == other.HasMapConfiguration
            && BaseHidesFromSource == other.BaseHidesFromSource
            && SourcePropertyNames.SequenceEqual(other.SourcePropertyNames)
            && SetAccessor == other.SetAccessor
            && DeclaredBaseTypeNames.SequenceEqual(other.DeclaredBaseTypeNames);
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
            hash = hash * 31 + MaxDepthToSource.GetHashCode();
            hash = hash * 31 + (ConvertEnumsTo?.GetHashCode() ?? 0);
            hash = hash * 31 + GenerateCopyConstructor.GetHashCode();
            hash = hash * 31 + GenerateEquality.GetHashCode();
            hash = hash * 31 + (ToSourceConfigurationTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + BaseHidesFacetMembers.GetHashCode();
            hash = hash * 31 + BaseHidesToSource.GetHashCode();
            hash = hash * 31 + HasProjectionMapConfiguration.GetHashCode();
            hash = hash * 31 + HasMapConfiguration.GetHashCode();
            hash = hash * 31 + BaseHidesFromSource.GetHashCode();
            hash = hash * 31 + SourcePropertyNames.Length.GetHashCode();
            hash = hash * 31 + SetAccessor.GetHashCode();
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

            foreach (var baseTypeName in DeclaredBaseTypeNames)
                hash = hash * 31 + (baseTypeName?.GetHashCode() ?? 0);

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
