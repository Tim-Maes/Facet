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
        var hasInitOnlyProperties = model.Members.Any(m => m.IsInitOnly);
        var hasRequiredProperties = model.Members.Any(m => m.IsRequired);

        // For record classes, avoid positional declarations when there are required members,
        // because C# doesn't support the 'required' modifier on positional parameters of record classes.
        // Record structs DO support 'required' on positional parameters, so they can stay positional.
        var isPositional = model.IsRecord && !model.HasExistingPrimaryConstructor
            && !(model.TypeKind == TypeKind.Class && hasRequiredProperties);
        var hasCustomMapping = !string.IsNullOrWhiteSpace(model.ConfigurationTypeName);

        // Determine if we need to generate equality (skip for records which already have value equality)
        var shouldGenerateEquality = model.GenerateEquality && !model.IsRecord;

        // Only generate positional declaration if there's no existing primary constructor
        if (isPositional)
        {
            GeneratePositionalDeclaration(sb, model, keyword, containingTypeIndent);
        }

        // Generate the type declaration, including IEquatable<T> if equality is requested
        if (shouldGenerateEquality)
        {
            sb.AppendLine($"{containingTypeIndent}{model.Accessibility} partial {keyword} {model.Name} : {EqualityGenerator.GetEquatableInterface(model)}");
        }
        else
        {
            sb.AppendLine($"{containingTypeIndent}{model.Accessibility} partial {keyword} {model.Name}");
        }
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

        // Generate copy constructor
        if (model.GenerateCopyConstructor)
        {
            CopyConstructorGenerator.Generate(sb, model, memberIndent);
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

        // Generate equality members (Equals, GetHashCode, ==, !=)
        // Skip for records which already have value-based equality
        if (shouldGenerateEquality)
        {
            EqualityGenerator.Generate(sb, model, memberIndent);
        }

        sb.AppendLine($"{containingTypeIndent}}}");

        // Close containing type braces
        CloseContainingTypeHierarchy(sb, model, containingTypeIndent);

        return sb.ToString();
    }

    /// <summary>
    /// Dispatches to <see cref="Generate"/> for a single-source facet or to
    /// <see cref="GenerateCombined"/> when the same target type carries multiple
    /// <c>[Facet]</c> attributes (multi-source scenario).
    /// </summary>
    public static string GenerateForGroup(
        IReadOnlyList<FacetTargetModel> models,
        Dictionary<string, FacetTargetModel> facetLookup)
    {
        if (models.Count == 1)
            return Generate(models[0], facetLookup);

        return GenerateCombined(models, facetLookup);
    }

    /// <summary>
    /// Generates a single partial-class file that combines mappings from multiple source types
    /// to the same target type.
    /// <para>
    /// Properties are the union of all members across every source mapping (deduplicated by name,
    /// first-definition wins).  Constructor/factory/projection/ToSource artefacts are generated
    /// once per source.  Projection and ToSource use source-specific names
    /// (<c>ProjectionFrom{SourceSimpleName}</c> / <c>To{SourceSimpleName}</c>) to avoid conflicts.
    /// Shared artefacts (parameterless constructor, copy constructor, equality) are generated from
    /// the primary (first) model only.
    /// </para>
    /// </summary>
    public static string GenerateCombined(
        IReadOnlyList<FacetTargetModel> models,
        Dictionary<string, FacetTargetModel> facetLookup)
    {
        var primaryModel = models[0];
        var sb = new StringBuilder();
        GenerateFileHeader(sb);

        // Collect namespaces and static-using directives from ALL models
        var namespacesToImport = new HashSet<string>();
        var staticUsingTypes = new HashSet<string>();
        foreach (var m in models)
        {
            foreach (var ns in CodeGenerationHelpers.CollectNamespaces(m))
                namespacesToImport.Add(ns);
            foreach (var su in CodeGenerationHelpers.CollectStaticUsingTypes(m))
                staticUsingTypes.Add(su);
        }

        foreach (var ns in namespacesToImport.OrderBy(x => x))
            sb.AppendLine($"using {ns};");

        foreach (var type in staticUsingTypes.OrderBy(x => x))
            sb.AppendLine($"using static {type};");

        sb.AppendLine();

        // Enable nullable context if ANY model needs it
        var needsNullable = models.Any(m =>
            m.Members.Any(mem => !mem.IsValueType && mem.TypeName.EndsWith("?"))
            || m.MaxDepth > 0
            || m.PreserveReferences);
        if (needsNullable)
        {
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(primaryModel.Namespace))
            sb.AppendLine($"namespace {primaryModel.Namespace};");

        var containingTypeIndent = GenerateContainingTypeHierarchy(sb, primaryModel);

        if (!string.IsNullOrWhiteSpace(primaryModel.TypeXmlDocumentation))
        {
            var indentedDoc = primaryModel.TypeXmlDocumentation!.Replace("\n", $"\n{containingTypeIndent}");
            sb.AppendLine($"{containingTypeIndent}{indentedDoc}");
        }

        var keyword = GetTypeKeyword(primaryModel);

        // Build the union of all members across source models, deduplicating by name (first-wins).
        var seenMemberNames = new HashSet<string>();
        var unionMembers = new System.Collections.Generic.List<FacetMember>();
        foreach (var m in models)
        {
            foreach (var member in m.Members)
            {
                if (seenMemberNames.Add(member.Name))
                    unionMembers.Add(member);
            }
        }

        var hasInitOnlyUnion = unionMembers.Any(m => m.IsInitOnly);
        var hasRequiredUnion = unionMembers.Any(m => m.IsRequired);

        // Positional record logic uses the primary model's member set for the declaration
        var isPositional = primaryModel.IsRecord && !primaryModel.HasExistingPrimaryConstructor
            && !(primaryModel.TypeKind == TypeKind.Class && hasRequiredUnion);
        var shouldGenerateEquality = primaryModel.GenerateEquality && !primaryModel.IsRecord;

        if (isPositional)
        {
            // Use the primary model for positional declaration (shares the primary source's shape)
            GeneratePositionalDeclaration(sb, primaryModel, keyword, containingTypeIndent);
        }

        if (shouldGenerateEquality)
        {
            sb.AppendLine($"{containingTypeIndent}{primaryModel.Accessibility} partial {keyword} {primaryModel.Name} : {EqualityGenerator.GetEquatableInterface(primaryModel)}");
        }
        else
        {
            sb.AppendLine($"{containingTypeIndent}{primaryModel.Accessibility} partial {keyword} {primaryModel.Name}");
        }
        sb.AppendLine($"{containingTypeIndent}{{");

        var memberIndent = containingTypeIndent + "    ";

        // Generate union of properties once
        if (!isPositional || primaryModel.HasExistingPrimaryConstructor)
        {
            // Build a synthetic model view with union members for MemberGenerator
            MemberGenerator.GenerateMembers(sb, primaryModel, memberIndent, unionMembers);
        }

        // Shared: parameterless constructor (from primary model)
        if (primaryModel.GenerateParameterlessConstructor)
            ConstructorGenerator.GenerateParameterlessConstructor(sb, primaryModel, isPositional);

        // Per-source: constructors + FromSource factory methods
        foreach (var model in models)
        {
            if (!model.GenerateConstructor) continue;

            var hasCustomMapping = !string.IsNullOrWhiteSpace(model.ConfigurationTypeName);
            var needsDepthTracking = model.MaxDepth > 0 || model.PreserveReferences;
            var modelHasInitOnly = model.Members.Any(mem => mem.IsInitOnly);
            var modelHasRequired = model.Members.Any(mem => mem.IsRequired);

            ConstructorGenerator.GenerateConstructor(
                sb, model, isPositional, modelHasInitOnly, hasCustomMapping, modelHasRequired);
        }

        // Shared: copy constructor (from primary model)
        if (primaryModel.GenerateCopyConstructor)
            CopyConstructorGenerator.Generate(sb, primaryModel, memberIndent);

        // Per-source: projections (use source-specific names to avoid static property conflicts)
        foreach (var model in models)
        {
            if (!model.GenerateExpressionProjection) continue;

            var projectionName = GetProjectionName(model, models);
            ProjectionGenerator.GenerateProjectionProperty(sb, model, memberIndent, facetLookup, projectionName);
        }

        // Per-source: ToSource methods (use source-specific names to avoid method conflicts)
        foreach (var model in models)
        {
            if (!model.GenerateToSource) continue;

            var toSourceName = GetToSourceMethodName(model, models);
            ToSourceGenerator.Generate(sb, model, toSourceName);
        }

        // Per-source: FlattenTo
        foreach (var model in models)
        {
            if (model.FlattenToTypes.Length > 0)
                FlattenToGenerator.Generate(sb, model, memberIndent, facetLookup);
        }

        // Shared: equality members (from primary model)
        if (shouldGenerateEquality)
            EqualityGenerator.Generate(sb, primaryModel, memberIndent);

        sb.AppendLine($"{containingTypeIndent}}}");
        CloseContainingTypeHierarchy(sb, primaryModel, containingTypeIndent);

        return sb.ToString();
    }

    /// <summary>
    /// Returns the property name to use for the Projection expression of the given model.
    /// <list type="bullet">
    /// <item>Single-source: <c>"Projection"</c> (backward-compatible).</item>
    /// <item>Multi-source: <c>"ProjectionFrom{SourceSimpleName}"</c>.</item>
    /// </list>
    /// </summary>
    private static string GetProjectionName(FacetTargetModel model, IReadOnlyList<FacetTargetModel> allModels)
    {
        if (allModels.Count <= 1)
            return "Projection";

        return "ProjectionFrom" + GetSourceSimpleName(model);
    }

    /// <summary>
    /// Returns the method name to use for the ToSource conversion of the given model.
    /// <list type="bullet">
    /// <item>Single-source: <c>null</c> → <c>"ToSource"</c> + deprecated <c>BackTo</c> alias.</item>
    /// <item>Multi-source: <c>"To{SourceSimpleName}"</c> (no BackTo alias).</item>
    /// </list>
    /// </summary>
    private static string? GetToSourceMethodName(FacetTargetModel model, IReadOnlyList<FacetTargetModel> allModels)
    {
        if (allModels.Count <= 1)
            return null; // Use default "ToSource" + BackTo

        return "To" + GetSourceSimpleName(model);
    }

    /// <summary>
    /// Extracts the simple (unqualified) type name from a model's fully-qualified
    /// <see cref="FacetTargetModel.SourceTypeName"/>, stripping everything from the
    /// first <c>&lt;</c> onwards so that generic types (e.g. <c>List&lt;String&gt;</c>)
    /// produce a valid C# identifier fragment (<c>List</c>).
    /// </summary>
    private static string GetSourceSimpleName(FacetTargetModel model)
    {
        var simpleName = CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName);
        var angleBracket = simpleName.IndexOf('<');
        return angleBracket > 0 ? simpleName.Substring(0, angleBracket) : simpleName;
    }

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
}
