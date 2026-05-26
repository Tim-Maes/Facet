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
    /// <param name="membersOverride">
    /// When non-<see langword="null"/>, generates declarations for this set of members instead of
    /// <see cref="FacetTargetModel.Members"/>.  Used by the multi-source combined generator to emit
    /// the union of all source members.
    /// </param>
    /// <param name="usePropertyNameAsInitializer">
    /// When <see langword="true"/>, each generated property is initialized with <c>= PropertyName;</c>
    /// instead of a type default. Required for positional record body overrides, where the initializer
    /// reads from the matching positional parameter and satisfies CS8907 ("Parameter 'X' is unread").
    /// </param>
    public static void GenerateMembers(
        StringBuilder sb,
        FacetTargetModel model,
        string memberIndent,
        System.Collections.Generic.IReadOnlyList<FacetMember>? membersOverride = null,
        bool usePropertyNameAsInitializer = false)
    {
        var baseClassMembers = new System.Collections.Generic.HashSet<string>(model.BaseClassMemberNames);

        var members = membersOverride ?? (System.Collections.Generic.IReadOnlyList<FacetMember>)model.Members;

        foreach (var m in members)
        {
            if (m.IsUserDeclared)
                continue;

            if (baseClassMembers.Contains(m.Name))
                continue;

            if (!string.IsNullOrWhiteSpace(m.XmlDocumentation))
            {
                var indentedDocumentation = m.XmlDocumentation!.Replace("\n", $"\n{memberIndent}");
                sb.AppendLine($"{memberIndent}{indentedDocumentation}");
            }

            foreach (var attribute in m.Attributes)
            {
                sb.AppendLine($"{memberIndent}{attribute}");
            }

            if (m.Kind == FacetMemberKind.Property)
            {
                GenerateProperty(sb, m, memberIndent, usePropertyNameAsInitializer);
            }
            else
            {
                GenerateField(sb, m, memberIndent);
            }
        }
    }

    private static void GenerateProperty(StringBuilder sb, FacetMember member, string indent, bool usePropertyNameAsInitializer = false)
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

        if (usePropertyNameAsInitializer)
        {
            propDef += $" = {member.Name};";
        }
        else if (!string.IsNullOrEmpty(member.DefaultValue))
        {
            propDef += $" = {member.DefaultValue};";
        }
        else if (!member.IsValueType && !member.IsRequired && !NullabilityAnalyzer.IsNullableTypeName(member.TypeName))
        {
            // Suppress CS8618 for generated non-nullable refs.
            propDef += " = default!;";
        }

        if (member.IsRequired)
        {
            propDef = $"required {propDef}";
        }

        sb.AppendLine($"{indent}{propDef}");
    }

    private static void GenerateField(StringBuilder sb, FacetMember member, string indent)
    {
        var fieldDef = $"public {member.TypeName} {member.Name}";
        
        if (!string.IsNullOrEmpty(member.DefaultValue))
        {
            fieldDef += $" = {member.DefaultValue}";
        }
        else if (!member.IsValueType && !member.IsRequired && !NullabilityAnalyzer.IsNullableTypeName(member.TypeName))
        {
            // Suppress CS8618 for generated non-nullable refs.
            fieldDef += " = default!";
        }
        
        fieldDef += ";";
        
        if (member.IsRequired)
        {
            fieldDef = $"required {fieldDef}";
        }
        sb.AppendLine($"{indent}{fieldDef}");
    }
}
