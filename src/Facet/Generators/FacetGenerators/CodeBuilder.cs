using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facet.Generators;

/// <summary>
/// Orchestrates the generation of complete facet type source code.
/// </summary>
internal static class CodeBuilder
{
    /// <summary>
    /// Generates the complete source code for a facet type.
    /// </summary>
    public static string Generate(FacetTargetModel model, Dictionary<string, FacetTargetModel> facetLookup)
    {
        var sb = new StringBuilder();
        GenerateFileHeader(sb);

        // Collect all namespaces from referenced types
        var namespacesToImport = CodeGenerationHelpers.CollectNamespaces(model);

        // Collect types that need 'using static' directives
        var staticUsingTypes = CodeGenerationHelpers.CollectStaticUsingTypes(model);

        // Generate using statements for all required namespaces
        foreach (var ns in namespacesToImport.OrderBy(x => x))
        {
            sb.AppendLine($"using {ns};");
        }

        // Generate using static statements for types nested in other types
        foreach (var type in staticUsingTypes.OrderBy(x => x))
        {
            sb.AppendLine($"using static {type};");
        }

        sb.AppendLine();

        // Nullable must be enabled in generated code with a directive
        var hasNullableRefTypeMembers = model.Members.Any(m => !m.IsValueType && m.TypeName.EndsWith("?"));
        // Also enable nullable context when depth tracking is needed, as the internal constructor
        // uses System.Collections.Generic.HashSet<object>? __processed (nullable parameter)
        var needsDepthTracking = model.MaxDepth > 0 || model.PreserveReferences;
        if (hasNullableRefTypeMembers || needsDepthTracking)
        {
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace};");
        }

        // Generate containing type hierarchy for nested classes
        var containingTypeIndent = GenerateContainingTypeHierarchy(sb, model);

        // Generate type-level XML documentation if available
        if (!string.IsNullOrWhiteSpace(model.TypeXmlDocumentation))
        {
            var indentedDocumentation = model.TypeXmlDocumentation!.Replace("\n", $"\n{containingTypeIndent}");
            sb.AppendLine($"{containingTypeIndent}{indentedDocumentation}");
        }

        var keyword = GetTypeKeyword(model);
        var isPositional = model.IsRecord && !model.HasExistingPrimaryConstructor;
        var hasInitOnlyProperties = model.Members.Any(m => m.IsInitOnly);
        var hasRequiredProperties = model.Members.Any(m => m.IsRequired);
        var hasCustomMapping = !string.IsNullOrWhiteSpace(model.ConfigurationTypeName);

        // Only generate positional declaration if there's no existing primary constructor
        if (isPositional)
        {
            GeneratePositionalDeclaration(sb, model, keyword, containingTypeIndent);
        }

        sb.AppendLine($"{containingTypeIndent}{model.Accessibility} partial {keyword} {model.Name}");
        sb.AppendLine($"{containingTypeIndent}{{");

        var memberIndent = containingTypeIndent + "    ";

        // Generate properties if not positional OR if there's an existing primary constructor
        if (!isPositional || model.HasExistingPrimaryConstructor)
        {
            MemberGenerator.GenerateMembers(sb, model, memberIndent);
        }

        // Generate parameterless constructor first if requested
        // This ensures third-party code that picks the first constructor will use the parameterless one
        if (model.GenerateParameterlessConstructor)
        {
            ConstructorGenerator.GenerateParameterlessConstructor(sb, model, isPositional);
        }

        // Generate constructor from source
        if (model.GenerateConstructor)
        {
            ConstructorGenerator.GenerateConstructor(sb, model, isPositional, hasInitOnlyProperties, hasCustomMapping, hasRequiredProperties);
        }

        // Generate projection
        if (model.GenerateExpressionProjection)
        {
            ProjectionGenerator.GenerateProjectionProperty(sb, model, memberIndent, facetLookup);
        }

        // Generate reverse mapping method (ToSource)
        if (model.GenerateToSource)
        {
            ToSourceGenerator.Generate(sb, model);
        }

        // Generate FlattenTo methods
        if (model.FlattenToTypes.Length > 0)
        {
            FlattenToGenerator.Generate(sb, model, memberIndent, facetLookup);
        }

        sb.AppendLine($"{containingTypeIndent}}}");

        // Close containing type braces
        CloseContainingTypeHierarchy(sb, model, containingTypeIndent);

        return sb.ToString();
    }

    #region Private Helper Methods

    private static void GenerateFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine($"//     This code was generated by the Facet source generator v{FacetConstants.GeneratorVersion}.");
        sb.AppendLine("//     Changes to this file may cause incorrect behavior and will be lost if");
        sb.AppendLine("//     the code is regenerated.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();
    }

    private static string GenerateContainingTypeHierarchy(StringBuilder sb, FacetTargetModel model)
    {
        var containingTypeIndent = "";
        foreach (var containingType in model.ContainingTypes)
        {
            // Don't specify accessibility for containing types - they're already defined in user code
            sb.AppendLine($"{containingTypeIndent}partial class {containingType}");
            sb.AppendLine($"{containingTypeIndent}{{");
            containingTypeIndent += "    ";
        }
        return containingTypeIndent;
    }

    private static void CloseContainingTypeHierarchy(StringBuilder sb, FacetTargetModel model, string containingTypeIndent)
    {
        // Close containing type braces
        for (int i = model.ContainingTypes.Length - 1; i >= 0; i--)
        {
            containingTypeIndent = containingTypeIndent.Substring(0, containingTypeIndent.Length - 4);
            sb.AppendLine($"{containingTypeIndent}}}");
        }
    }

    private static string GetTypeKeyword(FacetTargetModel model)
    {
        return (model.TypeKind, model.IsRecord) switch
        {
            (TypeKind.Class, false) => "class",
            (TypeKind.Class, true) => "record",
            (TypeKind.Struct, true) => "record struct",
            (TypeKind.Struct, false) => "struct",
            _ => "class",
        };
    }

    private static void GeneratePositionalDeclaration(StringBuilder sb, FacetTargetModel model, string keyword, string indent)
    {
        var parameters = string.Join(", ",
            model.Members.Select(m =>
            {
                var param = $"{m.TypeName} {m.Name}";
                // Add required modifier for positional parameters if needed
                if (m.IsRequired && model.TypeKind == TypeKind.Struct && model.IsRecord)
                {
                    param = $"required {param}";
                }
                return param;
            }));
        // Suppress CS1591 (missing XML comment) warnings for generated positional declarations
        // This prevents warnings when GenerateDocumentationFile is enabled
        sb.AppendLine($"{indent}#pragma warning disable CS1591");
        sb.AppendLine($"{indent}{model.Accessibility} partial {keyword} {model.Name}({parameters});");
        sb.AppendLine($"{indent}#pragma warning restore CS1591");
    }

    #endregion
}
