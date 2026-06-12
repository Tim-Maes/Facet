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
        foreach (var attribute in context.Attributes)
        {
            token.ThrowIfCancellationRequested();
            builder.Add(BuildModelForAttribute(targetSymbol, attribute, globalDefaults));
        }
        return builder.ToImmutable();
    }

    private static FacetMapTargetModel? BuildModelForAttribute(
        INamedTypeSymbol markerSymbol,
        AttributeData attribute,
        GlobalConfigurationDefaults globalDefaults)
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
            }
        }

        // Resolve members by matching target properties to source properties
        var members = ResolveMappableMembers(sourceType, targetType, include, exclude);

        if (members.IsEmpty) return null;

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
            sourceHasPositionalConstructor: sourceHasPositionalCtor,
            targetHasParameterlessConstructor: targetHasParameterlessCtor,
            members: members,
            containingTypes: containingTypes);
    }

    private static ImmutableArray<FacetMapMember> ResolveMappableMembers(
        INamedTypeSymbol sourceType,
        INamedTypeSymbol targetType,
        ImmutableHashSet<string> include,
        ImmutableHashSet<string> exclude)
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

            // Find matching source property
            if (!sourceProperties.TryGetValue(name, out var sourceProp)) continue;

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

            if (IsCollectionType(targetProp.Type, out var elementType, out var wrapper))
            {
                isCollection = true;
                collectionWrapper = wrapper;
                collectionElementType = elementType != null
                    ? GeneratorUtilities.GetTypeNameWithNullability(elementType)
                    : null;

                // Detect source collection wrapper if it differs from target
                if (IsCollectionType(sourceProp.Type, out _, out var sourceWrapper) && sourceWrapper != wrapper)
                {
                    sourceCollectionWrapper = sourceWrapper;
                }
            }

            // Detect source member type name for nullable conversions
            var sourceMemberTypeName = GeneratorUtilities.GetTypeNameWithNullability(sourceProp.Type);

            builder.Add(new FacetMapMember(
                name: name,
                typeName: typeName,
                sourcePropertyName: name,
                isValueType: isValueType,
                isCollection: isCollection,
                collectionWrapper: collectionWrapper,
                sourceCollectionWrapper: sourceCollectionWrapper,
                collectionElementType: collectionElementType,
                isNullable: isNullable,
                isTargetInitOnly: isTargetInitOnly,
                isSourceInitOnly: isSourceInitOnly,
                sourceMemberTypeName: sourceMemberTypeName));
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
}
