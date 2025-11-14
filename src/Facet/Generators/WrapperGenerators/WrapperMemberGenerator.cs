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
        // Generate XML documentation if available
        if (!string.IsNullOrWhiteSpace(member.XmlDocumentation))
        {
            var indentedDocumentation = member.XmlDocumentation!.Replace("\n", $"\n{indent}");
            sb.AppendLine($"{indent}{indentedDocumentation}");
        }

        // Generate attributes if CopyAttributes is enabled
        if (model.CopyAttributes && member.Attributes.Count > 0)
        {
            foreach (var attribute in member.Attributes)
            {
                sb.AppendLine($"{indent}{attribute}");
            }
        }

        // Generate the delegating property
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
        // For wrappers, properties delegate to the source object
        // Pattern: public TypeName PropertyName { get => _source.PropertyName; set => _source.PropertyName = value; }

        sb.AppendLine($"{indent}public {member.TypeName} {member.Name}");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    get => {model.SourceFieldName}.{member.Name};");
        sb.AppendLine($"{indent}    set => {model.SourceFieldName}.{member.Name} = value;");
        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateDelegatingField(StringBuilder sb, WrapperTargetModel model, FacetMember member, string indent)
    {
        // For fields in wrappers, we generate properties that delegate to the source field
        // Pattern: public TypeName FieldName { get => _source.FieldName; set => _source.FieldName = value; }

        sb.AppendLine($"{indent}public {member.TypeName} {member.Name}");
        sb.AppendLine($"{indent}{{");

        if (member.IsReadOnly)
        {
            // Readonly fields only get a getter
            sb.AppendLine($"{indent}    get => {model.SourceFieldName}.{member.Name};");
        }
        else
        {
            // Mutable fields get getter and setter
            sb.AppendLine($"{indent}    get => {model.SourceFieldName}.{member.Name};");
            sb.AppendLine($"{indent}    set => {model.SourceFieldName}.{member.Name} = value;");
        }

        sb.AppendLine($"{indent}}}");
    }
}
