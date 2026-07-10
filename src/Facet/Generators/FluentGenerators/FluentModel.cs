using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;

namespace Facet.Generators.Fluent;

/// <summary>A scalar/complex member of an entity's shape (the manifest keep-set, typed via Roslyn).</summary>
internal sealed class FluentMember : IEquatable<FluentMember>
{
    public FluentMember(string name, string typeDisplay)
    {
        Name = name;
        TypeDisplay = typeDisplay;
    }

    public string Name { get; }

    /// <summary>Fully qualified display string including nullable annotations.</summary>
    public string TypeDisplay { get; }

    public bool Equals(FluentMember? other)
        => other != null && Name == other.Name && TypeDisplay == other.TypeDisplay;

    public override bool Equals(object? obj) => obj is FluentMember other && Equals(other);

    public override int GetHashCode() => unchecked(Name.GetHashCode() * 31 + TypeDisplay.GetHashCode());
}

/// <summary>A chainable navigation, resolved against the entity's CLR symbol.</summary>
internal sealed class FluentNav : IEquatable<FluentNav>
{
    public FluentNav(string name, string targetClrName, bool isCollection, bool isOptional)
    {
        Name = name;
        TargetClrName = targetClrName;
        IsCollection = isCollection;
        IsOptional = isOptional;
    }

    public string Name { get; }

    /// <summary>Dot-separated CLR name of the target entity — a key into the fluent model set.</summary>
    public string TargetClrName { get; }

    public bool IsCollection { get; }

    /// <summary>
    /// Reference navigations only: whether the relationship is optional per the manifest
    /// (<c>navOptional</c>). Optional references surface as nullable shape members and get
    /// a null guard in selectors. Collections are never optional.
    /// </summary>
    public bool IsOptional { get; }

    public bool Equals(FluentNav? other)
        => other != null
            && Name == other.Name
            && TargetClrName == other.TargetClrName
            && IsCollection == other.IsCollection
            && IsOptional == other.IsOptional;

    public override bool Equals(object? obj) => obj is FluentNav other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Name.GetHashCode();
            hash = hash * 31 + TargetClrName.GetHashCode();
            hash = hash * 31 + IsCollection.GetHashCode();
            hash = hash * 31 + IsOptional.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// Everything emission needs for one entity: shape members from the manifest keep-set,
/// chainable navigations from the manifest nav sets, and the primary key — all typed by
/// resolving names against the entity's CLR symbol. Built once per (manifest, compilation)
/// pair; structural equality drives incremental caching.
/// </summary>
internal sealed class FluentEntityModel : IEquatable<FluentEntityModel>
{
    public FluentEntityModel(
        string clrName,
        string ns,
        string simpleName,
        ImmutableArray<FluentMember> members,
        ImmutableArray<FluentNav> navs,
        ImmutableArray<FluentMember> keyMembers)
    {
        ClrName = clrName;
        Namespace = ns;
        SimpleName = simpleName;
        Members = members;
        Navs = navs;
        KeyMembers = keyMembers;
    }

    /// <summary>Dot-separated, namespace-qualified CLR name (the manifest key).</summary>
    public string ClrName { get; }

    /// <summary>Namespace generated types are emitted into (the entity's own).</summary>
    public string Namespace { get; }

    public string SimpleName { get; }

    public string GlobalName => "global::" + ClrName;

    public ImmutableArray<FluentMember> Members { get; }

    public ImmutableArray<FluentNav> Navs { get; }

    /// <summary>
    /// Primary-key members in key order, typed. Empty when the entity is keyless, the
    /// manifest predates the <c>key</c> field, or a key member is a shadow property with no
    /// CLR symbol to take a type from — GetByKeyAsync is simply not emitted then.
    /// </summary>
    public ImmutableArray<FluentMember> KeyMembers { get; }

    public FluentNav? FindNav(string name) => Navs.FirstOrDefault(n => n.Name == name);

