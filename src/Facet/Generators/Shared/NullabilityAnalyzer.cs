using Microsoft.CodeAnalysis;

namespace Facet.Generators.Shared;

/// <summary>
/// Analyzes and provides utilities for handling nullable types consistently across the generator.
/// </summary>
internal static class NullabilityAnalyzer
{
    /// <summary>
    /// Checks if a type name represents a nullable type (ends with '?').
    /// </summary>
    public static bool IsNullableTypeName(string typeName)
    {
        return !string.IsNullOrEmpty(typeName) && typeName.EndsWith("?");
    }

    /// <summary>
    /// Checks if a type symbol is nullable based on its annotation.
    /// </summary>
    public static bool IsNullableType(ITypeSymbol type)
    {
        return type.NullableAnnotation == NullableAnnotation.Annotated;
    }

    /// <summary>
    /// Checks if a type is a nullable reference type, considering both annotation and type kind.
    /// </summary>
    public static bool IsNullableReferenceType(ITypeSymbol type)
    {
        return !type.IsValueType && type.NullableAnnotation == NullableAnnotation.Annotated;
    }

    /// <summary>
    /// Checks if a type should be treated as nullable for safety.
    /// This includes explicitly nullable types and reference types without explicit non-null annotation.
    /// </summary>
    public static bool ShouldTreatAsNullable(ITypeSymbol type, bool isRequired = false)
    {
        // Value types with nullable annotation are explicitly nullable
        if (type.IsValueType)
        {
            return type.NullableAnnotation == NullableAnnotation.Annotated;
        }

        // Reference types
        // If explicitly annotated as nullable
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        // If explicitly annotated as non-nullable AND required, treat as non-nullable
        if (type.NullableAnnotation == NullableAnnotation.NotAnnotated && isRequired)
        {
            return false;
        }

        // For safety, treat other reference types as potentially nullable
        return true;
    }

    /// <summary>
    /// Removes the nullable marker ('?') from a type name if present.
    /// </summary>
    public static string StripNullableMarker(string typeName)
    {
        return typeName.TrimEnd('?');
    }

    /// <summary>
    /// Makes a type name nullable by adding '?' if it's not already nullable.
    /// </summary>
    public static string MakeNullable(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return typeName;
        }

        return IsNullableTypeName(typeName) ? typeName : typeName + "?";
    }
}
