using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Facet.Generators.Shared;

/// <summary>
/// Shared utility methods for Facet source generators.
/// </summary>
internal static class GeneratorUtilities
{
    /// <summary>
    /// The global namespace prefix used in fully qualified type names.
    /// </summary>
    public const string GlobalPrefix = "global::";

    /// <summary>
    /// Strips the global:: prefix from a type name if present.
    /// </summary>
    public static string StripGlobalPrefix(string typeName)
    {
        return typeName.StartsWith(GlobalPrefix)
            ? typeName.Substring(GlobalPrefix.Length)
            : typeName;
    }

    /// <summary>
    /// Gets the type name with proper nullability information preserved.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to get the name from.</param>
    /// <returns>A fully qualified type name with nullability annotations.</returns>
    public static string GetTypeNameWithNullability(ITypeSymbol typeSymbol)
    {
        // Create a SymbolDisplayFormat that includes nullability information
        var format = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
            memberOptions: SymbolDisplayMemberOptions.None,
            delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
            extensionMethodStyle: SymbolDisplayExtensionMethodStyle.Default,
            parameterOptions: SymbolDisplayParameterOptions.None,
            propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
            localOptions: SymbolDisplayLocalOptions.None,
            kindOptions: SymbolDisplayKindOptions.None,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                                 SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                 SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        return typeSymbol.ToDisplayString(format);
    }

