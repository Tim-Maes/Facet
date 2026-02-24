using System.Collections.Generic;
using System.Text;

namespace Facet.Generators;

/// <summary>
/// Generates value-based equality members (Equals, GetHashCode, ==, !=) for facet types.
/// </summary>
internal static class EqualityGenerator
{
    /// <summary>
    /// Generates IEquatable&lt;T&gt; implementation, Equals, GetHashCode, and equality operators.
    /// </summary>
    public static void Generate(StringBuilder sb, FacetTargetModel model, string indent)
    {
        GenerateEqualsT(sb, model, indent);
        GenerateEqualsObject(sb, model, indent);
        GenerateGetHashCode(sb, model, indent);
        GenerateEqualityOperators(sb, model, indent);
    }

    /// <summary>
    /// Gets the interface declaration text that should be appended to the type declaration.
    /// Returns "System.IEquatable&lt;TypeName&gt;" for use in the partial type declaration.
    /// </summary>
    public static string GetEquatableInterface(FacetTargetModel model)
    {
        return $"System.IEquatable<{model.Name}>";
    }

    private static void GenerateEqualsT(StringBuilder sb, FacetTargetModel model, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Determines whether the specified <see cref=\"{model.Name}\"/> is equal to the current instance.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <param name=\"other\">The object to compare with the current instance.</param>");
        sb.AppendLine($"{indent}/// <returns><c>true</c> if the specified object has equal member values; otherwise, <c>false</c>.</returns>");

        bool isClass = model.TypeKind == Microsoft.CodeAnalysis.TypeKind.Class;

        if (isClass)
        {
            sb.AppendLine($"{indent}public bool Equals({model.Name}? other)");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    if (other is null) return false;");
            sb.AppendLine($"{indent}    if (ReferenceEquals(this, other)) return true;");
        }
        else
        {
            // struct
            sb.AppendLine($"{indent}public bool Equals({model.Name} other)");
            sb.AppendLine($"{indent}{{");
        }

        if (model.Members.Length == 0)
        {
            sb.AppendLine($"{indent}    return true;");
        }
        else
        {
            sb.Append($"{indent}    return ");
            var first = true;
            foreach (var m in model.Members)
            {
                if (!first)
                {
                    sb.AppendLine();
                    sb.Append($"{indent}        && ");
                }
                first = false;

                sb.Append(GetEqualityExpression(m));
            }
            sb.AppendLine(";");
        }

        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateEqualsObject(StringBuilder sb, FacetTargetModel model, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}/// <inheritdoc/>");
        sb.AppendLine($"{indent}public override bool Equals(object? obj) => obj is {model.Name} other && Equals(other);");
    }

    private static void GenerateGetHashCode(StringBuilder sb, FacetTargetModel model, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}/// <inheritdoc/>");
        sb.AppendLine($"{indent}public override int GetHashCode()");
        sb.AppendLine($"{indent}{{");

        if (model.Members.Length == 0)
        {
            sb.AppendLine($"{indent}    return 0;");
        }
        else
        {
            sb.AppendLine($"{indent}    unchecked");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        int hash = 17;");
            foreach (var m in model.Members)
            {
                sb.AppendLine($"{indent}        hash = hash * 31 + {GetHashCodeExpression(m)};");
            }
            sb.AppendLine($"{indent}        return hash;");
            sb.AppendLine($"{indent}    }}");
        }

        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateEqualityOperators(StringBuilder sb, FacetTargetModel model, string indent)
    {
        bool isClass = model.TypeKind == Microsoft.CodeAnalysis.TypeKind.Class;

        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>Determines whether two <see cref=\"{model.Name}\"/> instances are equal.</summary>");

        if (isClass)
        {
            sb.AppendLine($"{indent}public static bool operator ==({model.Name}? left, {model.Name}? right)");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    if (left is null) return right is null;");
            sb.AppendLine($"{indent}    return left.Equals(right);");
            sb.AppendLine($"{indent}}}");
        }
        else
        {
            sb.AppendLine($"{indent}public static bool operator ==({model.Name} left, {model.Name} right) => left.Equals(right);");
        }

        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>Determines whether two <see cref=\"{model.Name}\"/> instances are not equal.</summary>");

        if (isClass)
        {
            sb.AppendLine($"{indent}public static bool operator !=({model.Name}? left, {model.Name}? right) => !(left == right);");
        }
        else
        {
            sb.AppendLine($"{indent}public static bool operator !=({model.Name} left, {model.Name} right) => !(left == right);");
        }
    }

    private static string GetEqualityExpression(FacetMember member)
    {
        // For value types, use direct comparison
        if (member.IsValueType)
        {
            return $"this.{member.Name} == other.{member.Name}";
        }

        // For reference types, use EqualityComparer<T>.Default to handle nulls
        return $"System.Collections.Generic.EqualityComparer<{member.TypeName}>.Default.Equals(this.{member.Name}, other.{member.Name})";
    }

    private static string GetHashCodeExpression(FacetMember member)
    {
        if (member.IsValueType)
        {
            return $"{member.Name}.GetHashCode()";
        }

        // For reference types, guard against null
        return $"({member.Name}?.GetHashCode() ?? 0)";
    }
}
