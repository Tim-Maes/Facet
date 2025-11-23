using Facet.Generators.Shared;
using System.Text;

namespace Facet.Generators;

/// <summary>
/// Generates member declarations (properties and fields) for facet types.
/// </summary>
internal static class MemberGenerator
{
    /// <summary>
    /// Generates member declarations (properties and fields) for the target type.
    /// </summary>
    public static void GenerateMembers(StringBuilder sb, FacetTargetModel model, string memberIndent)
    {
        // Create a HashSet for efficient lookup of base class member names
        var baseClassMembers = new System.Collections.Generic.HashSet<string>(model.BaseClassMemberNames);

        foreach (var m in model.Members)
        {
            // Skip user-declared properties (those with [MapFrom] or [MapWhen] attribute)
            if (m.IsUserDeclared)
                continue;

            // Skip properties that already exist in base classes to avoid "hides inherited member" warning
            if (baseClassMembers.Contains(m.Name))
                continue;

            // Generate member XML documentation if available
            if (!string.IsNullOrWhiteSpace(m.XmlDocumentation))
            {
                var indentedDocumentation = m.XmlDocumentation!.Replace("\n", $"\n{memberIndent}");
                sb.AppendLine($"{memberIndent}{indentedDocumentation}");
            }

            // Generate attributes if any
            foreach (var attribute in m.Attributes)
            {
                sb.AppendLine($"{memberIndent}{attribute}");
            }

            if (m.Kind == FacetMemberKind.Property)
            {
                GenerateProperty(sb, m, memberIndent);
            }
            else
            {
                GenerateField(sb, m, memberIndent);
            }
        }
    }

    private static void GenerateProperty(StringBuilder sb, FacetMember member, string indent)
    {
        var propDef = $"public {member.TypeName} {member.Name}";

        if (member.IsInitOnly)
        {
            propDef += " { get; init; }";
        }
        else
        {
            propDef += " { get; set; }";
        }

        if (member.IsRequired)
        {
            propDef = $"required {propDef}";
        }

        sb.AppendLine($"{indent}{propDef}");
    }

    private static void GenerateField(StringBuilder sb, FacetMember member, string indent)
    {
        var fieldDef = $"public {member.TypeName} {member.Name};";
        if (member.IsRequired)
        {
            fieldDef = $"required {fieldDef}";
        }
        sb.AppendLine($"{indent}{fieldDef}");
    }
}
