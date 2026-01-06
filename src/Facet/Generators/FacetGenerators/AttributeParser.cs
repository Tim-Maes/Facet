using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet.Generators;

/// <summary>
/// Handles parsing of Facet attribute data and extraction of configuration values.
/// </summary>
internal static class AttributeParser
{
    /// <summary>
    /// Extracts nested facet mappings from the NestedFacets parameter.
    /// Returns a dictionary mapping source type full names to nested facet type information.
    /// </summary>
    public static Dictionary<string, (string childFacetTypeName, string sourceTypeName)> ExtractNestedFacetMappings(
        AttributeData attribute,
        Compilation compilation)
    {
        var mappings = new Dictionary<string, (string, string)>();

        var childrenArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == FacetConstants.AttributeNames.NestedFacets);
        if (childrenArg.Value.Kind != TypedConstantKind.Error && !childrenArg.Value.IsNull)
        {
            if (childrenArg.Value.Kind == TypedConstantKind.Array)
            {
                foreach (var childValue in childrenArg.Value.Values)
                {
                    if (childValue.Value is INamedTypeSymbol childFacetType)
                    {
                        // Find the Facet attribute on the child type to get its source type
                        var childFacetAttr = childFacetType.GetAttributes()
                            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == FacetConstants.FacetAttributeFullName);

                        if (childFacetAttr != null && childFacetAttr.ConstructorArguments.Length > 0)
                        {
                            if (childFacetAttr.ConstructorArguments[0].Value is INamedTypeSymbol childSourceType)
                            {
                                var sourceTypeName = childSourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                var childFacetTypeName = childFacetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                                // Map the source type to the child facet type
                                mappings[sourceTypeName] = (childFacetTypeName, sourceTypeName);
                            }
                        }
                    }
                }
            }
        }

        return mappings;
    }

    /// <summary>
    /// Extracts nested wrapper mappings from the NestedWrappers parameter.
    /// Returns a dictionary mapping source type full names to nested wrapper type information.
    /// </summary>
    public static Dictionary<string, (string childWrapperTypeName, string sourceTypeName)> ExtractNestedWrapperMappings(
        AttributeData attribute,
        Compilation compilation)
    {
        var mappings = new Dictionary<string, (string, string)>();

        var childrenArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == FacetConstants.AttributeNames.NestedWrappers);
        if (childrenArg.Value.Kind != TypedConstantKind.Error && !childrenArg.Value.IsNull)
        {
            if (childrenArg.Value.Kind == TypedConstantKind.Array)
            {
                foreach (var childValue in childrenArg.Value.Values)
                {
                    if (childValue.Value is INamedTypeSymbol childWrapperType)
                    {
                        // Find the Wrapper attribute on the child type to get its source type
                        var childWrapperAttr = childWrapperType.GetAttributes()
                            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == FacetConstants.WrapperAttributeFullName);

                        if (childWrapperAttr != null && childWrapperAttr.ConstructorArguments.Length > 0)
                        {
                            if (childWrapperAttr.ConstructorArguments[0].Value is INamedTypeSymbol childSourceType)
                            {
                                var sourceTypeName = childSourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                var childWrapperTypeName = childWrapperType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                                // Map the source type to the child wrapper type
                                mappings[sourceTypeName] = (childWrapperTypeName, sourceTypeName);
                            }
                        }
                    }
                }
            }
        }

        return mappings;
    }

    /// <summary>
    /// Gets a named argument value from the attribute, or returns the default value if not found.
    /// </summary>
    public static T GetNamedArg<T>(
        ImmutableArray<KeyValuePair<string, TypedConstant>> args,
        string name,
        T defaultValue)
        => args.FirstOrDefault(kv => kv.Key == name)
            .Value.Value is T t ? t : defaultValue;

    /// <summary>
    /// Checks if a named argument exists in the attribute.
    /// </summary>
    public static bool HasNamedArg(
        ImmutableArray<KeyValuePair<string, TypedConstant>> args,
        string name)
        => args.Any(kv => kv.Key == name);

    /// <summary>
    /// Extracts the excluded members list from the attribute constructor arguments.
    /// </summary>
    public static HashSet<string> ExtractExcludedMembers(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length > 1)
        {
            var excludeArg = attribute.ConstructorArguments[1];
            if (excludeArg.Kind == TypedConstantKind.Array)
            {
                return new HashSet<string>(
                    excludeArg.Values
                        .Select(v => v.Value?.ToString())
                        .Where(n => n != null)!);
            }
        }

        return new HashSet<string>();
    }

    /// <summary>
    /// Extracts the included members list from the attribute named arguments.
    /// </summary>
    public static (HashSet<string> includedMembers, bool isIncludeMode) ExtractIncludedMembers(AttributeData attribute)
    {
        var includeArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == FacetConstants.AttributeNames.Include);
        if (includeArg.Value.Kind != TypedConstantKind.Error && !includeArg.Value.IsNull)
        {
            if (includeArg.Value.Kind == TypedConstantKind.Array)
            {
                var included = new HashSet<string>(
                    includeArg.Value.Values
                        .Select(v => v.Value?.ToString())
                        .Where(n => n != null)!);
                return (included, true);
            }
        }

        return (new HashSet<string>(), false);
    }

    /// <summary>
    /// Extracts the configuration type name from the attribute.
    /// </summary>
    public static string? ExtractConfigurationTypeName(AttributeData attribute)
    {
        return attribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == FacetConstants.AttributeNames.Configuration)
            .Value.Value?
            .ToString();
    }

    /// <summary>
    /// Extracts the BeforeMapConfiguration type name from the attribute.
    /// </summary>
    public static string? ExtractBeforeMapConfigurationTypeName(AttributeData attribute)
    {
        var arg = attribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == FacetConstants.AttributeNames.BeforeMapConfiguration);
        
        if (arg.Value.Value is INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
        
        return null;
    }

    /// <summary>
    /// Extracts the AfterMapConfiguration type name from the attribute.
    /// </summary>
    public static string? ExtractAfterMapConfigurationTypeName(AttributeData attribute)
    {
        var arg = attribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == FacetConstants.AttributeNames.AfterMapConfiguration);
        
        if (arg.Value.Value is INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
        
        return null;
    }

    /// <summary>
    /// Extracts the FlattenTo types from the FlattenTo parameter.
    /// Returns a list of fully qualified type names of flatten target types.
    /// </summary>
    public static ImmutableArray<string> ExtractFlattenToTypes(AttributeData attribute)
    {
        var flattenToArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == FacetConstants.AttributeNames.FlattenTo);
        if (flattenToArg.Value.Kind != TypedConstantKind.Error && !flattenToArg.Value.IsNull)
        {
            if (flattenToArg.Value.Kind == TypedConstantKind.Array)
            {
                var types = new List<string>();
                foreach (var typeValue in flattenToArg.Value.Values)
                {
                    if (typeValue.Value is INamedTypeSymbol flattenToType)
                    {
                        var typeName = flattenToType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        types.Add(typeName);
                    }
                }
                return types.ToImmutableArray();
            }
        }

        return ImmutableArray<string>.Empty;
    }
}
