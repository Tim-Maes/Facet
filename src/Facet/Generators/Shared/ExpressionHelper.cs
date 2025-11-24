using System.Text;

namespace Facet.Generators.Shared;

/// <summary>
/// Provides shared utility methods for expression parsing and transformation.
/// Used by ExpressionBuilder and ProjectionGenerator to avoid code duplication.
/// </summary>
internal static class ExpressionHelper
{
    private static readonly char[] ExpressionChars = { ' ', '+', '-', '*', '/', '(', '?', ':' };

    /// <summary>
    /// Determines if the source string is an expression (contains operators, spaces, etc.)
    /// rather than a simple property name.
    /// </summary>
    public static bool IsExpression(string source)
    {
        return source.IndexOfAny(ExpressionChars) >= 0;
    }

    /// <summary>
    /// Transforms a MapFrom expression by prefixing identifiers with the source variable name.
    /// For example: "FirstName + \" \" + LastName" becomes "source.FirstName + \" \" + source.LastName"
    /// </summary>
    public static string TransformExpression(string expression, string sourceVariableName)
    {
        var result = new StringBuilder();
        var identifier = new StringBuilder();
        bool inString = false;
        char stringChar = '\0';

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];

            // Track string literals
            if ((c == '"' || c == '\'') && (i == 0 || expression[i - 1] != '\\'))
            {
                if (!inString)
                {
                    inString = true;
                    stringChar = c;
                }
                else if (c == stringChar)
                {
                    inString = false;
                }

                FlushIdentifier(result, identifier, sourceVariableName, expression, i);
                result.Append(c);
                continue;
            }

            if (inString)
            {
                result.Append(c);
                continue;
            }

            // Build identifiers
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                identifier.Append(c);
            }
            else
            {
                FlushIdentifier(result, identifier, sourceVariableName, expression, i);
                result.Append(c);
            }
        }

        FlushIdentifier(result, identifier, sourceVariableName, expression, expression.Length);
        return result.ToString();
    }

    /// <summary>
    /// Flushes an accumulated identifier to the result, prefixing with source variable if needed.
    /// </summary>
    private static void FlushIdentifier(StringBuilder result, StringBuilder identifier, string sourceVariableName, string expression, int currentIndex)
    {
        if (identifier.Length > 0)
        {
            var id = identifier.ToString();
            // Don't prefix keywords, numbers, type names (followed by '.'), or member access (preceded by '.')
            var identifierStartIndex = currentIndex - identifier.Length;
            var isPrecededByDot = identifierStartIndex > 0 && expression[identifierStartIndex - 1] == '.';

            if (!IsKeyword(id) && !char.IsDigit(id[0]) && !IsLikelyTypeName(id, expression, currentIndex) && !isPrecededByDot)
            {
                result.Append($"{sourceVariableName}.");
            }
            result.Append(id);
            identifier.Clear();
        }
    }

    /// <summary>
    /// Determines if the identifier is a C# keyword that should not be prefixed.
    /// </summary>
    public static bool IsKeyword(string identifier)
    {
        return identifier switch
        {
            "true" or "false" or "null" or "new" or "typeof" or "nameof" or
            "is" or "as" or "in" or "out" or "ref" or "this" or "base" or
            "default" or "string" or "int" or "bool" or "decimal" or "double" or
            "float" or "long" or "short" or "byte" or "char" or "object" => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if an identifier appears to be a type name (starts with uppercase and is followed by '.')
    /// This helps avoid prefixing enum type names like OrderStatus in "OrderStatus.Completed".
    /// </summary>
    public static bool IsLikelyTypeName(string identifier, string expression, int identifierEndIndex)
    {
        // If identifier starts with uppercase and is followed by '.', it's likely a type name
        if (identifier.Length > 0 && char.IsUpper(identifier[0]))
        {
            if (identifierEndIndex < expression.Length && expression[identifierEndIndex] == '.')
            {
                return true;
            }
        }
        return false;
    }
}
