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
    private const string FacetAttributeName = "Facet.FacetAttribute";

    /// <summary>
    /// Extracts nested facet mappings from the NestedFacets parameter.
    /// Returns a dictionary mapping source type full names to nested facet type information.
    /// </summary>
    public static Dictionary<string, (string childFacetTypeName, string sourceTypeName)> ExtractNestedFacetMappings(
        AttributeData attribute,
        Compilation compilation)
    {
        var mappings = new Dictionary<string, (string, string)>();

        var childrenArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "NestedFacets");
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
                            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == FacetAttributeName);

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
    /// Gets a named argument value from the attribute, or returns the default value if not found.
    /// </summary>
    public static T GetNamedArg<T>(
        ImmutableArray<KeyValuePair<string, TypedConstant>> args,
        string name,
        T defaultValue)
        => args.FirstOrDefault(kv => kv.Key == name)
            .Value.Value is T t ? t : defaultValue;

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
        var includeArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Include");
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
            .FirstOrDefault(kvp => kvp.Key == "Configuration")
            .Value.Value?
            .ToString();
    }
}