    /// <summary>
    /// Resolves manifest entries against the compilation. Entities whose CLR type is not
    /// visible to this compilation are skipped: the fluent surface can only be generated
    /// where the entity types can be referenced.
    /// </summary>
    public static ImmutableDictionary<string, FluentEntityModel> Build(
        EfModelManifest manifest,
        IEnumerable<string> entityClrNames,
        Compilation compilation)
    {
        var displayFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        var models = ImmutableDictionary.CreateBuilder<string, FluentEntityModel>(StringComparer.Ordinal);

        foreach (var clrName in entityClrNames)
        {
            if (!manifest.TryGetEntity(clrName, out var entry) || entry == null) continue;

            var symbol = compilation.GetTypeByMetadataName(clrName);
            if (symbol == null) continue;

            var properties = CollectProperties(symbol);

            var members = ImmutableArray.CreateBuilder<FluentMember>();
            foreach (var name in entry.Keep.OrderBy(n => n, StringComparer.Ordinal))
            {
                if (!properties.TryGetValue(name, out var property)) continue;
                members.Add(new FluentMember(name, property.Type.ToDisplayString(displayFormat)));
            }

            var navs = ImmutableArray.CreateBuilder<FluentNav>();
            foreach (var name in entry.ChainableNavs.OrderBy(n => n, StringComparer.Ordinal))
            {
                if (!properties.TryGetValue(name, out var property)) continue;

                ITypeSymbol targetType = property.Type;
                var isCollection = GeneratorUtilities.TryGetCollectionElementType(property.Type, out var elementType, out _)
                    && elementType != null;
                if (isCollection) targetType = elementType!;
                if (targetType.NullableAnnotation == NullableAnnotation.Annotated
                    && targetType is INamedTypeSymbol annotated)
                {
                    targetType = annotated.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
                }

                var targetClrName = GeneratorUtilities.StripGlobalPrefix(
                    targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                // Only navigations to entities the manifest also lists are chainable —
                // their shape types must exist for the capability interface to reference.
                if (!manifest.TryGetEntity(targetClrName, out _)) continue;

                // Pre-navOptional manifests can't say which references are required;
                // pessimistic-nullable never lies, and regenerating the manifest tightens it.
                var isOptional = !isCollection
                    && (!entry.OptionalNavsKnown || entry.OptionalNavs.Contains(name));
                navs.Add(new FluentNav(name, targetClrName, isCollection, isOptional));
            }

            var keyMembers = ImmutableArray.CreateBuilder<FluentMember>();
            foreach (var name in entry.Key)
            {
                if (!properties.TryGetValue(name, out var property))
                {
                    // Shadow key member: EF.Property could still filter by it, but a typed
                    // GetByKeyAsync signature needs a CLR type. Drop the whole key.
                    keyMembers.Clear();
                    break;
                }

                keyMembers.Add(new FluentMember(name, property.Type.ToDisplayString(displayFormat)));
            }

            var ns = symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString();

            models[clrName] = new FluentEntityModel(
                clrName,
                ns,
                symbol.Name,
                members.ToImmutable(),
                navs.ToImmutable(),
                keyMembers.ToImmutable());
        }

        return models.ToImmutable();
    }

    private static Dictionary<string, IPropertySymbol> CollectProperties(INamedTypeSymbol symbol)
    {
        var properties = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);
        for (var type = symbol; type != null; type = type.BaseType)
        {
            foreach (var member in type.GetMembers())
            {
                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsIndexer: false } property
                    && !properties.ContainsKey(property.Name))
                {
                    properties.Add(property.Name, property);
                }
            }
        }

        return properties;
    }

    public bool Equals(FluentEntityModel? other)
        => other != null
            && ClrName == other.ClrName
            && Namespace == other.Namespace
            && SimpleName == other.SimpleName
            && Members.SequenceEqual(other.Members)
            && Navs.SequenceEqual(other.Navs)
            && KeyMembers.SequenceEqual(other.KeyMembers);

    public override bool Equals(object? obj) => obj is FluentEntityModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = ClrName.GetHashCode();
            hash = hash * 31 + Namespace.GetHashCode();
            foreach (var member in Members) hash = hash * 31 + member.GetHashCode();
            foreach (var nav in Navs) hash = hash * 31 + nav.GetHashCode();
            foreach (var key in KeyMembers) hash = hash * 31 + key.GetHashCode();
            return hash;
        }
    }
}
