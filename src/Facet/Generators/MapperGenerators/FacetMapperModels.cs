using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet.Generators.MapperGenerators;

/// <summary>
/// How a single facet property is filled from the source object.
/// </summary>
internal enum MappedMemberKind
{
    /// <summary>The source value is assignable to the facet property as-is.</summary>
    Direct,

    /// <summary>
    /// The facet property is itself a facet, and a mapper is registered for the corresponding
    /// source property type. The initializer is inlined so the whole graph stays one expression.
    /// </summary>
    NestedFacet,

    /// <summary>
    /// The facet property is a collection whose element type is a facet with a registered mapper
    /// for the source collection's element type.
    /// </summary>
    NestedFacetCollection,
}

/// <summary>
/// One facet property, resolved against the source type.
/// </summary>
internal sealed class MappedMember : IEquatable<MappedMember>
{
    public MappedMember(
        string name,
        MappedMemberKind kind,
        string facetTypeName,
        bool sourceCanBeNull,
        bool sourceIsSettable,
        string? nestedSourceTypeName = null,
        string? nestedFacetTypeName = null,
        string? collectionWrapper = null)
    {
        Name = name;
        Kind = kind;
        FacetTypeName = facetTypeName;
        SourceCanBeNull = sourceCanBeNull;
        SourceIsSettable = sourceIsSettable;
        NestedSourceTypeName = nestedSourceTypeName;
        NestedFacetTypeName = nestedFacetTypeName;
        CollectionWrapper = collectionWrapper;
    }

    /// <summary>Property name, identical on both sides — matching is by name.</summary>
    public string Name { get; }

    public MappedMemberKind Kind { get; }

    /// <summary>The facet property's type, globally qualified.</summary>
    public string FacetTypeName { get; }

    /// <summary>
    /// Whether the source value may be null, and therefore needs a guard before a nested
    /// initializer is applied to it.
    /// </summary>
    public bool SourceCanBeNull { get; }

    /// <summary>
    /// Whether the corresponding source property is publicly settable — a precondition for
    /// including this member in the reverse <c>ToSource</c> mapping.
    /// </summary>
    public bool SourceIsSettable { get; }

    /// <summary>For nested kinds: the source side's type (the element type, for collections).</summary>
    public string? NestedSourceTypeName { get; }

    /// <summary>For nested kinds: the facet side's type (the element type, for collections).</summary>
    public string? NestedFacetTypeName { get; }

    /// <summary>
    /// For <see cref="MappedMemberKind.NestedFacetCollection"/>: the materialising call to apply
    /// after <c>Select</c>, e.g. <c>ToList</c>, <c>ToArray</c>, or null to leave it lazy.
    /// </summary>
    public string? CollectionWrapper { get; }

    public bool Equals(MappedMember? other) =>
        other is not null
        && Name == other.Name
        && Kind == other.Kind
        && FacetTypeName == other.FacetTypeName
        && SourceCanBeNull == other.SourceCanBeNull
        && SourceIsSettable == other.SourceIsSettable
        && NestedSourceTypeName == other.NestedSourceTypeName
        && NestedFacetTypeName == other.NestedFacetTypeName
        && CollectionWrapper == other.CollectionWrapper;