    /// <summary>
    /// Gets all members from the inheritance hierarchy, starting from the most derived type
    /// and walking up to the base types. This ensures that overridden members are preferred.
    /// </summary>
    /// <param name="type">The type symbol to get members from.</param>
    /// <returns>An enumerable of tuples containing the symbol, init-only flag, and required flag.</returns>
    public static IEnumerable<(ISymbol Symbol, bool IsInitOnly, bool IsRequired)> GetAllMembersWithModifiers(INamedTypeSymbol type)
    {
        var visited = new HashSet<string>();
        var current = type;

        while (current != null)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.DeclaredAccessibility == Accessibility.Public &&
                    !visited.Contains(member.Name))
                {
                    if (member is IPropertySymbol prop)
                    {
                        visited.Add(member.Name);
                        var isInitOnly = prop.SetMethod?.IsInitOnly == true;
                        var isRequired = prop.IsRequired;
                        yield return (prop, isInitOnly, isRequired);
                    }
                    else if (member is IFieldSymbol field)
                    {
                        visited.Add(member.Name);
                        var isRequired = field.IsRequired;
                        yield return (field, false, isRequired);
                    }
                }
            }

            current = current.BaseType;

            if (current?.SpecialType == SpecialType.System_Object)
                break;
        }
    }

    /// <summary>
    /// Determines if a type is a value type based on its name.
    /// </summary>
    /// <param name="typeName">The fully qualified or simple type name.</param>
    /// <returns>True if the type is a value type; otherwise, false.</returns>
    public static bool IsValueType(string typeName)
    {
        return typeName switch
        {
            "bool" or "System.Boolean" => true,
            "byte" or "System.Byte" => true,
            "sbyte" or "System.SByte" => true,
            "char" or "System.Char" => true,
            "decimal" or "System.Decimal" => true,
            "double" or "System.Double" => true,
            "float" or "System.Single" => true,
            "int" or "System.Int32" => true,
            "uint" or "System.UInt32" => true,
            "long" or "System.Int64" => true,
            "ulong" or "System.UInt64" => true,
            "short" or "System.Int16" => true,
            "ushort" or "System.UInt16" => true,
            "System.DateTime" => true,
            "System.DateTimeOffset" => true,
            "System.TimeSpan" => true,
            "System.Guid" => true,
            _ when typeName.StartsWith("System.Enum") => true,
            _ when typeName.EndsWith("Enum") => true,  // Simple heuristic for enums
            _ => false
        };
    }

    /// <summary>
    /// Makes a type nullable by appending a '?' suffix if it's not already nullable.
    /// </summary>
    /// <param name="typeName">The type name to make nullable.</param>
    /// <returns>The nullable version of the type name.</returns>
    public static string MakeNullable(string typeName)
    {
        // Don't make already nullable types more nullable
        if (typeName.EndsWith("?") || typeName.StartsWith("System.Nullable<"))
            return typeName;

        // Always add ? to make the type nullable
        return typeName + "?";
    }

    /// <summary>
    /// Gets the appropriate default value expression for a given type name.
    /// Used for parameterless constructor generation and required field initialization.
    /// </summary>
    /// <param name="typeName">The fully qualified or simple type name.</param>
    /// <returns>A C# expression string representing the default value.</returns>
    public static string GetDefaultValueForType(string typeName)
    {
        // Remove global:: prefix if present
        var cleanTypeName = StripGlobalPrefix(typeName);

        // Handle nullable types
        if (cleanTypeName.EndsWith("?"))
        {
            return "null";
        }

        // Handle Nullable<T>
        if (cleanTypeName.StartsWith("System.Nullable<"))
        {
            return "null";
        }

        return cleanTypeName switch
        {
            // String types
            "string" or "System.String" => "string.Empty",

            // Numeric types
            "int" or "System.Int32" => "0",
            "long" or "System.Int64" => "0L",
            "short" or "System.Int16" => "(short)0",
            "byte" or "System.Byte" => "(byte)0",
            "sbyte" or "System.SByte" => "(sbyte)0",
            "uint" or "System.UInt32" => "0U",
            "ulong" or "System.UInt64" => "0UL",
            "ushort" or "System.UInt16" => "(ushort)0",
            "float" or "System.Single" => "0.0f",
            "double" or "System.Double" => "0.0",
            "decimal" or "System.Decimal" => "0.0m",

            // Other value types
            "bool" or "System.Boolean" => "false",
            "char" or "System.Char" => "'\\0'",
            "System.DateTime" => "default(System.DateTime)",
            "System.DateTimeOffset" => "default(System.DateTimeOffset)",
            "System.TimeSpan" => "default(System.TimeSpan)",
            "System.Guid" => "default(System.Guid)",

            // Collection types
            _ when cleanTypeName.StartsWith("System.Collections.Generic.List<") => $"new {cleanTypeName}()",
            _ when cleanTypeName.StartsWith("System.Collections.Generic.IList<") =>
                $"new System.Collections.Generic.List<{cleanTypeName.Substring("System.Collections.Generic.IList<".Length).TrimEnd('>')}>()",
            _ when cleanTypeName.StartsWith("List<") => $"new {cleanTypeName}()",
            _ when cleanTypeName.StartsWith("IList<") =>
                $"new List<{cleanTypeName.Substring("IList<".Length).TrimEnd('>')}>()",

            // Default for unknown types
            _ when IsValueType(cleanTypeName) => $"default({cleanTypeName})",
            _ => "null" // Reference types default to null
        };
    }

    /// <summary>
    /// Gets a default value suitable for parameterless constructor initialization.
    /// Similar to GetDefaultValueForType but with slight variations for constructor contexts.
    /// </summary>
    /// <param name="typeName">The type name.</param>
    /// <returns>A C# expression string representing the default value.</returns>
    public static string GetDefaultValue(string typeName)
    {
        // Handle nullable types - use typed null to avoid ambiguity with record copy constructors
        if (typeName.EndsWith("?"))
        {
            return $"({typeName})null";
        }

        // Handle common value types
        return typeName switch
        {
            "bool" => "false",
            "byte" => "0",
            "sbyte" => "0",
            "short" => "0",
            "ushort" => "0",
            "int" => "0",
            "uint" => "0",
            "long" => "0",
            "ulong" => "0",
            "float" => "0f",
            "double" => "0d",
            "decimal" => "0m",
            "char" => "'\\0'",
            "string" => "string.Empty",
            var t when t.StartsWith("System.DateTime") => "default",
            var t when t.StartsWith("System.DateTimeOffset") => "default",
            var t when t.StartsWith("System.TimeSpan") => "default",
            var t when t.StartsWith("System.Guid") => "default",
            // For value types, use default() expression
            var t when IsValueType(t) => $"default({typeName})",
            // For reference types (collections, objects, etc.), use typed default to avoid 
            // ambiguity with record copy constructors when null could match multiple parameter types
            _ => $"default({typeName})!"
        };
    }

    /// <summary>
    /// Attempts to extract the element type from a collection type.
    /// Handles List&lt;T&gt;, ICollection&lt;T&gt;, IEnumerable&lt;T&gt;, IList&lt;T&gt;, IReadOnlyList&lt;T&gt;, IReadOnlyCollection&lt;T&gt;, and T[].
    /// </summary>
    /// <param name="typeSymbol">The type symbol to analyze.</param>
    /// <param name="elementType">The extracted element type symbol if successful.</param>
    /// <param name="collectionWrapper">The collection type wrapper (e.g., "List", "ICollection", "IReadOnlyList", "array").</param>
    /// <returns>True if the type is a collection; otherwise, false.</returns>
    public static bool TryGetCollectionElementType(ITypeSymbol typeSymbol, out ITypeSymbol? elementType, out string? collectionWrapper)
    {
        elementType = null;
        collectionWrapper = null;

        // Check for array type
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            elementType = arrayType.ElementType;
            collectionWrapper = "array";
            return true;
        }

        // Check for generic collection types
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var typeDefinition = namedType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Check for List<T>
            if (typeDefinition == "global::System.Collections.Generic.List<T>")
            {
                elementType = namedType.TypeArguments[0];
                collectionWrapper = "List";
                return true;
            }

            // Check for ICollection<T>
            if (typeDefinition == "global::System.Collections.Generic.ICollection<T>")
            {
                elementType = namedType.TypeArguments[0];
                collectionWrapper = "ICollection";
                return true;
            }

            // Check for IList<T>
            if (typeDefinition == "global::System.Collections.Generic.IList<T>")
            {
                elementType = namedType.TypeArguments[0];
                collectionWrapper = "IList";
                return true;
            }

            // Check for IEnumerable<T>
            if (typeDefinition == "global::System.Collections.Generic.IEnumerable<T>")
            {
                elementType = namedType.TypeArguments[0];
                collectionWrapper = "IEnumerable";
                return true;
            }

            // Check for IReadOnlyList<T>
            if (typeDefinition == "global::System.Collections.Generic.IReadOnlyList<T>")
            {
                elementType = namedType.TypeArguments[0];
                collectionWrapper = "IReadOnlyList";
                return true;
            }

            // Check for IReadOnlyCollection<T>
            if (typeDefinition == "global::System.Collections.Generic.IReadOnlyCollection<T>")
            {
                elementType = namedType.TypeArguments[0];
                collectionWrapper = "IReadOnlyCollection";
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Wraps an element type name in the appropriate collection type.
    /// </summary>
    /// <param name="elementTypeName">The fully qualified element type name.</param>
    /// <param name="collectionWrapper">The collection wrapper type ("List", "ICollection", "IList", "IEnumerable", "IReadOnlyList", "IReadOnlyCollection", "array").</param>
    /// <returns>The fully qualified collection type name.</returns>
    public static string WrapInCollectionType(string elementTypeName, string collectionWrapper)
    {
        return collectionWrapper switch
        {
            "List" => $"global::System.Collections.Generic.List<{elementTypeName}>",
            "ICollection" => $"global::System.Collections.Generic.ICollection<{elementTypeName}>",
            "IList" => $"global::System.Collections.Generic.IList<{elementTypeName}>",
            "IEnumerable" => $"global::System.Collections.Generic.IEnumerable<{elementTypeName}>",
            "IReadOnlyList" => $"global::System.Collections.Generic.IReadOnlyList<{elementTypeName}>",
            "IReadOnlyCollection" => $"global::System.Collections.Generic.IReadOnlyCollection<{elementTypeName}>",
            "array" => $"{elementTypeName}[]",
            _ => elementTypeName
        };
    }
}
