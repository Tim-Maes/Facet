using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace Facet.Generators.FacetGenerators;

/// <summary>
/// Validates Facet attribute configurations to catch errors early and provide helpful diagnostics.
/// </summary>
internal static class AttributeValidator
{
    /// <summary>
    /// Validates that the MaxDepth value is reasonable (not negative or excessively large).
    /// </summary>
    public static bool ValidateMaxDepth(int maxDepth, out string? errorMessage)
    {
        if (maxDepth < 0)
        {
            errorMessage = $"MaxDepth cannot be negative. Provided value: {maxDepth}";
            return false;
        }

        if (maxDepth > 100)
        {
            errorMessage = $"MaxDepth is unusually large ({maxDepth}). This may indicate a configuration error. " +
                          "Consider using a value between 0 and 10 for most scenarios.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Validates that a configuration type name refers to a valid type with the expected methods.
    /// </summary>
    public static bool ValidateConfigurationType(
        string? configurationTypeName,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol targetType,
        Compilation compilation,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(configurationTypeName))
        {
            errorMessage = null;
            return true; // No configuration type is valid
        }

        // Try to find the configuration type
        var configurationType = compilation.GetTypeByMetadataName(configurationTypeName);
        if (configurationType == null)
        {
            errorMessage = $"Configuration type '{configurationTypeName}' could not be found. " +
                          "Ensure the type is accessible and the namespace is correct.";
            return false;
        }

        // Check if it has a static Map method with the expected signature
        var mapMethod = configurationType.GetMembers("Map")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsStatic &&
                                m.Parameters.Length == 2 &&
                                SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, sourceType) &&
                                SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, targetType));

        if (mapMethod == null)
        {
            errorMessage = $"Configuration type '{configurationTypeName}' does not have a valid Map method. " +
                          $"Expected signature: static void Map({sourceType.Name} source, {targetType.Name} target)";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Validates that nested facets actually have the Facet attribute.
    /// </summary>
    public static bool ValidateNestedFacet(
        INamedTypeSymbol nestedFacetType,
        out string? errorMessage)
    {
        var facetAttribute = nestedFacetType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == FacetConstants.FacetAttributeFullName);

        if (facetAttribute == null)
        {
            errorMessage = $"Type '{nestedFacetType.Name}' is specified as a nested facet but does not have the [Facet] attribute. " +
                          "All nested facets must be properly annotated with the Facet attribute.";
            return false;
        }

        // Check if the nested facet has a valid source type
        if (facetAttribute.ConstructorArguments.Length == 0 ||
            facetAttribute.ConstructorArguments[0].Value is not INamedTypeSymbol)
        {
            errorMessage = $"Nested facet '{nestedFacetType.Name}' has an invalid Facet attribute. " +
                          "The Facet attribute must specify a source type.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Validates that include and exclude lists don't overlap.
    /// </summary>
    public static bool ValidateIncludeExcludeLists(
        System.Collections.Generic.HashSet<string> excluded,
        System.Collections.Generic.HashSet<string> included,
        bool isIncludeMode,
        out string? errorMessage)
    {
        if (isIncludeMode && excluded.Count > 0)
        {
            errorMessage = "Cannot specify both Include and Exclude parameters. Use either Include to specify what to include, " +
                          "or the exclude parameter to specify what to exclude, but not both.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Validates that PreserveReferences and MaxDepth settings are compatible.
    /// </summary>
    public static bool ValidateCircularReferenceSettings(
        int maxDepth,
        bool preserveReferences,
        bool hasNestedFacets,
        out string? warningMessage)
    {
        warningMessage = null;

        // If there are no nested facets, circular reference settings don't matter
        if (!hasNestedFacets)
        {
            return true;
        }

        // If PreserveReferences is false and MaxDepth is 0, warn about potential stack overflow
        if (!preserveReferences && maxDepth == 0)
        {
            warningMessage = "PreserveReferences is disabled and MaxDepth is set to 0 (unlimited). " +
                            "This configuration may cause stack overflow exceptions with circular references. " +
                            "Consider enabling PreserveReferences or setting a MaxDepth limit.";
            return true; // This is a warning, not an error
        }

        return true;
    }
}
