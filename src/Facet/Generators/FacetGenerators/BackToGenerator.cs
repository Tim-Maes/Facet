using Facet.Generators.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facet.Generators;

/// <summary>
/// Generates BackTo methods for converting facet instances back to their source types.
/// </summary>
internal static class BackToGenerator
{
    /// <summary>
    /// Generates the BackTo method that converts the facet type back to the source type.
    /// </summary>
    public static void GenerateBackToMethod(StringBuilder sb, FacetTargetModel model)
    {
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Converts this instance of <see cref=\"{model.Name}\"/> to an instance of the source type.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <returns>An instance of the source type with properties mapped from this instance.</returns>");
        sb.AppendLine($"    public {model.SourceTypeName} BackTo()");
        sb.AppendLine("    {");

        if (model.SourceHasPositionalConstructor)
        {
            GeneratePositionalBackTo(sb, model);
        }
        else
        {
            GenerateObjectInitializerBackTo(sb, model);
        }

        sb.AppendLine("    }");
    }

    private static void GeneratePositionalBackTo(StringBuilder sb, FacetTargetModel model)
    {
        // For source types with positional constructors (like records), use positional syntax
        var constructorArgs = string.Join(", ",
            model.Members.Select(m => ExpressionBuilder.GetBackToValueExpression(m)));
        sb.AppendLine($"        return new {model.SourceTypeName}({constructorArgs});");
    }

    private static void GenerateObjectInitializerBackTo(StringBuilder sb, FacetTargetModel model)
    {
        // For source types without positional constructors, use object initializer syntax
        sb.AppendLine($"        return new {model.SourceTypeName}");
        sb.AppendLine("        {");

        var propertyAssignments = new List<string>();

        // Add assignments for included properties
        foreach (var member in model.Members)
        {
            var backToValue = ExpressionBuilder.GetBackToValueExpression(member);
            propertyAssignments.Add($"            {member.Name} = {backToValue}");
        }

        // Add default values for excluded required members
        foreach (var excludedMember in model.ExcludedRequiredMembers)
        {
            var defaultValue = GeneratorUtilities.GetDefaultValueForType(excludedMember.TypeName);
            propertyAssignments.Add($"            {excludedMember.Name} = {defaultValue}");
        }

        sb.AppendLine(string.Join(",\n", propertyAssignments));
        sb.AppendLine("        };");
    }
}