    public override bool Equals(object? obj) => obj is MappedMember other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Name?.GetHashCode() ?? 0;
            hash = hash * 31 + (int)Kind;
            hash = hash * 31 + (FacetTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + SourceCanBeNull.GetHashCode();
            hash = hash * 31 + SourceIsSettable.GetHashCode();
            hash = hash * 31 + (NestedSourceTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (NestedFacetTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (CollectionWrapper?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

/// <summary>
/// A facet property that could not be mapped, and why. Surfaces as FAC110 so that a rename on
/// either side is a build warning rather than a silently default-valued property.
/// </summary>
internal sealed class UnmappedMember : IEquatable<UnmappedMember>
{
    public UnmappedMember(string name, string reason)
    {
        Name = name;
        Reason = reason;
    }

    public string Name { get; }

    public string Reason { get; }

    public bool Equals(UnmappedMember? other) =>
        other is not null && Name == other.Name && Reason == other.Reason;

    public override bool Equals(object? obj) => obj is UnmappedMember other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (Name?.GetHashCode() ?? 0) * 31 + (Reason?.GetHashCode() ?? 0);
        }
    }
}

/// <summary>
/// One <c>[assembly: FacetMapper(...)]</c> registration, resolved into everything the code
/// builder needs.
/// </summary>
internal sealed class MapperRegistration : IEquatable<MapperRegistration>
{
    public MapperRegistration(
        string sourceTypeName,
        string facetTypeName,
        string sourceSimpleName,
        string facetSimpleName,
        string mapperName,
        string mapperNamespace,
        bool generateProjection,
        bool generateToSource,
        bool sourceHasParameterlessConstructor,
        bool facetIsInCurrentCompilation,
        MapperConfiguration configuration,
        bool mappingPackageAvailable,
        ImmutableArray<MappedMember> members,
        ImmutableArray<UnmappedMember> unmappedMembers,
        SourceLocationInfo? location)
    {
        SourceTypeName = sourceTypeName;
        FacetTypeName = facetTypeName;
        SourceSimpleName = sourceSimpleName;
        FacetSimpleName = facetSimpleName;
        MapperName = mapperName;
        MapperNamespace = mapperNamespace;
        GenerateProjection = generateProjection;
        GenerateToSource = generateToSource;
        SourceHasParameterlessConstructor = sourceHasParameterlessConstructor;
        FacetIsInCurrentCompilation = facetIsInCurrentCompilation;
        Configuration = configuration;
        MappingPackageAvailable = mappingPackageAvailable;
        Members = members;
        UnmappedMembers = unmappedMembers;
        Location = location;
    }

    /// <summary>Globally qualified name of the type being mapped from.</summary>
    public string SourceTypeName { get; }

    /// <summary>Globally qualified name of the facet type being mapped to.</summary>
    public string FacetTypeName { get; }

    public string SourceSimpleName { get; }

    public string FacetSimpleName { get; }

    /// <summary>Name of the generated static class.</summary>
    public string MapperName { get; }

    /// <summary>Namespace of the generated static class.</summary>
    public string MapperNamespace { get; }

    public bool GenerateProjection { get; }

    public bool GenerateToSource { get; }

    public bool SourceHasParameterlessConstructor { get; }

    /// <summary>
    /// Whether the facet type is a <c>[Facet]</c> type declared in the compilation being
    /// generated, rather than read from a referenced assembly. One generator cannot see another's
    /// output, so such a type looks memberless here — the case FAC114 exists to report. A
    /// hand-written type in this assembly is fully visible and does not set this.
    /// </summary>
    public bool FacetIsInCurrentCompilation { get; }

    /// <summary>The custom mapping hooks this registration opted into.</summary>
    public MapperConfiguration Configuration { get; }

    /// <summary>
    /// Whether <c>Facet.Mapping</c> is referenced, which decides whether the overload taking a
    /// configuration instance can be emitted at all — its parameter type lives in that package, so
    /// emitting it unconditionally would break any project that has not taken the dependency.
    /// </summary>
    public bool MappingPackageAvailable { get; }

    public ImmutableArray<MappedMember> Members { get; }

    public ImmutableArray<UnmappedMember> UnmappedMembers { get; }

    /// <summary>Location of the attribute application, for diagnostics.</summary>
    public SourceLocationInfo? Location { get; }

    /// <summary>
    /// Identifies the (source, facet) pair. Nested members are resolved by looking up the pair
    /// they need in the set of all registrations in the compilation.
    /// </summary>
    public string PairKey => MakePairKey(SourceTypeName, FacetTypeName);

    public static string MakePairKey(string sourceTypeName, string facetTypeName)
        => sourceTypeName + "->" + facetTypeName;

    /// <summary>
    /// Members the reverse mapping can carry back: a direct member whose source property is
    /// publicly settable.
    /// </summary>
    public IEnumerable<MappedMember> ReverseMappableMembers =>
        Members.Where(m => m.Kind == MappedMemberKind.Direct && m.SourceIsSettable);

    /// <summary>
    /// Members the reverse mapping leaves at their default, and why each is excluded.
    /// </summary>
    /// <remarks>
    /// A nested member is excluded because reversing it by construction is wrong whenever the
    /// target is tracked by an ORM — a fresh child instance reads as an insert and the previous one
    /// as an orphan. <c>ApplyTo</c> handles the nested case by writing onto the instance already
    /// there, and <c>ToSourceConfiguration</c> is where a policy beyond that belongs.
    /// </remarks>
    public IEnumerable<(string Name, string Reason)> ReverseSkippedMembers =>
        Members
            .Where(m => m.Kind != MappedMemberKind.Direct || !m.SourceIsSettable)
            .Select(m => (
                m.Name,
                m.Kind switch
                {
                    MappedMemberKind.NestedFacet =>
                        "a nested facet cannot be reversed by construction without detaching it from "
                        + "whatever tracks the original; ApplyTo writes onto the existing instance instead",
                    MappedMemberKind.NestedFacetCollection =>
                        "reconciling a collection needs a key and a policy for added and removed "
                        + "elements, which only the application can decide",
                    _ => "the source property has no public setter",
                }));

    public bool Equals(MapperRegistration? other) =>
        other is not null
        && SourceTypeName == other.SourceTypeName
        && FacetTypeName == other.FacetTypeName
        && MapperName == other.MapperName
        && MapperNamespace == other.MapperNamespace
        && GenerateProjection == other.GenerateProjection
        && GenerateToSource == other.GenerateToSource
        && SourceHasParameterlessConstructor == other.SourceHasParameterlessConstructor
        && FacetIsInCurrentCompilation == other.FacetIsInCurrentCompilation
        && MappingPackageAvailable == other.MappingPackageAvailable
        && Configuration.Equals(other.Configuration)
        && Members.SequenceEqual(other.Members)
        && UnmappedMembers.SequenceEqual(other.UnmappedMembers)
        && Nullable.Equals(Location, other.Location);

    public override bool Equals(object? obj) => obj is MapperRegistration other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = SourceTypeName?.GetHashCode() ?? 0;
            hash = hash * 31 + (FacetTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (MapperName?.GetHashCode() ?? 0);
            hash = hash * 31 + (MapperNamespace?.GetHashCode() ?? 0);
            hash = hash * 31 + GenerateProjection.GetHashCode();
            hash = hash * 31 + GenerateToSource.GetHashCode();
            hash = hash * 31 + Configuration.GetHashCode();
            hash = hash * 31 + Members.Length;
            foreach (var member in Members)
                hash = hash * 31 + member.GetHashCode();
            hash = hash * 31 + UnmappedMembers.Length;
            return hash;
        }
    }
}

/// <summary>
/// The custom mapping hooks a registration opted into, already checked against the interfaces
/// they have to implement.
/// </summary>
/// <remarks>
/// The split between <see cref="MapTypeName"/> and <see cref="ProjectionTypeName"/> is the one
/// Facet already draws for <c>[Facet]</c>, and it is forced rather than chosen: an
/// <c>IFacetMapConfiguration.Map</c> is imperative, and imperative code cannot appear in an
/// expression tree, so it can only run in the runtime mapper. An
/// <c>IFacetProjectionMapConfiguration</c> declares its rules as expressions instead, which can be
/// merged into the projection's object initializer — and then also applied to the runtime mapper,
/// so one declaration serves both.
/// </remarks>
internal sealed class MapperConfiguration : IEquatable<MapperConfiguration>
{
    public static MapperConfiguration None { get; } =
        new MapperConfiguration(null, null, null, null, null);

    public MapperConfiguration(
        string? mapTypeName,
        string? projectionTypeName,
        string? beforeMapTypeName,
        string? afterMapTypeName,
        string? toSourceTypeName)
    {
        MapTypeName = mapTypeName;
        ProjectionTypeName = projectionTypeName;
        BeforeMapTypeName = beforeMapTypeName;
        AfterMapTypeName = afterMapTypeName;
        ToSourceTypeName = toSourceTypeName;
    }

    /// <summary>Implements <c>IFacetMapConfiguration</c>; its <c>Map</c> runs in the runtime mapper.</summary>
    public string? MapTypeName { get; }

    /// <summary>Implements <c>IFacetProjectionMapConfiguration</c>; merged into the projection.</summary>
    public string? ProjectionTypeName { get; }

    public string? BeforeMapTypeName { get; }

    public string? AfterMapTypeName { get; }

    public string? ToSourceTypeName { get; }

    /// <summary>
    /// Whether anything has to run against a constructed instance, which is what decides between
    /// emitting the runtime mapper as a single object initializer and as construct-then-populate.
    /// </summary>
    public bool RequiresInstanceSteps =>
        MapTypeName is not null
        || ProjectionTypeName is not null
        || BeforeMapTypeName is not null
        || AfterMapTypeName is not null;

    public bool IsNone =>
        !RequiresInstanceSteps && ToSourceTypeName is null;

    public bool Equals(MapperConfiguration? other) =>
        other is not null
        && MapTypeName == other.MapTypeName
        && ProjectionTypeName == other.ProjectionTypeName
        && BeforeMapTypeName == other.BeforeMapTypeName
        && AfterMapTypeName == other.AfterMapTypeName
        && ToSourceTypeName == other.ToSourceTypeName;

    public override bool Equals(object? obj) => obj is MapperConfiguration other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = MapTypeName?.GetHashCode() ?? 0;
            hash = hash * 31 + (ProjectionTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (BeforeMapTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (AfterMapTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (ToSourceTypeName?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
