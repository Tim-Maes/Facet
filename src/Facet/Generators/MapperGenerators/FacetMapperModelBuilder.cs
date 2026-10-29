using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Facet.Generators.MapperGenerators;

/// <summary>
/// Outcome of resolving one <c>[assembly: FacetMapper(...)]</c> application: either a
/// registration to generate, or the reason it cannot be generated.
/// </summary>
internal sealed class MapperBuildResult
{
    private MapperBuildResult(MapperRegistration? registration, string? error, SourceLocationInfo? location)
    {
        Registration = registration;
        Error = error;
        Location = location;
    }

    public MapperRegistration? Registration { get; }

    public string? Error { get; }

    public SourceLocationInfo? Location { get; }

    public static MapperBuildResult Ok(MapperRegistration registration) =>
        new MapperBuildResult(registration, null, registration.Location);

    public static MapperBuildResult Invalid(string error, SourceLocationInfo? location) =>
        new MapperBuildResult(null, error, location);
}

/// <summary>
/// Resolves <c>[assembly: FacetMapper(...)]</c> registrations against the two types they name.
/// </summary>
/// <remarks>
/// Both types are read from metadata like any other referenced type, which is the whole point:
/// the facet type does not have to be in this compilation, so the mapper can be generated into an
/// assembly that merely references the contract assembly. Members are matched by name, because
/// that is all this side can see — the facet's own <c>[Facet]</c>/<c>[MapFrom]</c> attributes are
/// not necessarily in its metadata (see <c>Facet_EmitAttributeMetadata</c>), and a mapper that
/// depended on them would stop working for exactly the assemblies this feature exists to serve.
/// </remarks>
internal static class FacetMapperModelBuilder
{
    public static ImmutableArray<MapperBuildResult> BuildModels(
        GeneratorAttributeSyntaxContext context,
        CancellationToken token)
    {
        // ClassifyConversion (member assignability) is C#-specific, so the C# compilation is needed.
        var compilation = (CSharpCompilation)context.SemanticModel.Compilation;

        // Every registration in the compilation, not just the ones in this file: a nested facet
        // property is only mappable if its own pair is registered somewhere, and the attributes
        // may be spread across files.
        var registeredPairs = CollectRegisteredPairs(compilation);

        var results = ImmutableArray.CreateBuilder<MapperBuildResult>();

        foreach (var attribute in context.Attributes)
        {
            token.ThrowIfCancellationRequested();
            results.Add(BuildModel(attribute, compilation, registeredPairs));
        }

        return results.ToImmutable();
    }

    /// <summary>
    /// Maps "sourceType-&gt;facetType" to the facet type symbol, for resolving nested members.
    /// </summary>
    private static Dictionary<string, INamedTypeSymbol> CollectRegisteredPairs(CSharpCompilation compilation)
    {
        var pairs = new Dictionary<string, INamedTypeSymbol>();

        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            var attributeName = attribute.AttributeClass?.ToDisplayString();

            if (attributeName == FacetConstants.FacetMapperAttributeFullName)
            {
                if (!TryGetTypeArguments(attribute, out var sourceType, out var facetType))
                    continue;

                var key = MapperRegistration.MakePairKey(GlobalName(sourceType!), GlobalName(facetType!));
                if (!pairs.ContainsKey(key))
                    pairs.Add(key, facetType!);
            }
            else if (attributeName == FacetConstants.FacetMappersForAssemblyAttributeFullName)
            {
                // Nested members resolve against whatever is registered, including the pairs a
                // contract assembly's manifest contributes.
                foreach (var (_, source, facet) in ReadManifestPairs(attribute, compilation))
                {
                    var key = MapperRegistration.MakePairKey(GlobalName(source), GlobalName(facet));
                    if (!pairs.ContainsKey(key))
                        pairs.Add(key, facet);
                }
            }
        }

