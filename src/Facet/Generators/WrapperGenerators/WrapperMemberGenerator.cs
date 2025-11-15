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
        // For nested wrappers, we wrap the nested object
        // Pattern (mutable): public TypeName PropertyName { get => _source.PropertyName; set => _source.PropertyName = value; }
        // Pattern (nested): public NestedWrapper PropertyName { get => new NestedWrapper(_source.PropertyName); set => _source.PropertyName = value.Unwrap(); }
        // Pattern (readonly): public TypeName PropertyName { get => _source.PropertyName; }

        sb.AppendLine($"{indent}public {member.TypeName} {member.Name}");
        sb.AppendLine($"{indent}{{");

        // Generate getter
        if (member.IsNestedFacet)
        {
            // For nested wrappers, wrap the source property
            // Handle nullable types
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
            // Simple delegation
            sb.AppendLine($"{indent}    get => {model.SourceFieldName}.{member.Name};");
        }

        // Generate setter
        if (!model.ReadOnly)
        {
            if (member.IsNestedFacet)
            {
                // For nested wrappers, unwrap the value
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
                // Simple assignment
                sb.AppendLine($"{indent}    set => {model.SourceFieldName}.{member.Name} = value;");
            }
        }

        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateDelegatingField(StringBuilder sb, WrapperTargetModel model, FacetMember member, string indent)
    {
        // For fields in wrappers, we generate properties that delegate to the source field
        // For nested wrappers, we wrap the nested object
        // Pattern (mutable): public TypeName FieldName { get => _source.FieldName; set => _source.FieldName = value; }
        // Pattern (nested): public NestedWrapper FieldName { get => new NestedWrapper(_source.FieldName); set => _source.FieldName = value.Unwrap(); }
        // Pattern (readonly): public TypeName FieldName { get => _source.FieldName; }

        sb.AppendLine($"{indent}public {member.TypeName} {member.Name}");
        sb.AppendLine($"{indent}{{");

        // Generate getter
        if (member.IsNestedFacet)
        {
            // For nested wrappers, wrap the source field
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
            // Simple delegation
            sb.AppendLine($"{indent}    get => {model.SourceFieldName}.{member.Name};");
        }

        // Generate setter
        if (!model.ReadOnly && !member.IsReadOnly)
        {
            if (member.IsNestedFacet)
            {
                // For nested wrappers, unwrap the value
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
                // Simple assignment
                sb.AppendLine($"{indent}    set => {model.SourceFieldName}.{member.Name} = value;");
            }
        }

        sb.AppendLine($"{indent}}}");
    }
}
