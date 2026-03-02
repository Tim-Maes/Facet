using System.Linq;
using System.Text;

namespace Facet.Generators;

/// <summary>
/// Generates copy constructors for facet types that copy all member values from another instance.
/// </summary>
internal static class CopyConstructorGenerator
{
    /// <summary>
    /// Generates a copy constructor that accepts another instance of the same facet type
    /// and copies all generated member values.
    /// </summary>
    public static void Generate(StringBuilder sb, FacetTargetModel model, string indent)
    {
        var hasRequiredProperties = model.Members.Any(m => m.IsRequired);

        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Initializes a new instance of the <see cref=\"{model.Name}\"/> class by copying all");
        sb.AppendLine($"{indent}/// member values from another <see cref=\"{model.Name}\"/> instance.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <param name=\"other\">The instance to copy values from.</param>");
        sb.AppendLine($"{indent}/// <exception cref=\"System.ArgumentNullException\">Thrown when <paramref name=\"other\"/> is null.</exception>");

        if (hasRequiredProperties)
        {
            sb.AppendLine($"{indent}[System.Diagnostics.CodeAnalysis.SetsRequiredMembers]");
        }

        sb.AppendLine($"{indent}public {model.Name}({model.Name} other)");
        sb.AppendLine($"{indent}{{");

        // Add null check for reference types (classes)
        if (model.TypeKind == Microsoft.CodeAnalysis.TypeKind.Class)
        {
            sb.AppendLine($"{indent}    if (other is null) throw new System.ArgumentNullException(nameof(other));");
        }

        // Copy all members
        foreach (var m in model.Members)
        {
            // Skip init-only properties — they can't be assigned in a constructor body
            // unless this is a positional record (handled differently)
            if (m.IsInitOnly)
                continue;

            sb.AppendLine($"{indent}    this.{m.Name} = other.{m.Name};");
        }

        sb.AppendLine($"{indent}}}");
    }
}
