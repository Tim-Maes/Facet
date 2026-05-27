using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    public static string Generate(FacetTargetModel model, Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        var sb = new StringBuilder();
        GenerateFileHeader(sb);

        var namespacesToImport = CodeGenerationHelpers.CollectNamespaces(model);
        var staticUsingTypes = CodeGenerationHelpers.CollectStaticUsingTypes(model);

        foreach (var ns in namespacesToImport.OrderBy(x => x))
        {
            sb.AppendLine($"using {ns};");
        }

        foreach (var type in staticUsingTypes.OrderBy(x => x))
        {
            sb.AppendLine($"using static {type};");
        }

        sb.AppendLine();

        // Generated code needs #nullable enabled.
        var hasNullableRefTypeMembers = model.Members.Any(m => !m.IsValueType && m.TypeName.EndsWith("?"));
        
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

        var containingTypeIndent = GenerateContainingTypeHierarchy(sb, model);

        if (!string.IsNullOrWhiteSpace(model.TypeXmlDocumentation))
        {
            var indentedDocumentation = model.TypeXmlDocumentation!.Replace("\n", $"\n{containingTypeIndent}");
            sb.AppendLine($"{containingTypeIndent}{indentedDocumentation}");
        }

        var keyword = GetTypeKeyword(model);
        var hasInitOnlyProperties = model.Members.Any(m => m.IsInitOnly);
        var hasRequiredProperties = model.Members.Any(m => m.IsRequired);

        var isPositional = model.IsRecord && !model.HasExistingPrimaryConstructor
            && !(model.TypeKind == TypeKind.Class && hasRequiredProperties);
        var hasCustomMapping = !string.IsNullOrWhiteSpace(model.ConfigurationTypeName);
        var shouldGenerateEquality = model.GenerateEquality && !model.IsRecord;

        if (isPositional)
        {
            GeneratePositionalDeclaration(sb, model, keyword, containingTypeIndent);
        }

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

        if (!isPositional || model.HasExistingPrimaryConstructor)
        {
            MemberGenerator.GenerateMembers(sb, model, memberIndent);
        }
        else
        {
            // Positional parameters can't carry doc comments; emit property body overrides for documented members.
            var membersWithDocs = model.Members
                .Where(static m => !string.IsNullOrWhiteSpace(m.XmlDocumentation) && !m.IsUserDeclared)
                .ToList();
            MemberGenerator.GenerateMembers(sb, model, memberIndent, membersWithDocs, usePropertyNameAsInitializer: true);
        }

        // Parameterless constructor first so third-party code that picks the first constructor gets it.
        if (model.GenerateParameterlessConstructor)
        {
            ConstructorGenerator.GenerateParameterlessConstructor(sb, model, isPositional);
        }

        if (model.GenerateConstructor)
        {
            ConstructorGenerator.GenerateConstructor(sb, model, isPositional, hasInitOnlyProperties, hasCustomMapping, hasRequiredProperties);
        }

        if (hasCustomMapping && !model.HasMapConfiguration && model.HasProjectionMapConfiguration)
        {
            GenerateProjectionMapAction(sb, model, memberIndent);
        }

        if (model.GenerateCopyConstructor)
        {
            CopyConstructorGenerator.Generate(sb, model, memberIndent);
        }

        if (model.GenerateExpressionProjection)
        {
            ProjectionGenerator.GenerateProjectionProperty(sb, model, memberIndent, facetLookup);

            if (!(model.HasExistingPrimaryConstructor && model.IsRecord))
            {
                var sourceSpecificName = "ProjectionFrom" + GetSourceSimpleName(model);
                
                var baseSrcMatches = model.BaseHidesFacetMembers
                    && model.BaseFacetInfo?.BaseSourceTypeName == model.SourceTypeName;
                var aliasNewMod = baseSrcMatches ? "new " : "";
                sb.AppendLine();
                ProjectionGenerator.GenerateProjectionDocumentation(sb, model, memberIndent, sourceSpecificName);
                sb.AppendLine($"{memberIndent}public static {aliasNewMod}Expression<Func<{model.SourceTypeName}, {model.Name}>> {sourceSpecificName} => Projection;");
            }
        }

        if (model.GenerateToSource)
        {
            ToSourceGenerator.Generate(sb, model, facetLookup);
        }

        if (model.GenerateToSource && !model.SourceHasPositionalConstructor)
        {
            ToSourceGenerator.GenerateApplyToSource(sb, model, facetLookup);
        }

        if (model.FlattenToTypes.Length > 0)
        {
            FlattenToGenerator.Generate(sb, model, memberIndent, facetLookup);
        }

        if (shouldGenerateEquality)
        {
            EqualityGenerator.Generate(sb, model, memberIndent);
        }

        sb.AppendLine($"{containingTypeIndent}}}");

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
        Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        if (models.Count == 1)
            return Generate(models[0], facetLookup);

        return GenerateCombined(models, facetLookup);
    }

    /// <summary>
    /// Split-output variant of <see cref="GenerateForGroup"/>.
    /// Returns a Properties file (property declarations only) and a Mappings file
    /// (constructors, projections, and conversion methods).
    /// </summary>
    public static (string propertiesCode, string mappingsCode) GenerateForGroupSplit(
        IReadOnlyList<FacetTargetModel> models,
        Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        if (models.Count == 1)
            return GenerateSplit(models[0], facetLookup);

        return GenerateCombinedSplit(models, facetLookup);
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
        Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        var primaryModel = models[0];
        var sb = new StringBuilder();
        GenerateFileHeader(sb);

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

        var isPositional = primaryModel.IsRecord && !primaryModel.HasExistingPrimaryConstructor
            && !(primaryModel.TypeKind == TypeKind.Class && hasRequiredUnion);
        var shouldGenerateEquality = primaryModel.GenerateEquality && !primaryModel.IsRecord;

        if (isPositional)
        {
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

        if (!isPositional || primaryModel.HasExistingPrimaryConstructor)
        {
            MemberGenerator.GenerateMembers(sb, primaryModel, memberIndent, unionMembers);
        }

        if (primaryModel.GenerateParameterlessConstructor)
            ConstructorGenerator.GenerateParameterlessConstructor(sb, primaryModel, isPositional);

        foreach (var model in models)
        {
            if (!model.GenerateConstructor) continue;

            var hasCustomMapping = !string.IsNullOrWhiteSpace(model.ConfigurationTypeName);
            var needsDepthTracking = model.MaxDepth > 0 || model.PreserveReferences;
            var modelHasInitOnly = model.Members.Any(mem => mem.IsInitOnly);
            var modelHasRequired = model.Members.Any(mem => mem.IsRequired);

            ConstructorGenerator.GenerateConstructor(
                sb, model, isPositional, modelHasInitOnly, hasCustomMapping, modelHasRequired);

            if (hasCustomMapping && !model.HasMapConfiguration && model.HasProjectionMapConfiguration)
            {
                GenerateProjectionMapAction(sb, model, memberIndent);
            }
        }

        if (primaryModel.GenerateCopyConstructor)
            CopyConstructorGenerator.Generate(sb, primaryModel, memberIndent);

        foreach (var model in models)
        {
            if (!model.GenerateExpressionProjection) continue;

            var projectionName = GetProjectionName(model, models);
            ProjectionGenerator.GenerateProjectionProperty(sb, model, memberIndent, facetLookup, projectionName);
        }

        foreach (var model in models)
        {
            if (!model.GenerateToSource) continue;

            var toSourceName = GetToSourceMethodName(model, models);
            ToSourceGenerator.Generate(sb, model, facetLookup, toSourceName);
        }

        foreach (var model in models)
        {
            if (!model.GenerateToSource || model.SourceHasPositionalConstructor) continue;

            var applyMethodName = GetApplyToSourceMethodName(model, models);
            ToSourceGenerator.GenerateApplyToSource(sb, model, facetLookup, applyMethodName);
        }

        foreach (var model in models)
        {
            if (model.FlattenToTypes.Length > 0)
                FlattenToGenerator.Generate(sb, model, memberIndent, facetLookup);
        }

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
            return null; 

        return "To" + GetSourceSimpleName(model);
    }

    /// <summary>
    /// Returns the method name to use for the ApplyToSource operation of the given model.
    /// <list type="bullet">
    /// <item>Single-source: <c>null</c> → <c>"ApplyToSource"</c>.</item>
    /// <item>Multi-source: <c>"ApplyTo{SourceSimpleName}"</c>.</item>
    /// </list>
    /// </summary>
    private static string? GetApplyToSourceMethodName(FacetTargetModel model, IReadOnlyList<FacetTargetModel> allModels)
    {
        if (allModels.Count <= 1)
            return null; 

        return "ApplyTo" + GetSourceSimpleName(model);
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

    private static (string propertiesCode, string mappingsCode) GenerateSplit(
        FacetTargetModel model,
        Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        var mapsNamespaces = CodeGenerationHelpers.CollectNamespaces(model);
        var mapsStaticUsings = CodeGenerationHelpers.CollectStaticUsingTypes(model);
        var propsNamespaces = CodeGenerationHelpers.CollectNamespacesForProperties(model);
        var propsStaticUsings = CodeGenerationHelpers.CollectStaticUsingTypesForProperties(model);
        var hasNullableRefTypeMembers = model.Members.Any(m => !m.IsValueType && m.TypeName.EndsWith("?"));
        var needsNullable = hasNullableRefTypeMembers || model.MaxDepth > 0 || model.PreserveReferences;
        var needsNullableInProps = needsNullable;
        var needsNullableInMaps = needsNullable;
        var keyword = GetTypeKeyword(model);
        var hasInitOnlyProperties = model.Members.Any(m => m.IsInitOnly);
        var hasRequiredProperties = model.Members.Any(m => m.IsRequired);
        var isPositional = model.IsRecord && !model.HasExistingPrimaryConstructor
            && !(model.TypeKind == TypeKind.Class && hasRequiredProperties);
        var hasCustomMapping = !string.IsNullOrWhiteSpace(model.ConfigurationTypeName);
        var shouldGenerateEquality = model.GenerateEquality && !model.IsRecord;

        // --- Properties file ---
        var propsSb = new StringBuilder();
        var propsIndent = WritePreamble(propsSb, model, propsNamespaces, propsStaticUsings, needsNullableInProps, includeTypeDocs: true);

        if (isPositional)
            GeneratePositionalDeclaration(propsSb, model, keyword, propsIndent);

        var propsBaseList = FormatBaseTypeList(model.DeclaredBaseTypeNames);
        propsSb.AppendLine($"{propsIndent}{model.Accessibility} partial {keyword} {model.Name}{propsBaseList}");
        propsSb.AppendLine($"{propsIndent}{{");

        var propsMemberIndent = propsIndent + "    ";
        if (!isPositional || model.HasExistingPrimaryConstructor)
        {
            MemberGenerator.GenerateMembers(propsSb, model, propsMemberIndent);
        }
        else
        {
            var membersWithDocs = model.Members
                .Where(static m => !string.IsNullOrWhiteSpace(m.XmlDocumentation) && !m.IsUserDeclared)
                .ToList();
            MemberGenerator.GenerateMembers(propsSb, model, propsMemberIndent, membersWithDocs, usePropertyNameAsInitializer: true);
        }

        propsSb.AppendLine($"{propsIndent}}}");
        CloseContainingTypeHierarchy(propsSb, model, propsIndent);

        // --- Mappings file ---
        var mapsSb = new StringBuilder();
        var mapsIndent = WritePreamble(mapsSb, model, mapsNamespaces, mapsStaticUsings, needsNullableInMaps, includeTypeDocs: false);

        if (shouldGenerateEquality)
            mapsSb.AppendLine($"{mapsIndent}{model.Accessibility} partial {keyword} {model.Name} : {EqualityGenerator.GetEquatableInterface(model)}");
        else
            mapsSb.AppendLine($"{mapsIndent}{model.Accessibility} partial {keyword} {model.Name}");
        mapsSb.AppendLine($"{mapsIndent}{{");

        var mapsMemberIndent = mapsIndent + "    ";

        if (model.GenerateParameterlessConstructor)
            ConstructorGenerator.GenerateParameterlessConstructor(mapsSb, model, isPositional);

        if (model.GenerateConstructor)
            ConstructorGenerator.GenerateConstructor(mapsSb, model, isPositional, hasInitOnlyProperties, hasCustomMapping, hasRequiredProperties);

        if (hasCustomMapping && !model.HasMapConfiguration && model.HasProjectionMapConfiguration)
            GenerateProjectionMapAction(mapsSb, model, mapsMemberIndent);

        if (model.GenerateCopyConstructor)
            CopyConstructorGenerator.Generate(mapsSb, model, mapsMemberIndent);

        if (model.GenerateExpressionProjection)
        {
            ProjectionGenerator.GenerateProjectionProperty(mapsSb, model, mapsMemberIndent, facetLookup);

            if (!(model.HasExistingPrimaryConstructor && model.IsRecord))
            {
                var sourceSpecificName = "ProjectionFrom" + GetSourceSimpleName(model);
                var baseSrcMatches = model.BaseHidesFacetMembers
                    && model.BaseFacetInfo?.BaseSourceTypeName == model.SourceTypeName;
                var aliasNewMod = baseSrcMatches ? "new " : "";
                mapsSb.AppendLine();
                ProjectionGenerator.GenerateProjectionDocumentation(mapsSb, model, mapsMemberIndent, sourceSpecificName);
                mapsSb.AppendLine($"{mapsMemberIndent}public static {aliasNewMod}Expression<Func<{model.SourceTypeName}, {model.Name}>> {sourceSpecificName} => Projection;");
            }
        }

        if (model.GenerateToSource)
            ToSourceGenerator.Generate(mapsSb, model, facetLookup);

        if (model.GenerateToSource && !model.SourceHasPositionalConstructor)
            ToSourceGenerator.GenerateApplyToSource(mapsSb, model, facetLookup);

        if (model.FlattenToTypes.Length > 0)
            FlattenToGenerator.Generate(mapsSb, model, mapsMemberIndent, facetLookup);

        if (shouldGenerateEquality)
            EqualityGenerator.Generate(mapsSb, model, mapsMemberIndent);

        mapsSb.AppendLine($"{mapsIndent}}}");
        CloseContainingTypeHierarchy(mapsSb, model, mapsIndent);

        return (propsSb.ToString(), mapsSb.ToString());
    }

    private static (string propertiesCode, string mappingsCode) GenerateCombinedSplit(
        IReadOnlyList<FacetTargetModel> models,
        Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        var primaryModel = models[0];

        var mapsNamespaces = new HashSet<string>();
        var mapsStaticUsings = new HashSet<string>();
        var propsNamespaces = new HashSet<string>();
        var propsStaticUsings = new HashSet<string>();
        foreach (var m in models)
        {
            foreach (var ns in CodeGenerationHelpers.CollectNamespaces(m))
                mapsNamespaces.Add(ns);
            foreach (var su in CodeGenerationHelpers.CollectStaticUsingTypes(m))
                mapsStaticUsings.Add(su);
            foreach (var ns in CodeGenerationHelpers.CollectNamespacesForProperties(m))
                propsNamespaces.Add(ns);
            foreach (var su in CodeGenerationHelpers.CollectStaticUsingTypesForProperties(m))
                propsStaticUsings.Add(su);
        }

        var needsNullable = models.Any(m => m.Members.Any(mem => !mem.IsValueType && mem.TypeName.EndsWith("?")))
            || models.Any(m => m.MaxDepth > 0 || m.PreserveReferences);
        var needsNullableInProps = needsNullable;
        var needsNullableInMaps = needsNullable;

        var keyword = GetTypeKeyword(primaryModel);

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

        var hasRequiredUnion = unionMembers.Any(m => m.IsRequired);
        var isPositional = primaryModel.IsRecord && !primaryModel.HasExistingPrimaryConstructor
            && !(primaryModel.TypeKind == TypeKind.Class && hasRequiredUnion);
        var shouldGenerateEquality = primaryModel.GenerateEquality && !primaryModel.IsRecord;

        // --- Properties file ---
        var propsSb = new StringBuilder();
        var propsIndent = WritePreamble(propsSb, primaryModel, propsNamespaces, propsStaticUsings, needsNullableInProps, includeTypeDocs: true);

        if (isPositional)
            GeneratePositionalDeclaration(propsSb, primaryModel, keyword, propsIndent);

        var propsBaseList = FormatBaseTypeList(primaryModel.DeclaredBaseTypeNames);
        propsSb.AppendLine($"{propsIndent}{primaryModel.Accessibility} partial {keyword} {primaryModel.Name}{propsBaseList}");
        propsSb.AppendLine($"{propsIndent}{{");

        if (!isPositional || primaryModel.HasExistingPrimaryConstructor)
            MemberGenerator.GenerateMembers(propsSb, primaryModel, propsIndent + "    ", unionMembers);

        propsSb.AppendLine($"{propsIndent}}}");
        CloseContainingTypeHierarchy(propsSb, primaryModel, propsIndent);

        // --- Mappings file ---
        var mapsSb = new StringBuilder();
        var mapsIndent = WritePreamble(mapsSb, primaryModel, mapsNamespaces, mapsStaticUsings, needsNullableInMaps, includeTypeDocs: false);

        if (shouldGenerateEquality)
            mapsSb.AppendLine($"{mapsIndent}{primaryModel.Accessibility} partial {keyword} {primaryModel.Name} : {EqualityGenerator.GetEquatableInterface(primaryModel)}");
        else
            mapsSb.AppendLine($"{mapsIndent}{primaryModel.Accessibility} partial {keyword} {primaryModel.Name}");
        mapsSb.AppendLine($"{mapsIndent}{{");

        var mapsMemberIndent = mapsIndent + "    ";

        if (primaryModel.GenerateParameterlessConstructor)
            ConstructorGenerator.GenerateParameterlessConstructor(mapsSb, primaryModel, isPositional);

        foreach (var model in models)
        {
            if (!model.GenerateConstructor) continue;

            var hasCustomMapping = !string.IsNullOrWhiteSpace(model.ConfigurationTypeName);
            var modelHasInitOnly = model.Members.Any(mem => mem.IsInitOnly);
            var modelHasRequired = model.Members.Any(mem => mem.IsRequired);

            ConstructorGenerator.GenerateConstructor(
                mapsSb, model, isPositional, modelHasInitOnly, hasCustomMapping, modelHasRequired);

            if (hasCustomMapping && !model.HasMapConfiguration && model.HasProjectionMapConfiguration)
                GenerateProjectionMapAction(mapsSb, model, mapsMemberIndent);
        }

        if (primaryModel.GenerateCopyConstructor)
            CopyConstructorGenerator.Generate(mapsSb, primaryModel, mapsMemberIndent);

        foreach (var model in models)
        {
            if (!model.GenerateExpressionProjection) continue;

            var projectionName = GetProjectionName(model, models);
            ProjectionGenerator.GenerateProjectionProperty(mapsSb, model, mapsMemberIndent, facetLookup, projectionName);
        }

        foreach (var model in models)
        {
            if (!model.GenerateToSource) continue;

            var toSourceName = GetToSourceMethodName(model, models);
            ToSourceGenerator.Generate(mapsSb, model, facetLookup, toSourceName);
        }

        foreach (var model in models)
        {
            if (!model.GenerateToSource || model.SourceHasPositionalConstructor) continue;

            var applyMethodName = GetApplyToSourceMethodName(model, models);
            ToSourceGenerator.GenerateApplyToSource(mapsSb, model, facetLookup, applyMethodName);
        }

        foreach (var model in models)
        {
            if (model.FlattenToTypes.Length > 0)
                FlattenToGenerator.Generate(mapsSb, model, mapsMemberIndent, facetLookup);
        }

        if (shouldGenerateEquality)
            EqualityGenerator.Generate(mapsSb, primaryModel, mapsMemberIndent);

        mapsSb.AppendLine($"{mapsIndent}}}");
        CloseContainingTypeHierarchy(mapsSb, primaryModel, mapsIndent);

        return (propsSb.ToString(), mapsSb.ToString());
    }

    private static string WritePreamble(
        StringBuilder sb,
        FacetTargetModel model,
        IEnumerable<string> namespacesToImport,
        IEnumerable<string> staticUsingTypes,
        bool needsNullable,
        bool includeTypeDocs)
    {
        GenerateFileHeader(sb);

        foreach (var ns in namespacesToImport.OrderBy(x => x))
            sb.AppendLine($"using {ns};");

        foreach (var type in staticUsingTypes.OrderBy(x => x))
            sb.AppendLine($"using static {type};");

        sb.AppendLine();

        if (needsNullable)
        {
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(model.Namespace))
            sb.AppendLine($"namespace {model.Namespace};");

        var indent = GenerateContainingTypeHierarchy(sb, model);

        if (includeTypeDocs && !string.IsNullOrWhiteSpace(model.TypeXmlDocumentation))
        {
            var indentedDoc = model.TypeXmlDocumentation!.Replace("\n", $"\n{indent}");
            sb.AppendLine($"{indent}{indentedDoc}");
        }

        return indent;
    }


    /// <c>ConfigureProjection</c> expressions. Called when the configuration type
    /// implements <c>IFacetProjectionMapConfiguration</c> but not <c>IFacetMapConfiguration</c>,
    /// allowing users to write mapping logic once as expressions and reuse it in both
    /// projections (EF Core) and constructors (in-memory).
    /// </summary>
    private static void GenerateProjectionMapAction(StringBuilder sb, FacetTargetModel model, string memberIndent)
    {
        var src = model.SourceTypeName;
        var tgt = model.Name;
        var bodyIndent = memberIndent + "    ";

        sb.AppendLine();
        sb.AppendLine($"{memberIndent}private static global::System.Action<{src}, {tgt}>? __projectionMapAction;");
        sb.AppendLine();
        sb.AppendLine($"{memberIndent}private static global::System.Action<{src}, {tgt}> __GetProjectionMapAction()");
        sb.AppendLine($"{memberIndent}{{");
        sb.AppendLine($"{bodyIndent}return global::System.Threading.LazyInitializer.EnsureInitialized(ref __projectionMapAction, () =>");
        sb.AppendLine($"{bodyIndent}{{");

        var innerIndent = bodyIndent + "    ";

        sb.AppendLine($"{innerIndent}var __sourceParam = global::System.Linq.Expressions.Expression.Parameter(typeof({src}), \"source\");");
        sb.AppendLine($"{innerIndent}var __targetParam = global::System.Linq.Expressions.Expression.Parameter(typeof({tgt}), \"target\");");
        sb.AppendLine($"{innerIndent}var __assignments = new global::System.Collections.Generic.List<global::System.Linq.Expressions.Expression>();");
        sb.AppendLine();

        if (model.BaseFacetInfo?.AllBaseProjectionConfigs.Length > 0)
        {
            sb.AppendLine($"{innerIndent}// Apply base Facet projection mappings");
            for (int __cfgIdx = 0; __cfgIdx < model.BaseFacetInfo.AllBaseProjectionConfigs.Length; __cfgIdx++)
            {
                var (cfgTypeName, cfgSrcTypeName, cfgTgtTypeName) = model.BaseFacetInfo.AllBaseProjectionConfigs[__cfgIdx];
                var builderVar = __cfgIdx == 0 ? "__baseBuilder" : $"__baseBuilder{__cfgIdx}";
                sb.AppendLine($"{innerIndent}var {builderVar} = new global::Facet.Mapping.FacetProjectionBuilder<{cfgSrcTypeName}, {cfgTgtTypeName}>();");
                sb.AppendLine($"{innerIndent}{cfgTypeName}.ConfigureProjection({builderVar});");
                sb.AppendLine($"{innerIndent}foreach (var (__member, __expr) in {builderVar}.Mappings)");
                sb.AppendLine($"{innerIndent}{{");
                sb.AppendLine($"{innerIndent}    var __derivedMember = typeof({tgt}).GetProperty(__member.Name);");
                sb.AppendLine($"{innerIndent}    if (__derivedMember != null)");
                sb.AppendLine($"{innerIndent}    {{");
                sb.AppendLine($"{innerIndent}        var __body = global::Facet.Mapping.ParameterReplacer.Replace(__expr, __sourceParam);");
                sb.AppendLine($"{innerIndent}        __assignments.Add(global::System.Linq.Expressions.Expression.Assign(");
                sb.AppendLine($"{innerIndent}            global::System.Linq.Expressions.Expression.MakeMemberAccess(__targetParam, __derivedMember), __body));");
                sb.AppendLine($"{innerIndent}    }}");
                sb.AppendLine($"{innerIndent}}}");
                sb.AppendLine();
            }
        }

        sb.AppendLine($"{innerIndent}var __builder = new global::Facet.Mapping.FacetProjectionBuilder<{src}, {tgt}>();");
        sb.AppendLine($"{innerIndent}global::{model.ConfigurationTypeName}.ConfigureProjection(__builder);");
        sb.AppendLine($"{innerIndent}foreach (var (__member, __expr) in __builder.Mappings)");
        sb.AppendLine($"{innerIndent}{{");
        sb.AppendLine($"{innerIndent}    var __body = global::Facet.Mapping.ParameterReplacer.Replace(__expr, __sourceParam);");
        sb.AppendLine($"{innerIndent}    __assignments.Add(global::System.Linq.Expressions.Expression.Assign(");
        sb.AppendLine($"{innerIndent}        global::System.Linq.Expressions.Expression.MakeMemberAccess(__targetParam, __member), __body));");
        sb.AppendLine($"{innerIndent}}}");
        sb.AppendLine();
        sb.AppendLine($"{innerIndent}if (__assignments.Count == 0)");
        sb.AppendLine($"{innerIndent}    return (_, _) => {{ }};");
        sb.AppendLine();
        sb.AppendLine($"{innerIndent}var __block = global::System.Linq.Expressions.Expression.Block(__assignments);");
        sb.AppendLine($"{innerIndent}return global::System.Linq.Expressions.Expression.Lambda<global::System.Action<{src}, {tgt}>>(");
        sb.AppendLine($"{innerIndent}    __block, __sourceParam, __targetParam).Compile();");

        sb.AppendLine($"{bodyIndent}}})!;");
        sb.AppendLine($"{memberIndent}}}");
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

    /// <summary>
    /// Formats the base-type list for a class/struct/record declaration.
    /// Returns an empty string when there are no declared base types,
    /// otherwise returns <c>" : TypeA, TypeB"</c> (leading space included).
    /// </summary>
    private static string FormatBaseTypeList(ImmutableArray<string> baseTypeNames)
    {
        if (baseTypeNames.IsDefaultOrEmpty) return string.Empty;
        return " : " + string.Join(", ", baseTypeNames);
    }

    private static string GenerateContainingTypeHierarchy(StringBuilder sb, FacetTargetModel model)
    {
        var containingTypeIndent = "";
        foreach (var containingType in model.ContainingTypes)
        {
            sb.AppendLine($"{containingTypeIndent}partial class {containingType}");
            sb.AppendLine($"{containingTypeIndent}{{");
            containingTypeIndent += "    ";
        }
        return containingTypeIndent;
    }

    private static void CloseContainingTypeHierarchy(StringBuilder sb, FacetTargetModel model, string containingTypeIndent)
    {
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
                
                if (m.IsRequired && model.TypeKind == TypeKind.Struct && model.IsRecord)
                {
                    param = $"required {param}";
                }
                return param;
            }));
        
        sb.AppendLine($"{indent}#pragma warning disable CS1591");
        sb.AppendLine($"{indent}{model.Accessibility} partial {keyword} {model.Name}({parameters});");
        sb.AppendLine($"{indent}#pragma warning restore CS1591");
    }
}
