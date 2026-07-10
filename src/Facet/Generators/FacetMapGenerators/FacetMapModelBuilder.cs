using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Facet.Generators.FacetMapGenerators;

internal static class FacetMapModelBuilder
{
    public static ImmutableArray<FacetMapTargetModel?> BuildModels(
        GeneratorAttributeSyntaxContext context,
        GlobalConfigurationDefaults globalDefaults,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not INamedTypeSymbol targetSymbol) return ImmutableArray<FacetMapTargetModel?>.Empty;
        if (context.Attributes.Length == 0) return ImmutableArray<FacetMapTargetModel?>.Empty;

        var builder = ImmutableArray.CreateBuilder<FacetMapTargetModel?>(context.Attributes.Length);
        var attributeCount = context.Attributes.Length;
        for (int i = 0; i < context.Attributes.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            builder.Add(BuildModelForAttribute(targetSymbol, context.Attributes[i], globalDefaults, i, attributeCount));
        }
        return builder.ToImmutable();
    }

    private static FacetMapTargetModel? BuildModelForAttribute(
        INamedTypeSymbol markerSymbol,
        AttributeData attribute,
        GlobalConfigurationDefaults globalDefaults,
        int attributeIndex,
        int attributeCount)
    {
        if (attribute.ConstructorArguments.Length < 2) return null;

        var sourceType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        var targetType = attribute.ConstructorArguments[1].Value as INamedTypeSymbol;

        if (sourceType == null || targetType == null) return null;

        // Parse exclude array (3rd constructor param, params string[])
        var exclude = ImmutableHashSet<string>.Empty;
        if (attribute.ConstructorArguments.Length > 2 && !attribute.ConstructorArguments[2].IsNull)
        {
            exclude = attribute.ConstructorArguments[2].Values
                .Where(v => v.Value is string)
                .Select(v => (string)v.Value!)
                .ToImmutableHashSet();
        }

        // Parse named arguments
        var include = ImmutableHashSet<string>.Empty;
        bool generateToSource = globalDefaults.GenerateToSource;
        bool generateProjection = globalDefaults.GenerateProjection;
        int maxDepth = globalDefaults.MaxDepth;
        INamedTypeSymbol? configType = null;
        INamedTypeSymbol? toSourceConfigType = null;
        INamedTypeSymbol? beforeMapConfigType = null;
        INamedTypeSymbol? afterMapConfigType = null;
        INamedTypeSymbol? collectionTargetType = null;

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "Include":
                    if (!namedArg.Value.IsNull)
                    {
                        include = namedArg.Value.Values
                            .Where(v => v.Value is string)
                            .Select(v => (string)v.Value!)
                            .ToImmutableHashSet();
                    }
                    break;
                case "GenerateToSource":
                    generateToSource = namedArg.Value.Value is true;
                    break;
                case "GenerateProjection":
                    generateProjection = namedArg.Value.Value is bool gp && gp;
                    break;
                case "MaxDepth":
                    if (namedArg.Value.Value is int md) maxDepth = md;
                    break;
                case "Configuration":
                    configType = namedArg.Value.Value as INamedTypeSymbol;
                    break;
                case "ToSourceConfiguration":
                    toSourceConfigType = namedArg.Value.Value as INamedTypeSymbol;
                    break;
                case "BeforeMapConfiguration":
                    beforeMapConfigType = namedArg.Value.Value as INamedTypeSymbol;
                    break;
                case "AfterMapConfiguration":
                    afterMapConfigType = namedArg.Value.Value as INamedTypeSymbol;
                    break;
                case "CollectionTargetType":
                    collectionTargetType = namedArg.Value.Value as INamedTypeSymbol;
                    break;
            }
        }

        // Detect whether the configuration type implements IFacetMapConfiguration vs IFacetProjectionMapConfiguration
        var hasMapConfiguration = false;
        var hasProjectionMapConfiguration = false;
        if (configType != null)
        {
            var compilation = markerSymbol.ContainingAssembly;
            foreach (var iface in configType.AllInterfaces)
            {
                var ifaceName = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (ifaceName.Contains("IFacetMapConfiguration"))
                {
                    hasMapConfiguration = true;
                }
                if (ifaceName.Contains("IFacetProjectionMapConfiguration"))
                {
                    hasProjectionMapConfiguration = true;
                }
            }
        }

        // Resolve members by matching target properties to source properties
        var collectionTargetWrapper = ResolveCollectionTargetWrapper(collectionTargetType);
        var members = ResolveMappableMembers(sourceType, targetType, include, exclude, collectionTargetWrapper);

        // Allow empty members list if there's a configuration that will provide the mappings
        // (e.g., IFacetProjectionMapConfiguration where all mappings are defined via builder.Map())
        if (members.IsEmpty && configType == null) return null;

        var ns = markerSymbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : markerSymbol.ContainingNamespace?.ToDisplayString();

        var fullName = markerSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");

        var containingTypes = TypeAnalyzer.GetContainingTypes(markerSymbol);

        var sourceHasPositionalCtor = TypeAnalyzer.HasPositionalConstructor(sourceType);
        var targetHasParameterlessCtor = HasParameterlessConstructor(targetType);

        var accessibility = markerSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => "internal"
        };

        return new FacetMapTargetModel(
            markerClassName: markerSymbol.Name,
            ns: ns,
            fullName: fullName,
            accessibility: accessibility,
            sourceTypeName: sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            targetTypeName: targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            sourceTypeSimpleName: sourceType.Name,
            targetTypeSimpleName: targetType.Name,
            generateToSource: generateToSource,
            generateProjection: generateProjection,
            maxDepth: maxDepth,
            configurationTypeName: configType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            toSourceConfigurationTypeName: toSourceConfigType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            beforeMapConfigurationTypeName: beforeMapConfigType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            afterMapConfigurationTypeName: afterMapConfigType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            hasMapConfiguration: hasMapConfiguration,
            hasProjectionMapConfiguration: hasProjectionMapConfiguration,
            sourceHasPositionalConstructor: sourceHasPositionalCtor,
            targetHasParameterlessConstructor: targetHasParameterlessCtor,
            members: members,
            containingTypes: containingTypes,
            attributeIndex: attributeIndex,
            attributeCount: attributeCount);
    }

    private static ImmutableArray<FacetMapMember> ResolveMappableMembers(
        INamedTypeSymbol sourceType,
        INamedTypeSymbol targetType,
        ImmutableHashSet<string> include,
        ImmutableHashSet<string> exclude,
        string? collectionTargetWrapper = null)
    {
        // Get all target properties (these define what we map TO)
        var targetProperties = GetPublicProperties(targetType);

        // Get all source properties (these define what we map FROM)
        var sourceProperties = GetPublicProperties(sourceType)
            .ToDictionary(p => p.Name);

        var builder = ImmutableArray.CreateBuilder<FacetMapMember>();

        foreach (var targetProp in targetProperties)
        {
            var name = targetProp.Name;

            // Apply include/exclude filters on source property names
            if (!include.IsEmpty && !include.Contains(name)) continue;
            if (exclude.Contains(name)) continue;

            // Check for [MapFrom] attribute on target property to determine source property name
            string? mapFromSource = null;
            bool mapFromReversible = true;
            bool mapFromIncludeInProjection = true;
            var mapFromAttr = targetProp.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Facet.MapFromAttribute");
            if (mapFromAttr != null && mapFromAttr.ConstructorArguments.Length > 0)
            {
                mapFromSource = mapFromAttr.ConstructorArguments[0].Value as string;
                foreach (var namedArg in mapFromAttr.NamedArguments)
                {
                    switch (namedArg.Key)
                    {
                        case "Reversible":
                            mapFromReversible = namedArg.Value.Value is true;
                            break;
                        case "IncludeInProjection":
                            mapFromIncludeInProjection = namedArg.Value.Value is not false;
                            break;
                    }
                }
            }

            // Find matching source property (use MapFrom source if specified)
            var sourcePropertyName = mapFromSource ?? name;
            if (!sourceProperties.TryGetValue(sourcePropertyName, out var sourceProp)) continue;

            var typeName = GeneratorUtilities.GetTypeNameWithNullability(targetProp.Type);
            var isValueType = targetProp.Type.IsValueType;
            var isNullable = targetProp.Type.NullableAnnotation == NullableAnnotation.Annotated
                || (targetProp.Type is INamedTypeSymbol nts && nts.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T);

            var isTargetInitOnly = targetProp.SetMethod?.IsInitOnly == true;
            var isSourceInitOnly = sourceProp.SetMethod?.IsInitOnly == true;

            // Detect collections
            bool isCollection = false;
            string? collectionWrapper = null;
            string? sourceCollectionWrapper = null;
            string? collectionElementType = null;

            // Detect nested type differences (source and target have different types for same property)
            bool isNestedFacet = false;
            string? nestedFacetSourceTypeName = null;
            string? nestedTargetTypeSimpleName = null;
            string? nestedSourceTypeSimpleName = null;

            // Detect enum conversion (source is enum, target is string/int or vice versa)
            bool isEnumConversion = false;
            string? originalEnumTypeName = null;

            if (IsCollectionType(targetProp.Type, out var elementType, out var wrapper))
            {
                isCollection = true;
                // Apply CollectionTargetType override if specified
                var effectiveWrapper = collectionTargetWrapper ?? wrapper;
                collectionWrapper = effectiveWrapper;
                if (collectionTargetWrapper != null && collectionTargetWrapper != wrapper)
                {
                    sourceCollectionWrapper = wrapper; // original target wrapper becomes the "source" for reverse mapping
                }
                collectionElementType = elementType != null
                    ? GeneratorUtilities.GetTypeNameWithNullability(elementType)
                    : null;

                // Detect source collection wrapper if it differs from target
                if (IsCollectionType(sourceProp.Type, out var sourceElementType, out var sourceWrapper))
                {
                    if (sourceWrapper != wrapper)
                    {
                        sourceCollectionWrapper = sourceWrapper;
                    }

                    // Detect nested type mapping: source and target collection element types differ
                    if (elementType != null && sourceElementType != null
                        && !SymbolEqualityComparer.Default.Equals(elementType, sourceElementType))
                    {
                        // Check if this is an enum conversion (source element is enum, target element is string/int)
                        var sourceElem = UnwrapNullable(sourceElementType);
                        var targetElem = UnwrapNullable(elementType);

                        if (sourceElem.TypeKind == TypeKind.Enum && IsStringOrInt(targetElem))
                        {
                            isEnumConversion = true;
                            originalEnumTypeName = sourceElem.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        }
                        else if (targetElem.TypeKind == TypeKind.Enum && IsStringOrInt(sourceElem))
                        {
                            // Reverse: target is enum, source is string/int (unusual but valid)
                            isEnumConversion = true;
                            originalEnumTypeName = targetElem.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        }
                        else
                        {
                            isNestedFacet = true;
                            nestedFacetSourceTypeName = sourceElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            nestedTargetTypeSimpleName = elementType.Name;
                            nestedSourceTypeSimpleName = sourceElementType.Name;
                        }
                    }
                }
            }
            else
            {
                // Detect nested type mapping or enum conversion for non-collection properties
                var sourceUnwrapped = UnwrapNullable(sourceProp.Type);
                var targetUnwrapped = UnwrapNullable(targetProp.Type);

                if (!SymbolEqualityComparer.Default.Equals(sourceUnwrapped, targetUnwrapped))
                {
                    // Check for enum conversion: source is enum -> target is string/int
                    if (sourceUnwrapped.TypeKind == TypeKind.Enum && IsStringOrInt(targetUnwrapped))
                    {
                        isEnumConversion = true;
                        originalEnumTypeName = sourceUnwrapped.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    }
                    // Check for enum conversion: target is enum -> source is string/int (reverse scenario)
                    else if (targetUnwrapped.TypeKind == TypeKind.Enum && IsStringOrInt(sourceUnwrapped))
                    {
                        isEnumConversion = true;
                        originalEnumTypeName = targetUnwrapped.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    }
                    // Nested type mapping: both are non-primitive reference types with no special type,
                    // and neither is a known collection type (e.g., Dictionary<K,V>)
                    else if (!targetProp.Type.IsValueType
                        && targetProp.Type.SpecialType == SpecialType.None
                        && sourceProp.Type.SpecialType == SpecialType.None
                        && !IsDictionaryType(targetProp.Type)
                        && !IsDictionaryType(sourceProp.Type))
                    {
                        isNestedFacet = true;
                        nestedFacetSourceTypeName = sourceProp.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        nestedTargetTypeSimpleName = (targetProp.Type as INamedTypeSymbol)?.Name ?? targetProp.Type.Name;
                        nestedSourceTypeSimpleName = (sourceProp.Type as INamedTypeSymbol)?.Name ?? sourceProp.Type.Name;
                    }
                    // Types are incompatible (e.g., string vs complex type, or Dictionary types that differ) - skip this property
                    else
                    {
                        continue;
                    }
                }
            }

            // Detect MapWhen attributes on target property
            var mapWhenConditions = new System.Collections.Generic.List<string>();
            string? mapWhenDefault = null;
            foreach (var attr in targetProp.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == "Facet.MapWhenAttribute"
                    && attr.ConstructorArguments.Length > 0
                    && attr.ConstructorArguments[0].Value is string condition)
                {
                    mapWhenConditions.Add(condition);
                    foreach (var namedArg in attr.NamedArguments)
                    {
                        if (namedArg.Key == "Default" && namedArg.Value.Value != null)
                        {
                            mapWhenDefault = namedArg.Value.Value.ToString();
                        }
                    }
                }
            }

            // Detect source member type name for nullable conversions
            var sourceMemberTypeName = GeneratorUtilities.GetTypeNameWithNullability(sourceProp.Type);

            builder.Add(new FacetMapMember(
                name: name,
                typeName: typeName,
                sourcePropertyName: mapFromSource ?? name,
                isValueType: isValueType,
                isCollection: isCollection,
                collectionWrapper: collectionWrapper,
                sourceCollectionWrapper: sourceCollectionWrapper,
                collectionElementType: collectionElementType,
                isNullable: isNullable,
                isTargetInitOnly: isTargetInitOnly,
                isSourceInitOnly: isSourceInitOnly,
                isNestedFacet: isNestedFacet,
                nestedFacetSourceTypeName: nestedFacetSourceTypeName,
                sourceMemberTypeName: sourceMemberTypeName,
                nestedTargetTypeSimpleName: nestedTargetTypeSimpleName,
                nestedSourceTypeSimpleName: nestedSourceTypeSimpleName,
                mapFromSource: mapFromSource,
                mapFromReversible: mapFromReversible,
                mapFromIncludeInProjection: mapFromIncludeInProjection,
                mapWhenConditions: mapWhenConditions.Count > 0 ? mapWhenConditions : null,
                mapWhenDefault: mapWhenDefault,
                isEnumConversion: isEnumConversion,
                originalEnumTypeName: originalEnumTypeName));
        }

        return builder.ToImmutable();
    }

    private static System.Collections.Generic.List<IPropertySymbol> GetPublicProperties(INamedTypeSymbol type)
    {
        var properties = new System.Collections.Generic.List<IPropertySymbol>();
        var visited = new System.Collections.Generic.HashSet<string>();
        var current = type;

        while (current != null)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol prop
                    && prop.DeclaredAccessibility == Accessibility.Public
                    && !prop.IsStatic
                    && !prop.IsIndexer
                    && prop.GetMethod != null
                    && !visited.Contains(prop.Name))
                {
                    visited.Add(prop.Name);
                    properties.Add(prop);
                }
            }

            current = current.BaseType;
            if (current?.SpecialType == SpecialType.System_Object) break;
        }

        return properties;
    }

    private static bool HasParameterlessConstructor(INamedTypeSymbol type)
    {
        // Structs always have an implicit parameterless constructor
        if (type.IsValueType) return true;

        return type.Constructors.Any(c =>
            c.DeclaredAccessibility == Accessibility.Public
            && c.Parameters.IsEmpty
            && !c.IsImplicitlyDeclared == false)
            || type.Constructors.Any(c =>
                c.DeclaredAccessibility == Accessibility.Public
                && c.Parameters.IsEmpty);
    }

    private static bool IsCollectionType(ITypeSymbol type, out ITypeSymbol? elementType, out string? wrapper)
    {
        elementType = null;
        wrapper = null;

        if (type is IArrayTypeSymbol arrayType)
        {
            elementType = arrayType.ElementType;
            wrapper = "array";
            return true;
        }

        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var name = namedType.Name;
            var constructedFrom = namedType.ConstructedFrom.ToDisplayString();

            if (name == "List" || name == "IList" || name == "ICollection"
                || name == "IEnumerable" || name == "IReadOnlyList" || name == "IReadOnlyCollection"
                || name == "Collection" || constructedFrom.Contains("System.Collections.Generic"))
            {
                if (namedType.TypeArguments.Length == 1)
                {
                    elementType = namedType.TypeArguments[0];
                    wrapper = name;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Unwraps Nullable&lt;T&gt; to get the underlying type. Returns the original type if not nullable.
    /// </summary>
    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType
            && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return namedType.TypeArguments[0];
        }
        return type;
    }

    /// <summary>
    /// Checks if a type is a Dictionary or IDictionary type (multi-type-argument collections that cannot be auto-mapped).
    /// </summary>
    private static bool IsDictionaryType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var name = namedType.Name;
            if (name == "Dictionary" || name == "IDictionary"
                || name == "IReadOnlyDictionary" || name == "ConcurrentDictionary"
                || name == "SortedDictionary" || name == "ImmutableDictionary"
                || name == "ImmutableSortedDictionary")
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if a type is string or int (the two types enums can be converted to).
    /// </summary>
    private static bool IsStringOrInt(ITypeSymbol type)
    {
        return type.SpecialType == SpecialType.System_String
            || type.SpecialType == SpecialType.System_Int32;
    }

    /// <summary>
    /// Resolves the CollectionTargetType to a wrapper name string.
    /// </summary>
    private static string? ResolveCollectionTargetWrapper(INamedTypeSymbol? collectionTargetType)
    {
        if (collectionTargetType == null) return null;
        return collectionTargetType.Name switch
        {
            "List" => "List",
            "IList" => "IList",
            "ICollection" => "ICollection",
            "IEnumerable" => "IEnumerable",
            "IReadOnlyList" => "IReadOnlyList",
            "IReadOnlyCollection" => "IReadOnlyCollection",
            "Collection" => "Collection",
            "ImmutableArray" => "ImmutableArray",
            "ImmutableList" => "ImmutableList",
            _ => collectionTargetType.Name
        };
    }
}
