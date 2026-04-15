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
    /// <param name="toSourceMethodName">
    /// The name to use for the generated method.
    /// When <see langword="null"/>, the default names <c>ToSource</c> and <c>BackTo</c> are used.
    /// When provided (for multi-source facets), only the specified method is generated without the
    /// deprecated <c>BackTo</c> alias.
    /// </param>
    public static void Generate(StringBuilder sb, FacetTargetModel model, string? toSourceMethodName = null)
    {
        var methodName = toSourceMethodName ?? "ToSource";
        var isCustomName = toSourceMethodName != null;

        // Generate the main ToSource method
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Converts this instance of <see cref=\"{model.Name}\"/> to an instance of <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <returns>An instance of <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/> with properties mapped from this instance.</returns>");
        sb.AppendLine($"    public {(model.BaseHidesFacetMembers ? "new " : "")}{model.SourceTypeName} {methodName}()");
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

        // Generate the deprecated BackTo method only for the default (single-source) naming
        if (!isCustomName)
        {
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Converts this instance of <see cref=\"{model.Name}\"/> to an instance of the source type.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    /// <returns>An instance of the source type with properties mapped from this instance.</returns>");
            sb.AppendLine("    [global::System.Obsolete(\"Use ToSource() instead. This method will be removed in a future version.\")]");
            sb.AppendLine($"    public {(model.BaseHidesFacetMembers ? "new " : "")}{model.SourceTypeName} BackTo() => ToSource();");
        }
    }

    private static void GeneratePositionalToSource(StringBuilder sb, FacetTargetModel model)
    {
        var constructorArgs = string.Join(", ",
            model.Members
                .Where(m => m.MapFromReversible)
                .Select(m => ExpressionBuilder.GetToSourceValueExpression(m)));

        if (model.ToSourceConfigurationTypeName != null)
        {
            sb.AppendLine($"        var result = new {model.SourceTypeName}({constructorArgs});");
            sb.AppendLine($"        {model.ToSourceConfigurationTypeName}.Map(this, result);");
            sb.AppendLine("        return result;");
        }
        else
        {
            sb.AppendLine($"        return new {model.SourceTypeName}({constructorArgs});");
        }
    }

    private static void GenerateObjectInitializerToSource(StringBuilder sb, FacetTargetModel model)
    {
        var propertyAssignments = new List<string>();

        foreach (var member in model.Members)
        {
            if (!member.MapFromReversible)
                continue;

            var toSourceValue = ExpressionBuilder.GetToSourceValueExpression(member);
            propertyAssignments.Add($"            {member.SourcePropertyName} = {toSourceValue}");
        }

        foreach (var excludedMember in model.ExcludedRequiredMembers)
        {
            var defaultValue = GeneratorUtilities.GetDefaultValueForType(excludedMember.TypeName);
            propertyAssignments.Add($"            {excludedMember.Name} = {defaultValue}");
        }

        var initializer = string.Join(",\n", propertyAssignments);

        if (model.ToSourceConfigurationTypeName != null)
        {
            sb.AppendLine($"        var result = new {model.SourceTypeName}");
            sb.AppendLine("        {");
            sb.AppendLine(initializer);
            sb.AppendLine("        };");
            sb.AppendLine($"        {model.ToSourceConfigurationTypeName}.Map(this, result);");
            sb.AppendLine("        return result;");
        }
        else
        {
            sb.AppendLine($"        return new {model.SourceTypeName}");
            sb.AppendLine("        {");
            sb.AppendLine(initializer);
            sb.AppendLine("        };");
        }
    }
}
