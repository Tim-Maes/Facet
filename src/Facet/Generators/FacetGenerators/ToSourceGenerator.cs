using Facet.Generators.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facet.Generators;

/// <summary>
/// Generates ToSource methods for converting facet instances back to their source types.
/// </summary>
internal static class ToSourceGenerator
{
    /// <summary>
    /// Generates the ToSource and BackTo methods that convert the facet type back to the source type.
    /// </summary>
    public static void Generate(StringBuilder sb, FacetTargetModel model)
    {
        // Generate the main ToSource method
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Converts this instance of <see cref=\"{model.Name}\"/> to an instance of the source type.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <returns>An instance of the source type with properties mapped from this instance.</returns>");
        sb.AppendLine($"    public {model.SourceTypeName} ToSource()");
        sb.AppendLine("    {");

        if (model.SourceHasPositionalConstructor)
        {
            GeneratePositionalToSource(sb, model);
        }
        else
        {
            GenerateObjectInitializerToSource(sb, model);
        }

        sb.AppendLine("    }");

        // Generate the deprecated BackTo method that calls ToSource
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Converts this instance of <see cref=\"{model.Name}\"/> to an instance of the source type.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <returns>An instance of the source type with properties mapped from this instance.</returns>");
        sb.AppendLine("    [global::System.Obsolete(\"Use ToSource() instead. This method will be removed in a future version.\")]");
        sb.AppendLine($"    public {model.SourceTypeName} BackTo() => ToSource();");
    }

    private static void GeneratePositionalToSource(StringBuilder sb, FacetTargetModel model)
    {
        // For source types with positional constructors (like records), use positional syntax
        // Only include members that are reversible
        var constructorArgs = string.Join(", ",
            model.Members
                .Where(m => m.MapFromReversible)
                .Select(m => ExpressionBuilder.GetToSourceValueExpression(m)));
        sb.AppendLine($"        return new {model.SourceTypeName}({constructorArgs});");
    }

    private static void GenerateObjectInitializerToSource(StringBuilder sb, FacetTargetModel model)
    {
        // For source types without positional constructors, use object initializer syntax
        sb.AppendLine($"        return new {model.SourceTypeName}");
        sb.AppendLine("        {");

        var propertyAssignments = new List<string>();

        // Add assignments for included properties (only if reversible)
        foreach (var member in model.Members)
        {
            // Skip non-reversible members
            if (!member.MapFromReversible)
                continue;

            var toSourceValue = ExpressionBuilder.GetToSourceValueExpression(member);
            // Use SourcePropertyName for the target property name (supports MapFrom)
            propertyAssignments.Add($"            {member.SourcePropertyName} = {toSourceValue}");
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