        return pairs;
    }

    /// <summary>
    /// Resolves the (facet, source) pairs a <c>[assembly: FacetMappersForAssembly]</c> target assembly
    /// records in its contract manifest, skipping any the attribute excludes or whose types can no
    /// longer be resolved.
    /// </summary>
    private static List<(string FacetFullName, INamedTypeSymbol Source, INamedTypeSymbol Facet)> ReadManifestPairs(
        AttributeData attribute,
        CSharpCompilation compilation)
    {
        var resolved = new List<(string, INamedTypeSymbol, INamedTypeSymbol)>();

        if (attribute.ConstructorArguments.Length < 1
            || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol marker)
        {
            return resolved;
        }

        var exclude = AttributeParser.GetStringArrayNamedArg(attribute.NamedArguments, "Exclude") ?? ImmutableArray<string>.Empty;

        foreach (var (facetFullName, sourceFullName) in ContractManifest.Read(marker.ContainingAssembly))
        {
            if (exclude.Contains(facetFullName) || exclude.Contains(SimpleName(facetFullName)))
                continue;

            var facet = compilation.GetTypeByMetadataName(facetFullName);
            var source = compilation.GetTypeByMetadataName(sourceFullName);

            if (facet is null || source is null)
                continue;

            resolved.Add((facetFullName, source, facet));
        }

        return resolved;
    }

    /// <summary>
    /// Builds a registration per manifest pair, for every <c>[assembly: FacetMappersForAssembly]</c> in
    /// this compilation unit. A pair also stated explicitly with <c>[assembly: FacetMapper]</c> is
    /// skipped, so the explicit registration (with its own name, exclusions or configuration) wins.
    /// </summary>
    public static ImmutableArray<MapperBuildResult> BuildModelsFromManifest(
        GeneratorAttributeSyntaxContext context,
        CancellationToken token)
    {
        var compilation = (CSharpCompilation)context.SemanticModel.Compilation;
        var registeredPairs = CollectRegisteredPairs(compilation);
        var explicitPairs = CollectExplicitPairKeys(compilation);

        var results = ImmutableArray.CreateBuilder<MapperBuildResult>();

        foreach (var attribute in context.Attributes)
        {
            token.ThrowIfCancellationRequested();

            var generateProjection = GetBoolArgument(attribute, "GenerateProjection", defaultValue: true);
            var generateToSource = GetBoolArgument(attribute, "GenerateToSource", defaultValue: false);
            var declaredNamespace = GetStringArgument(attribute, "Namespace");
            var location = SourceLocationInfo.FromAttribute(attribute);

            foreach (var (_, source, facet) in ReadManifestPairs(attribute, compilation))
            {
                if (explicitPairs.Contains(MapperRegistration.MakePairKey(GlobalName(source), GlobalName(facet))))
                    continue;

                results.Add(BuildModelFor(
                    source,
                    facet,
                    compilation,
                    registeredPairs,
                    exclude: new HashSet<string>(),
                    mapperName: null,
                    mapperNamespace: declaredNamespace,
                    generateProjection: generateProjection,
                    generateToSource: generateToSource,
                    configuration: MapperConfiguration.None,
                    location: location));
            }
        }

        return results.ToImmutable();
    }

    private static HashSet<string> CollectExplicitPairKeys(CSharpCompilation compilation)
    {
        var keys = new HashSet<string>();

        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != FacetConstants.FacetMapperAttributeFullName)
                continue;

            if (TryGetTypeArguments(attribute, out var source, out var facet))
                keys.Add(MapperRegistration.MakePairKey(GlobalName(source!), GlobalName(facet!)));
        }

        return keys;
    }


    private static string SimpleName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');

        return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
    }

    private static MapperBuildResult BuildModel(
        AttributeData attribute,
        CSharpCompilation compilation,
        Dictionary<string, INamedTypeSymbol> registeredPairs)
    {
        var location = SourceLocationInfo.FromAttribute(attribute);

        if (!TryGetTypeArguments(attribute, out var sourceType, out var facetType))
            return MapperBuildResult.Invalid("both a source type and a facet type are required", location);

        return BuildModelFor(
            sourceType!,
            facetType!,
            compilation,
            registeredPairs,
            exclude: GetExcludedNames(attribute),
            mapperName: GetStringArgument(attribute, "Name"),
            mapperNamespace: GetStringArgument(attribute, "Namespace"),
            generateProjection: GetBoolArgument(attribute, "GenerateProjection", defaultValue: true),
            generateToSource: GetBoolArgument(attribute, "GenerateToSource", defaultValue: false),
            configuration: ResolveConfiguration(attribute, compilation),
            location: location);
    }

    /// <summary>
    /// Resolves one (source, facet) pair into a registration, whatever named it — an explicit
    /// <c>[assembly: FacetMapper]</c> or a contract manifest entry reached through
    /// <c>[assembly: FacetMappersForAssembly]</c>.
    /// </summary>
    private static MapperBuildResult BuildModelFor(
        INamedTypeSymbol sourceType,
        INamedTypeSymbol facetType,
        CSharpCompilation compilation,
        Dictionary<string, INamedTypeSymbol> registeredPairs,
        HashSet<string> exclude,
        string? mapperName,
        string? mapperNamespace,
        bool generateProjection,
        bool generateToSource,
        MapperConfiguration configuration,
        SourceLocationInfo? location)
    {
        if (facetType.TypeKind == TypeKind.Interface || facetType.IsAbstract || facetType.IsStatic)
            return MapperBuildResult.Invalid(
                $"facet type '{facetType.ToDisplayString()}' must be a concrete, instantiable type", location);

        if (sourceType.IsStatic)
            return MapperBuildResult.Invalid(
                $"source type '{sourceType.ToDisplayString()}' must not be static", location);

        if (!facetType.InstanceConstructors.Any(IsUsableParameterlessConstructor))
            return MapperBuildResult.Invalid(
                $"facet type '{facetType.ToDisplayString()}' needs an accessible parameterless constructor so the " +
                "mapper can use an object initializer (a projection cannot call any other constructor); " +
                "keep Facet_GenerateParameterlessConstructor on, or declare one",
                location);

        var sourceProperties = GetReadableProperties(sourceType);

        var members = ImmutableArray.CreateBuilder<MappedMember>();
        var unmapped = ImmutableArray.CreateBuilder<UnmappedMember>();

        foreach (var facetProperty in GetSettableProperties(facetType))
        {
            if (exclude.Contains(facetProperty.Name))
                continue;

            if (!sourceProperties.TryGetValue(facetProperty.Name, out var sourceProperty))
            {
                // A configuration may well be filling this one by hand, in which case silence is
                // correct — the member is not unmapped, just mapped somewhere this cannot see.
                if (configuration.RequiresInstanceSteps)
                    continue;

                unmapped.Add(new UnmappedMember(
                    facetProperty.Name,
                    $"'{sourceType.ToDisplayString()}' has no public readable property of that name"));
                continue;
            }

            var member = ResolveMember(facetProperty, sourceProperty, compilation, registeredPairs, out var reason);
            if (member is null)
            {
                if (!configuration.RequiresInstanceSteps)
                    unmapped.Add(new UnmappedMember(facetProperty.Name, reason!));
            }
            else
            {
                members.Add(member);
            }
        }

        var registration = new MapperRegistration(
            sourceTypeName: GlobalName(sourceType),
            facetTypeName: GlobalName(facetType),
            sourceSimpleName: sourceType.Name,
            facetSimpleName: facetType.Name,
            mapperName: string.IsNullOrWhiteSpace(mapperName) ? facetType.Name + "Mapper" : mapperName!,
            mapperNamespace: mapperNamespace ?? NamespaceOf(facetType),
            generateProjection: generateProjection,
            generateToSource: generateToSource,
            sourceHasParameterlessConstructor: sourceType.InstanceConstructors.Any(IsUsableParameterlessConstructor),
            // Only the genuinely dangerous case: a [Facet] partial in this same compilation, whose
            // members FacetGenerator adds in a pass this generator cannot observe. A hand-written
            // type in this assembly is fully visible and needs no warning.
            facetIsInCurrentCompilation:
                SymbolEqualityComparer.Default.Equals(facetType.ContainingAssembly, compilation.Assembly)
                && facetType.GetAttributes().Any(a =>
                    a.AttributeClass?.ToDisplayString() == FacetConstants.FacetAttributeFullName),
            configuration: configuration,
            mappingPackageAvailable:
                compilation.GetTypeByMetadataName(FacetConstants.MapConfigurationInstanceInterfaceFullName) is not null,
            members: members.ToImmutable(),
            unmappedMembers: unmapped.ToImmutable(),
            location: location);

        return MapperBuildResult.Ok(registration);
    }

    /// <summary>
    /// Reads the hook types off the attribute, keeping only those that actually implement the
    /// interface the generated call will use. A type named but not implementing it is dropped
    /// rather than emitted, so the result is a compile error in the user's own code instead of in
    /// generated code they cannot edit.
    /// </summary>
    private static MapperConfiguration ResolveConfiguration(AttributeData attribute, CSharpCompilation compilation)
    {
        var configuration = GetTypeArgument(attribute, FacetConstants.AttributeNames.Configuration);
        var beforeMap = GetTypeArgument(attribute, FacetConstants.AttributeNames.BeforeMapConfiguration);
        var afterMap = GetTypeArgument(attribute, FacetConstants.AttributeNames.AfterMapConfiguration);
        var toSource = GetTypeArgument(attribute, FacetConstants.AttributeNames.ToSourceConfiguration);

        var mapName = Implements(configuration, FacetConstants.MapConfigurationInterfaceFullName, compilation)
            ? GlobalName(configuration!) : null;
        var projectionName = Implements(configuration, FacetConstants.ProjectionMapConfigurationInterfaceFullName, compilation)
            ? GlobalName(configuration!) : null;
        var beforeName = Implements(beforeMap, FacetConstants.BeforeMapConfigurationInterfaceFullName, compilation)
            ? GlobalName(beforeMap!) : null;
        var afterName = Implements(afterMap, FacetConstants.AfterMapConfigurationInterfaceFullName, compilation)
            ? GlobalName(afterMap!) : null;
        var toSourceName = Implements(toSource, FacetConstants.ToSourceConfigurationInterfaceFullName, compilation)
            ? GlobalName(toSource!) : null;

        if (mapName is null && projectionName is null && beforeName is null
            && afterName is null && toSourceName is null)
        {
            return MapperConfiguration.None;
        }

        return new MapperConfiguration(mapName, projectionName, beforeName, afterName, toSourceName);
    }

    private static bool Implements(INamedTypeSymbol? type, string interfaceMetadataName, CSharpCompilation compilation)
    {
        if (type is null)
            return false;

        var @interface = compilation.GetTypeByMetadataName(interfaceMetadataName);
        if (@interface is null)
            return false;

        return type.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, @interface));
    }

    private static INamedTypeSymbol? GetTypeArgument(AttributeData attribute, string name)
    {
        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == name)
                return named.Value.Value as INamedTypeSymbol;
        }

        return null;
    }

    /// <summary>
    /// Decides how one facet property is filled, or returns null with the reason it cannot be.
    /// </summary>
    private static MappedMember? ResolveMember(
        IPropertySymbol facetProperty,
        IPropertySymbol sourceProperty,
        CSharpCompilation compilation,
        Dictionary<string, INamedTypeSymbol> registeredPairs,
        out string? reason)
    {
        reason = null;

        var facetType = facetProperty.Type;
        var sourceType = sourceProperty.Type;
        var sourceIsSettable = IsPubliclySettable(sourceProperty);

        // The common case: the value carries across untouched.
        if (compilation.ClassifyConversion(sourceType, facetType).IsImplicit)
        {
            return new MappedMember(
                facetProperty.Name,
                MappedMemberKind.Direct,
                GeneratorUtilities.GetTypeNameWithNullability(facetType),
                sourceCanBeNull: CanBeNull(sourceType),
                sourceIsSettable: sourceIsSettable);
        }

        // A facet property that is itself a facet, where that pair is registered too.
        var nestedKey = MapperRegistration.MakePairKey(GlobalName(sourceType), GlobalName(facetType));
        if (registeredPairs.ContainsKey(nestedKey))
        {
            return new MappedMember(
                facetProperty.Name,
                MappedMemberKind.NestedFacet,
                GeneratorUtilities.GetTypeNameWithNullability(facetType),
                sourceCanBeNull: CanBeNull(sourceType),
                sourceIsSettable: sourceIsSettable,
                nestedSourceTypeName: GlobalName(sourceType),
                nestedFacetTypeName: GlobalName(facetType));
        }

        // Collections of nested facets.
        var facetIsCollection = GeneratorUtilities.TryGetCollectionElementType(
            facetType, out var facetElement, out var facetWrapper);
        var sourceIsCollection = GeneratorUtilities.TryGetCollectionElementType(
            sourceType, out var sourceElement, out _);

        if (facetIsCollection && sourceIsCollection && facetElement is not null && sourceElement is not null)
        {
            var elementKey = MapperRegistration.MakePairKey(GlobalName(sourceElement), GlobalName(facetElement));
            if (!registeredPairs.ContainsKey(elementKey))
            {
                reason = $"no mapper is registered from '{sourceElement.ToDisplayString()}' to " +
                         $"'{facetElement.ToDisplayString()}'; add [assembly: FacetMapper(typeof(" +
                         $"{sourceElement.ToDisplayString()}), typeof({facetElement.ToDisplayString()}))]";
                return null;
            }

            if (facetWrapper is null)
            {
                reason = $"collection type '{facetType.ToDisplayString()}' has no recognised collection shape";
                return null;
            }

            return new MappedMember(
                facetProperty.Name,
                MappedMemberKind.NestedFacetCollection,
                GeneratorUtilities.GetTypeNameWithNullability(facetType),
                sourceCanBeNull: CanBeNull(sourceType),
                sourceIsSettable: sourceIsSettable,
                nestedSourceTypeName: GlobalName(sourceElement),
                nestedFacetTypeName: GlobalName(facetElement),
                collectionWrapper: facetWrapper);
        }

        reason = $"'{sourceType.ToDisplayString()}' is not implicitly convertible to " +
                 $"'{facetType.ToDisplayString()}', and no mapper is registered between them";
        return null;
    }

    private static bool TryGetTypeArguments(
        AttributeData attribute,
        out INamedTypeSymbol? sourceType,
        out INamedTypeSymbol? facetType)
    {
        sourceType = null;
        facetType = null;

        if (attribute.ConstructorArguments.Length < 2)
            return false;

        sourceType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        facetType = attribute.ConstructorArguments[1].Value as INamedTypeSymbol;

        return sourceType is not null && facetType is not null;
    }

    private static HashSet<string> GetExcludedNames(AttributeData attribute)
    {
        var excluded = new HashSet<string>();

        if (attribute.ConstructorArguments.Length < 3)
            return excluded;

        foreach (var value in attribute.ConstructorArguments[2].Values)
        {
            if (value.Value is string name && !string.IsNullOrWhiteSpace(name))
                excluded.Add(name);
        }

        return excluded;
    }

    private static string? GetStringArgument(AttributeData attribute, string name)
    {
        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == name && named.Value.Value is string value && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static bool GetBoolArgument(AttributeData attribute, string name, bool defaultValue)
    {
        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == name && named.Value.Value is bool value)
                return value;
        }

        return defaultValue;
    }

    /// <summary>
    /// Properties an object initializer can set: public, non-static, non-indexer, with a public
    /// setter — <c>init</c> included, since an initializer can set those.
    /// </summary>
    /// <remarks>
    /// The inheritance walk and the most-derived-declaration-wins rule come from
    /// <see cref="GeneratorUtilities.GetAllMembersWithModifiers"/>, which the facet generator uses
    /// for the same purpose. Sharing it also picks up its handling of the partial implementing
    /// declarations that attributes like <c>[ObservableProperty]</c> emit (issue #277), which a
    /// separate walk here did not.
    /// </remarks>
    private static IEnumerable<IPropertySymbol> GetSettableProperties(INamedTypeSymbol type) =>
        GeneratorUtilities.GetAllMembersWithModifiers(type)
            .Select(m => m.Symbol)
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsIndexer && IsPubliclySettable(p));

    /// <summary>
    /// Readable properties by name, over the same shared inheritance walk as
    /// <see cref="GetSettableProperties"/>.
    /// </summary>
    private static Dictionary<string, IPropertySymbol> GetReadableProperties(INamedTypeSymbol type)
    {
        var properties = new Dictionary<string, IPropertySymbol>();

        foreach (var property in GeneratorUtilities.GetAllMembersWithModifiers(type)
                     .Select(m => m.Symbol)
                     .OfType<IPropertySymbol>())
        {
            if (property.IsIndexer)
                continue;

            if (property.GetMethod is null || property.GetMethod.DeclaredAccessibility != Accessibility.Public)
                continue;

            if (!properties.ContainsKey(property.Name))
                properties.Add(property.Name, property);
        }

        return properties;
    }

    private static bool IsPubliclySettable(IPropertySymbol property) =>
        property.SetMethod is not null && property.SetMethod.DeclaredAccessibility == Accessibility.Public;

    private static bool IsUsableParameterlessConstructor(IMethodSymbol constructor) =>
        constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility == Accessibility.Public;

    private static bool CanBeNull(ITypeSymbol type) =>
        type.IsReferenceType || type.NullableAnnotation == NullableAnnotation.Annotated;

    private static string GlobalName(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string NamespaceOf(INamedTypeSymbol type) =>
        type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : string.Empty;
}
