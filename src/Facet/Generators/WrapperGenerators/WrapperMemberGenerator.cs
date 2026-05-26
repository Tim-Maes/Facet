using System.Linq;
using System.Text;

namespace Facet.Generators;

/// <summary>
/// Generates delegating properties for wrapper types.
/// </summary>
internal static class WrapperMemberGenerator
{
    /// <summary>
    /// Generates all delegating properties/fields for the wrapper type.
    /// </summary>
    public static void GenerateMembers(StringBuilder sb, WrapperTargetModel model, string indent)
    {
        bool isFirst = true;

        foreach (var member in model.Members)
        {
            if (!isFirst)
            {
                sb.AppendLine();
            }
            isFirst = false;

            GenerateMember(sb, model, member, indent);
        }

        if (model.Members.Length > 0)
        {
            sb.AppendLine();
        }
    }

    private static void GenerateMember(StringBuilder sb, WrapperTargetModel model, FacetMember member, string indent)
    {
        if (!string.IsNullOrWhiteSpace(member.XmlDocumentation))
        {
            var indentedDocumentation = member.XmlDocumentation!.Replace("\n", $"\n{indent}");
            sb.AppendLine($"{indent}{indentedDocumentation}");
        }

        if (model.CopyAttributes && member.Attributes.Count > 0)
        {
            foreach (var attribute in member.Attributes)
            {
                sb.AppendLine($"{indent}{attribute}");
            }
        }

        if (member.Kind == FacetMemberKind.Property)
        {
            GenerateDelegatingProperty(sb, model, member, indent);
        }
        else if (member.Kind == FacetMemberKind.Field)
        {
            GenerateDelegatingField(sb, model, member, indent);
        }
    }

    private static void GenerateDelegatingProperty(StringBuilder sb, WrapperTargetModel model, FacetMember member, string indent)
    {
        sb.AppendLine($"{indent}public {member.TypeName} {member.Name}");
        sb.AppendLine($"{indent}{{");

        if (member.IsNestedFacet)
        {
            bool isNullable = member.TypeName.EndsWith("?");
            string wrapperType = isNullable ? member.TypeName.TrimEnd('?') : member.TypeName;

            if (isNullable)
            {
                sb.AppendLine($"{indent}    get => {model.SourceFieldName}.{member.Name} != null ? new {wrapperType}({model.SourceFieldName}.{member.Name}) : null;");
            }
            else
            {
                sb.AppendLine($"{indent}    get => new {wrapperType}({model.SourceFieldName}.{member.Name});");
            }
        }
        else
        {
            sb.AppendLine($"{indent}    get => {model.SourceFieldName}.{member.Name};");
        }

        if (!model.ReadOnly)
        {
            if (member.IsNestedFacet)
            {
                bool isNullable = member.TypeName.EndsWith("?");
                if (isNullable)
                {
                    sb.AppendLine($"{indent}    set => {model.SourceFieldName}.{member.Name} = value?.Unwrap();");
                }
                else
                {
                    sb.AppendLine($"{indent}    set => {model.SourceFieldName}.{member.Name} = value.Unwrap();");
                }
            }
            else
            {
                sb.AppendLine($"{indent}    set => {model.SourceFieldName}.{member.Name} = value;");
            }
        }

        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateDelegatingField(StringBuilder sb, WrapperTargetModel model, FacetMember member, string indent)
    {
        sb.AppendLine($"{indent}public {member.TypeName} {member.Name}");
        sb.AppendLine($"{indent}{{");

        if (member.IsNestedFacet)
        {
            bool isNullable = member.TypeName.EndsWith("?");
            string wrapperType = isNullable ? member.TypeName.TrimEnd('?') : member.TypeName;

            if (isNullable)
            {
                sb.AppendLine($"{indent}    get => {model.SourceFieldName}.{member.Name} != null ? new {wrapperType}({model.SourceFieldName}.{member.Name}) : null;");
            }
            else
            {
                sb.AppendLine($"{indent}    get => new {wrapperType}({model.SourceFieldName}.{member.Name});");
            }
        }
        else
        {
            sb.AppendLine($"{indent}    get => {model.SourceFieldName}.{member.Name};");
        }

        if (!model.ReadOnly && !member.IsReadOnly)
        {
            if (member.IsNestedFacet)
            {
                bool isNullable = member.TypeName.EndsWith("?");
                if (isNullable)
                {
                    sb.AppendLine($"{indent}    set => {model.SourceFieldName}.{member.Name} = value?.Unwrap();");
                }
                else
                {
                    sb.AppendLine($"{indent}    set => {model.SourceFieldName}.{member.Name} = value.Unwrap();");
                }
            }
            else
            {
                sb.AppendLine($"{indent}    set => {model.SourceFieldName}.{member.Name} = value;");
            }
        }

        sb.AppendLine($"{indent}}}");
    }
}
