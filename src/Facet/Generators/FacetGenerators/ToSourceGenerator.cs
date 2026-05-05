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
    /// <param name="facetLookup">
    /// Dictionary mapping facet type names to their model lists (for resolving multi-source nested facets).
    /// </param>
    /// <param name="toSourceMethodName">
    /// The name to use for the generated method.
    /// When <see langword="null"/>, the default names <c>ToSource</c> and <c>BackTo</c> are used.
    /// When provided (for multi-source facets), only the specified method is generated without the
    /// deprecated <c>BackTo</c> alias.
    /// </param>
    public static void Generate(StringBuilder sb, FacetTargetModel model, Dictionary<string, List<FacetTargetModel>>? facetLookup, string? toSourceMethodName = null)
    {
        var methodName = toSourceMethodName ?? "ToSource";
        var isCustomName = toSourceMethodName != null;
        var newMod = model.BaseHidesFacetMembers && !isCustomName ? "new " : "";

        bool hasDepthLimit = model.MaxDepthToSource > 0;

        // Generate the main public ToSource method
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Converts this instance of <see cref=\"{model.Name}\"/> to an instance of <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <returns>An instance of <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/> with properties mapped from this instance.</returns>");

        if (hasDepthLimit)
        {
            // Public entry-point delegates to the depth-aware overload, starting at depth 0
            sb.AppendLine($"    public {newMod}{model.SourceTypeName} {methodName}() => {methodName}(0);");
        }
        else
        {
            sb.AppendLine($"    public {newMod}{model.SourceTypeName} {methodName}()");
            sb.AppendLine("    {");

            if (model.SourceHasPositionalConstructor)
                GeneratePositionalToSource(sb, model, facetLookup);
            else
                GenerateObjectInitializerToSource(sb, model, facetLookup);

            sb.AppendLine("    }");
        }

        // Generate the deprecated BackTo method only for the default (single-source) naming
        if (!isCustomName)
        {
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Converts this instance of <see cref=\"{model.Name}\"/> to an instance of the source type.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    /// <returns>An instance of the source type with properties mapped from this instance.</returns>");
            sb.AppendLine("    [global::System.Obsolete(\"Use ToSource() instead. This method will be removed in a future version.\")]");
            sb.AppendLine($"    public {newMod}{model.SourceTypeName} BackTo() => {methodName}();");
        }

        // Generate the internal depth-aware overload when MaxDepthToSource > 0
        if (hasDepthLimit)
        {
            sb.AppendLine();
            sb.AppendLine($"    /// <summary>Depth-aware overload used for <c>MaxDepthToSource</c> enforcement. Do not call directly.</summary>");
            sb.AppendLine($"    internal {newMod}{model.SourceTypeName} {methodName}(int __depth)");
            sb.AppendLine("    {");

            if (model.SourceHasPositionalConstructor)
                GeneratePositionalToSource(sb, model, facetLookup, useDepthParameter: true);
            else
                GenerateObjectInitializerToSource(sb, model, facetLookup, useDepthParameter: true);

            sb.AppendLine("    }");
        }
    }

    /// <summary>
    /// Generates an <c>ApplyToSource</c> method that writes the facet's reversible properties
    /// back onto an existing source instance (mutation, not construction).
    /// Not generated for positional-constructor sources because their properties cannot be
    /// individually assigned after the object is created.
    /// </summary>
    /// <param name="methodName">
    /// Override for the method name.  Pass <see langword="null"/> to use the default <c>ApplyToSource</c>.
    /// Multi-source scenarios pass a source-specific name such as <c>ApplyToOrder</c>.
    /// </param>
    public static void GenerateApplyToSource(StringBuilder sb, FacetTargetModel model, Dictionary<string, List<FacetTargetModel>>? facetLookup, string? methodName = null)
    {
        // Positional (record) sources cannot have individual properties set after construction.
        if (model.SourceHasPositionalConstructor)
            return;

        var applyMethodName = methodName ?? "ApplyToSource";
        var newMod = model.BaseHidesFacetMembers && methodName == null ? "new " : "";

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Applies the mapped properties of this <see cref=\"{model.Name}\"/> instance onto an existing");
        sb.AppendLine($"    /// <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/> instance in place.");
        sb.AppendLine("    /// Only properties that are part of this facet and marked as reversible are written.");
        sb.AppendLine("    /// Properties excluded from this facet are left unchanged on the source.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"source\">The existing <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/> instance to update.</param>");
        sb.AppendLine($"    public {newMod}void {applyMethodName}({model.SourceTypeName} source)");
        sb.AppendLine("    {");

        var hasMappable = false;
        foreach (var member in model.Members)
        {
            if (!member.MapFromReversible)
                continue;

            // Cannot assign to init-only source properties outside of a constructor or object initializer
            if (member.IsSourceInitOnly)
                continue;

            var value = ExpressionBuilder.GetToSourceValueExpression(member, facetLookup, model.SourceTypeName, 0, false);
            sb.AppendLine($"        source.{member.SourcePropertyName} = {value};");
            hasMappable = true;
        }

        if (!hasMappable)
            sb.AppendLine("        // No settable reversible members configured for this facet");

        if (model.ToSourceConfigurationTypeName != null)
            sb.AppendLine($"        {model.ToSourceConfigurationTypeName}.Map(this, source);");

        sb.AppendLine("    }");
    }

    private static void GeneratePositionalToSource(StringBuilder sb, FacetTargetModel model, Dictionary<string, List<FacetTargetModel>>? facetLookup, bool useDepthParameter = false)
    {
        var constructorArgs = string.Join(", ",
            model.Members
                .Where(m => m.MapFromReversible)
                .Select(m => ExpressionBuilder.GetToSourceValueExpression(m, facetLookup, model.SourceTypeName, model.MaxDepthToSource, useDepthParameter)));

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

    private static void GenerateObjectInitializerToSource(StringBuilder sb, FacetTargetModel model, Dictionary<string, List<FacetTargetModel>>? facetLookup, bool useDepthParameter = false)
    {
        var propertyAssignments = new List<string>();

        foreach (var member in model.Members)
        {
            if (!member.MapFromReversible)
                continue;

            var toSourceValue = ExpressionBuilder.GetToSourceValueExpression(member, facetLookup, model.SourceTypeName, model.MaxDepthToSource, useDepthParameter);
            propertyAssignments.Add($"            {member.SourcePropertyName} = {toSourceValue}");
        }

        foreach (var excludedMember in model.ExcludedRequiredMembers)
        {
            var defaultValue = GeneratorUtilities.GetDefaultValueForType(excludedMember.TypeName, excludedMember.IsValueType);
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
