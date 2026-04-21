using Facet.Generators.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facet.Generators;

/// <summary>
/// Generates LINQ projection expressions for efficient database query projections.
/// </summary>
internal static class ProjectionGenerator
{
    /// <summary>
    /// Generates the projection property for LINQ/EF Core query optimization.
    /// </summary>
    /// <param name="projectionPropertyName">
    /// The name to use for the generated static property.
    /// Defaults to <c>"Projection"</c> when <see langword="null"/>.
    /// Pass a custom name (e.g. <c>"ProjectionFromUnitEntity"</c>) for multi-source facets.
    /// </param>
    public static void GenerateProjectionProperty(
        StringBuilder sb,
        FacetTargetModel model,
        string memberIndent,
        Dictionary<string, List<FacetTargetModel>> facetLookup,
        string? projectionPropertyName = null)
    {
        var propertyName = projectionPropertyName ?? "Projection";
        sb.AppendLine();

        if (model.HasExistingPrimaryConstructor && model.IsRecord)
        {
            GenerateProjectionNotSupportedComment(sb, model, memberIndent);
        }
        else if (model.HasProjectionMapConfiguration)
        {
            GenerateProjectionDocumentation(sb, model, memberIndent, propertyName);
            GenerateLazyProjection(sb, model, memberIndent, facetLookup, propertyName);
        }
        else
        {
            GenerateProjectionDocumentation(sb, model, memberIndent, propertyName);
            var newMod = model.BaseHidesFacetMembers && propertyName == "Projection" ? "new " : "";
            sb.AppendLine($"{memberIndent}public static {newMod}Expression<Func<{model.SourceTypeName}, {model.Name}>> {propertyName} =>");

            // Generate object initializer projection for EF Core compatibility
            GenerateProjectionExpression(sb, model, memberIndent, facetLookup);
        }
    }

    /// <summary>
    /// Generates a lazily-built projection that inlines ConfigureProjection bindings so that
    /// EF Core can translate the result to SQL without encountering any Invoke nodes.
    /// Emits: a backing field, a Projection property delegating to LazyInitializer, and a
    /// BuildProjection() method that assembles a MemberInitExpression at runtime.
    /// </summary>
    private static void GenerateLazyProjection(
        StringBuilder sb,
        FacetTargetModel model,
        string memberIndent,
        Dictionary<string, List<FacetTargetModel>> facetLookup,
        string propertyName = "Projection")
    {
        var newModifier = model.BaseHidesFacetMembers && propertyName == "Projection" ? "new " : "";
        var src = model.SourceTypeName;
        var tgt = model.Name;

        // Derive a unique backing-field name from the property name to avoid collisions in multi-source scenarios.
        var safeName = string.IsNullOrEmpty(propertyName) ? "Projection" : propertyName;
        var backingFieldName = "_" + char.ToLowerInvariant(safeName[0]) + safeName.Substring(1);
        var buildMethodName = "Build" + safeName;

        // Check whether this model has nested facet members that could trigger circular references
        var nestedFacetMembers = model.Members
            .Where(m => m.MapFromIncludeInProjection && m.IsNestedFacet)
            .ToArray();
        bool hasNestedFacets = nestedFacetMembers.Length > 0;

        // Backing field
        sb.AppendLine($"{memberIndent}private static global::System.Linq.Expressions.Expression<global::System.Func<{src}, {tgt}>>? {backingFieldName};");
        sb.AppendLine();

        if (hasNestedFacets)
        {
            // Thread-static build stack to detect circular references at runtime.
            // When BuildProjection for type A accesses NestedDto.Projection and that
            // eventually cycles back to A, the re-entrant call detects A is already on
            // the stack and builds a shallow projection (scalar + ConfigureProjection
            // only, no nested facets) to break the cycle.
            sb.AppendLine($"{memberIndent}[global::System.ThreadStatic]");
            sb.AppendLine($"{memberIndent}private static global::System.Collections.Generic.HashSet<string>? __projectionBuildStack;");
            sb.AppendLine();

            // Projection property with re-entrance guard
            sb.AppendLine($"{memberIndent}public static {newModifier}global::System.Linq.Expressions.Expression<global::System.Func<{src}, {tgt}>> {propertyName}");
            sb.AppendLine($"{memberIndent}{{");
            sb.AppendLine($"{memberIndent}    get");
            sb.AppendLine($"{memberIndent}    {{");
            sb.AppendLine($"{memberIndent}        var __cached = global::System.Threading.Volatile.Read(ref {backingFieldName});");
            sb.AppendLine($"{memberIndent}        if (__cached != null) return __cached;");
            sb.AppendLine();
            sb.AppendLine($"{memberIndent}        __projectionBuildStack ??= new global::System.Collections.Generic.HashSet<string>();");
            sb.AppendLine($"{memberIndent}        var __key = \"{tgt}:{safeName}\";");
            sb.AppendLine($"{memberIndent}        bool __isReentrant = !__projectionBuildStack.Add(__key);");
            sb.AppendLine($"{memberIndent}        try");
            sb.AppendLine($"{memberIndent}        {{");
            sb.AppendLine($"{memberIndent}            var __result = {buildMethodName}(!__isReentrant);");
            sb.AppendLine($"{memberIndent}            if (!__isReentrant)");
            sb.AppendLine($"{memberIndent}            {{");
            sb.AppendLine($"{memberIndent}                global::System.Threading.Volatile.Write(ref {backingFieldName}, __result);");
            sb.AppendLine($"{memberIndent}            }}");
            sb.AppendLine($"{memberIndent}            return __result;");
            sb.AppendLine($"{memberIndent}        }}");
            sb.AppendLine($"{memberIndent}        finally");
            sb.AppendLine($"{memberIndent}        {{");
            sb.AppendLine($"{memberIndent}            if (!__isReentrant) __projectionBuildStack.Remove(__key);");
            sb.AppendLine($"{memberIndent}        }}");
            sb.AppendLine($"{memberIndent}    }}");
            sb.AppendLine($"{memberIndent}}}");
        }
        else
        {
            // No nested facets — no risk of circular references; use simple lazy init
            sb.AppendLine($"{memberIndent}public static {newModifier}global::System.Linq.Expressions.Expression<global::System.Func<{src}, {tgt}>> {propertyName}");
            sb.AppendLine($"{memberIndent}    => global::System.Threading.LazyInitializer.EnsureInitialized(ref {backingFieldName}, () => {buildMethodName}(true));");
        }
        sb.AppendLine();

        // BuildProjection() method — accepts includeNestedFacets flag
        sb.AppendLine($"{memberIndent}private static global::System.Linq.Expressions.Expression<global::System.Func<{src}, {tgt}>> {buildMethodName}(bool __includeNestedFacets)");
        sb.AppendLine($"{memberIndent}{{");

        var bodyIndent = memberIndent + "    ";

        // Source parameter
        sb.AppendLine($"{bodyIndent}var __p = global::System.Linq.Expressions.Expression.Parameter(typeof({src}), \"source\");");
        sb.AppendLine();

        // Auto-generated bindings list
        sb.AppendLine($"{bodyIndent}var __bindings = new global::System.Collections.Generic.List<global::System.Linq.Expressions.MemberBinding>");
        sb.AppendLine($"{bodyIndent}{{");

        var includedMembers = model.Members
            .Where(m => m.MapFromIncludeInProjection && !m.IsNestedFacet)
            .ToArray();

        for (int i = 0; i < includedMembers.Length; i++)
        {
            var member = includedMembers[i];
            var bindingExpr = GetMemberBindingExpression(member, "__p", tgt);
            if (bindingExpr == null) continue;

            var comma = i < includedMembers.Length - 1 ? "," : "";
            sb.AppendLine($"{bodyIndent}    {bindingExpr}{comma}");
        }

        sb.AppendLine($"{bodyIndent}}};");
        sb.AppendLine();

        // Generate bindings for nested facet members (guarded by __includeNestedFacets)
        if (hasNestedFacets)
        {
            sb.AppendLine($"{bodyIndent}if (__includeNestedFacets)");
            sb.AppendLine($"{bodyIndent}{{");

            var nestedIndent = bodyIndent + "    ";
            foreach (var member in nestedFacetMembers)
            {
                GenerateNestedFacetBindingForLazyProjection(sb, member, tgt, nestedIndent, facetLookup);
            }

            sb.AppendLine($"{bodyIndent}}}");
            sb.AppendLine();
        }

        // Apply base Facet ConfigureProjection if present
        if (model.BaseFacetInfo?.BaseConfigurationTypeName != null)
        {
            sb.AppendLine($"{bodyIndent}// Apply base Facet projection mappings");
            sb.AppendLine($"{bodyIndent}var __baseBuilder = new global::Facet.Mapping.FacetProjectionBuilder<{model.BaseFacetInfo.BaseSourceTypeName}, {model.BaseFacetInfo.BaseTypeName}>();");
            sb.AppendLine($"{bodyIndent}{model.BaseFacetInfo.BaseConfigurationTypeName}.ConfigureProjection(__baseBuilder);");
            sb.AppendLine($"{bodyIndent}foreach (var (__member, __expr) in __baseBuilder.Mappings)");
            sb.AppendLine($"{bodyIndent}{{");
            sb.AppendLine($"{bodyIndent}    // Get the corresponding member in the derived type");
            sb.AppendLine($"{bodyIndent}    var __derivedMember = typeof({tgt}).GetProperty(__member.Name);");
            sb.AppendLine($"{bodyIndent}    if (__derivedMember != null)");
            sb.AppendLine($"{bodyIndent}    {{");
            sb.AppendLine($"{bodyIndent}        var __body = global::Facet.Mapping.ParameterReplacer.Replace(__expr, __p);");
            sb.AppendLine($"{bodyIndent}        __bindings.RemoveAll(b => ((global::System.Linq.Expressions.MemberAssignment)b).Member.Name == __derivedMember.Name);");
            sb.AppendLine($"{bodyIndent}        __bindings.Add(global::System.Linq.Expressions.Expression.Bind(__derivedMember, __body));");
            sb.AppendLine($"{bodyIndent}    }}");
            sb.AppendLine($"{bodyIndent}}}");
            sb.AppendLine();
        }

        // Apply ConfigureProjection overrides from derived class
        sb.AppendLine($"{bodyIndent}var __builder = new global::Facet.Mapping.FacetProjectionBuilder<{src}, {tgt}>();");
        sb.AppendLine($"{bodyIndent}global::{model.ConfigurationTypeName}.ConfigureProjection(__builder);");
        sb.AppendLine($"{bodyIndent}foreach (var (__member, __expr) in __builder.Mappings)");
        sb.AppendLine($"{bodyIndent}{{");
        sb.AppendLine($"{bodyIndent}    var __body = global::Facet.Mapping.ParameterReplacer.Replace(__expr, __p);");
        sb.AppendLine($"{bodyIndent}    __bindings.RemoveAll(b => ((global::System.Linq.Expressions.MemberAssignment)b).Member.Name == __member.Name);");
        sb.AppendLine($"{bodyIndent}    __bindings.Add(global::System.Linq.Expressions.Expression.Bind(__member, __body));");
        sb.AppendLine($"{bodyIndent}}}");
        sb.AppendLine();

        // Build and return the final lambda
        sb.AppendLine($"{bodyIndent}return global::System.Linq.Expressions.Expression.Lambda<global::System.Func<{src}, {tgt}>>(");
        sb.AppendLine($"{bodyIndent}    global::System.Linq.Expressions.Expression.MemberInit(");
        sb.AppendLine($"{bodyIndent}        global::System.Linq.Expressions.Expression.New(typeof({tgt})),");
        sb.AppendLine($"{bodyIndent}        __bindings),");
        sb.AppendLine($"{bodyIndent}    __p);");
        sb.AppendLine($"{memberIndent}}}");
    }

    /// <summary>
    /// Generates an <c>Expression.Bind</c> call for a nested facet member inside the lazy projection builder.
    /// Inlines the nested Facet's <c>Projection</c> expression using <c>ParameterReplacer.ReplaceParameter</c>.
    /// Handles single (nullable and non-nullable) and collection nested facets.
    /// </summary>
    private static void GenerateNestedFacetBindingForLazyProjection(
        StringBuilder sb,
        FacetMember member,
        string targetTypeName,
        string indent,
        Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        var sourcePropName = member.SourcePropertyName;

        if (member.IsCollection)
        {
            GenerateCollectionNestedFacetLazyBinding(sb, member, targetTypeName, indent, facetLookup);
        }
        else
        {
            GenerateSingleNestedFacetLazyBinding(sb, member, targetTypeName, indent, facetLookup);
        }
    }

    /// <summary>
    /// Generates runtime expression-tree code for a single (non-collection) nested facet binding.
    /// </summary>
    private static void GenerateSingleNestedFacetLazyBinding(
        StringBuilder sb,
        FacetMember member,
        string targetTypeName,
        string indent,
        Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        var sourcePropName = member.SourcePropertyName;
        var nonNullableTypeName = member.TypeName.TrimEnd('?');
        bool isNullable = member.TypeName.EndsWith("?");

        // Resolve the Projection property name on the nested DTO
        var projectionPropertyAccess = ResolveNestedProjectionPropertyAccess(
            nonNullableTypeName, member.NestedFacetSourceTypeName, facetLookup);

        sb.AppendLine($"{indent}// Nested facet binding: {member.Name}");
        sb.AppendLine($"{indent}{{");

        var innerIndent = indent + "    ";

        sb.AppendLine($"{innerIndent}var __nfProp = global::System.Linq.Expressions.Expression.Property(__p, \"{sourcePropName}\");");
        sb.AppendLine($"{innerIndent}var __nfLambda = (global::System.Linq.Expressions.LambdaExpression){projectionPropertyAccess};");
        sb.AppendLine($"{innerIndent}var __nfBody = (global::System.Linq.Expressions.Expression)global::Facet.Mapping.ParameterReplacer.ReplaceParameter(__nfLambda, __nfProp);");

        if (isNullable)
        {
            // Wrap with null check: source.Prop != null ? projected : default
            sb.AppendLine($"{innerIndent}__nfBody = global::System.Linq.Expressions.Expression.Condition(");
            sb.AppendLine($"{innerIndent}    global::System.Linq.Expressions.Expression.NotEqual(__nfProp, global::System.Linq.Expressions.Expression.Constant(null, __nfProp.Type)),");
            sb.AppendLine($"{innerIndent}    __nfBody,");
            sb.AppendLine($"{innerIndent}    global::System.Linq.Expressions.Expression.Default(typeof({nonNullableTypeName})));");
        }

        sb.AppendLine($"{innerIndent}__bindings.Add(global::System.Linq.Expressions.Expression.Bind(");
        sb.AppendLine($"{innerIndent}    typeof({targetTypeName}).GetProperty(\"{member.Name}\")!,");
        sb.AppendLine($"{innerIndent}    __nfBody));");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates runtime expression-tree code for a collection nested facet binding.
    /// Builds: source.Collection.Select(nestedProjection).ToList()  (or appropriate wrapper)
    /// </summary>
    private static void GenerateCollectionNestedFacetLazyBinding(
        StringBuilder sb,
        FacetMember member,
        string targetTypeName,
        string indent,
        Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        var sourcePropName = member.SourcePropertyName;
        var elementTypeName = ExpressionBuilder.ExtractElementTypeFromCollectionTypeName(member.TypeName);
        var nonNullableElementType = elementTypeName.TrimEnd('?');
        bool isNullableCollection = member.TypeName.EndsWith("?");

        // Determine source element type
        var sourceElementType = member.NestedFacetSourceTypeName ??
            (member.SourceMemberTypeName != null
                ? ExpressionBuilder.ExtractElementTypeFromCollectionTypeName(member.SourceMemberTypeName)
                : elementTypeName);

        // Resolve the Projection property name on the nested DTO element type
        var projectionPropertyAccess = ResolveNestedProjectionPropertyAccess(
            nonNullableElementType, sourceElementType, facetLookup);

        // Determine the ToList/ToArray wrapper method
        var collectionWrapper = member.CollectionWrapper ?? "List";
        var wrapperMethodCall = GetCollectionWrapperMethodCall(collectionWrapper, nonNullableElementType);

        sb.AppendLine($"{indent}// Collection nested facet binding: {member.Name}");
        sb.AppendLine($"{indent}{{");

        var innerIndent = indent + "    ";

        sb.AppendLine($"{innerIndent}var __nfCollectionProp = global::System.Linq.Expressions.Expression.Property(__p, \"{sourcePropName}\");");
        sb.AppendLine($"{innerIndent}var __nfProjection = (global::System.Linq.Expressions.LambdaExpression){projectionPropertyAccess};");
        sb.AppendLine();

        // Build Enumerable.Select call expression
        sb.AppendLine($"{innerIndent}// Build: source.{sourcePropName}.Select(projection){wrapperMethodCall}");
        sb.AppendLine($"{innerIndent}var __nfSelectMethod = typeof(global::System.Linq.Enumerable)");
        sb.AppendLine($"{innerIndent}    .GetMethods(global::System.Reflection.BindingFlags.Static | global::System.Reflection.BindingFlags.Public)");
        sb.AppendLine($"{innerIndent}    .First(m => m.Name == \"Select\" && m.GetParameters().Length == 2");
        sb.AppendLine($"{innerIndent}        && m.GetParameters()[1].ParameterType.IsGenericType");
        sb.AppendLine($"{innerIndent}        && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(global::System.Func<,>))");
        sb.AppendLine($"{innerIndent}    .MakeGenericMethod(typeof({sourceElementType}), typeof({nonNullableElementType}));");
        sb.AppendLine();
        sb.AppendLine($"{innerIndent}var __nfSelectCall = global::System.Linq.Expressions.Expression.Call(null, __nfSelectMethod, __nfCollectionProp, __nfProjection);");
        sb.AppendLine();

        // Build the wrapper call (ToList, ToArray, etc.)
        GenerateCollectionWrapperExpression(sb, innerIndent, collectionWrapper, nonNullableElementType);

        if (isNullableCollection)
        {
            sb.AppendLine($"{innerIndent}var __nfFinal = global::System.Linq.Expressions.Expression.Condition(");
            sb.AppendLine($"{innerIndent}    global::System.Linq.Expressions.Expression.NotEqual(__nfCollectionProp, global::System.Linq.Expressions.Expression.Constant(null, __nfCollectionProp.Type)),");
            sb.AppendLine($"{innerIndent}    __nfWrapped,");
            sb.AppendLine($"{innerIndent}    global::System.Linq.Expressions.Expression.Default(typeof({member.TypeName.TrimEnd('?')})));");
            sb.AppendLine();
            sb.AppendLine($"{innerIndent}__bindings.Add(global::System.Linq.Expressions.Expression.Bind(");
            sb.AppendLine($"{innerIndent}    typeof({targetTypeName}).GetProperty(\"{member.Name}\")!,");
            sb.AppendLine($"{innerIndent}    __nfFinal));");
        }
        else
        {
            sb.AppendLine($"{innerIndent}__bindings.Add(global::System.Linq.Expressions.Expression.Bind(");
            sb.AppendLine($"{innerIndent}    typeof({targetTypeName}).GetProperty(\"{member.Name}\")!,");
            sb.AppendLine($"{innerIndent}    __nfWrapped));");
        }

        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates the Expression.Call for the appropriate collection wrapper (ToList, ToArray, etc.)
    /// and assigns it to __nfWrapped.
    /// </summary>
    private static void GenerateCollectionWrapperExpression(
        StringBuilder sb,
        string indent,
        string collectionWrapper,
        string elementTypeName)
    {
        switch (collectionWrapper)
        {
            case FacetConstants.CollectionWrappers.Array:
                sb.AppendLine($"{indent}var __nfWrapMethod = typeof(global::System.Linq.Enumerable).GetMethod(\"ToArray\")!.MakeGenericMethod(typeof({elementTypeName}));");
                sb.AppendLine($"{indent}var __nfWrapped = global::System.Linq.Expressions.Expression.Call(null, __nfWrapMethod, __nfSelectCall);");
                break;
            case FacetConstants.CollectionWrappers.IEnumerable:
                // No wrapper needed - Select already returns IEnumerable
                sb.AppendLine($"{indent}var __nfWrapped = (global::System.Linq.Expressions.Expression)__nfSelectCall;");
                break;
            default:
                // Default to ToList() for List, IList, ICollection, etc.
                sb.AppendLine($"{indent}var __nfWrapMethod = typeof(global::System.Linq.Enumerable).GetMethod(\"ToList\")!.MakeGenericMethod(typeof({elementTypeName}));");
                sb.AppendLine($"{indent}var __nfWrapped = global::System.Linq.Expressions.Expression.Call(null, __nfWrapMethod, __nfSelectCall);");
                break;
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Resolves the fully qualified access to a nested Facet's Projection property.
    /// Determines whether to use <c>Projection</c> or <c>ProjectionFrom{Source}</c> based on
    /// whether the nested DTO is multi-source.
    /// </summary>
    private static string ResolveNestedProjectionPropertyAccess(
        string nestedDtoTypeName,
        string? nestedSourceTypeName,
        Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        // Strip "global::" prefix and extract simple name for lookup
        var lookupName = nestedDtoTypeName
            .Replace(Shared.GeneratorUtilities.GlobalPrefix, "")
            .Split('.', ':')
            .Last();

        List<FacetTargetModel>? models = null;

        // Try exact match
        if (!facetLookup.TryGetValue(lookupName, out models) || models == null || models.Count == 0)
        {
            // Try matching by simple name in values
            foreach (var kvp in facetLookup)
            {
                if (kvp.Value.Count > 0)
                {
                    var m = kvp.Value[0];
                    if (kvp.Key == lookupName || m.Name == nestedDtoTypeName ||
                        kvp.Key.EndsWith("." + lookupName))
                    {
                        models = kvp.Value;
                        break;
                    }
                }
            }
        }

        if (models == null || models.Count <= 1)
        {
            // Single-source: use "Projection"
            return $"{nestedDtoTypeName}.Projection";
        }

        // Multi-source: find the matching model and use "ProjectionFrom{SourceSimpleName}"
        if (nestedSourceTypeName != null)
        {
            foreach (var model in models)
            {
                if (model.SourceTypeName == nestedSourceTypeName)
                {
                    var sourceSimpleName = CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName);
                    var angleBracket = sourceSimpleName.IndexOf('<');
                    if (angleBracket > 0) sourceSimpleName = sourceSimpleName.Substring(0, angleBracket);
                    return $"{nestedDtoTypeName}.ProjectionFrom{sourceSimpleName}";
                }
            }
        }

        // Fallback to "Projection"
        return $"{nestedDtoTypeName}.Projection";
    }

    /// <summary>
    /// Gets the wrapper method call suffix for a collection type (e.g., ".ToList()", ".ToArray()").
    /// Used only for code comments.
    /// </summary>
    private static string GetCollectionWrapperMethodCall(string collectionWrapper, string elementTypeName)
    {
        return collectionWrapper switch
        {
            FacetConstants.CollectionWrappers.Array => ".ToArray()",
            FacetConstants.CollectionWrappers.IEnumerable => "",
            _ => ".ToList()"
        };
    }

    /// <summary>
    /// Returns a <c>Expression.Bind(...)</c> call string for the given scalar member,
    /// or null if the member cannot be expressed as a simple property access.
    /// </summary>
    private static string? GetMemberBindingExpression(
        FacetMember member,
        string paramName,
        string targetTypeName)
    {
        // Skip members with computed expressions — user must declare them in ConfigureProjection
        if (member.MapFromSource != null && ExpressionHelper.IsExpression(member.MapFromSource))
            return null;

        string valueExpr;
        if (member.MapFromSource != null)
        {
            // Build chained Expression.Property calls for dotted paths like "Company.Name"
            var parts = member.MapFromSource.Split('.');
            valueExpr = $"global::System.Linq.Expressions.Expression.Property({paramName}, \"{parts[0]}\")";
            for (int i = 1; i < parts.Length; i++)
                valueExpr = $"global::System.Linq.Expressions.Expression.Property({valueExpr}, \"{parts[i]}\")";
        }
        else
        {
            valueExpr = $"global::System.Linq.Expressions.Expression.Property({paramName}, \"{member.SourcePropertyName}\")";
        }

        return $"global::System.Linq.Expressions.Expression.Bind(typeof({targetTypeName}).GetProperty(\"{member.Name}\")!, {valueExpr})";
    }

    private static void GenerateProjectionNotSupportedComment(StringBuilder sb, FacetTargetModel model, string memberIndent)
    {
        // For records with existing primary constructors, the projection can't use the standard constructor approach
        sb.AppendLine($"{memberIndent}// Note: Projection generation is not supported for records with existing primary constructors.");
        sb.AppendLine($"{memberIndent}// You must manually create projection expressions or use the FromSource factory method.");
        sb.AppendLine($"{memberIndent}// Example: source => new {model.Name}(defaultPrimaryConstructorValue) {{ PropA = source.PropA, PropB = source.PropB }}");
    }


    private static void GenerateProjectionDocumentation(StringBuilder sb, FacetTargetModel model, string memberIndent, string propertyName = "Projection")
    {
        // Generate projection XML documentation
        sb.AppendLine($"{memberIndent}/// <summary>");
        sb.AppendLine($"{memberIndent}/// Gets the projection expression for converting <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/> to <see cref=\"{model.Name}\"/>.");
        sb.AppendLine($"{memberIndent}/// Use this for LINQ and Entity Framework query projections.");
        sb.AppendLine($"{memberIndent}/// </summary>");
        sb.AppendLine($"{memberIndent}/// <value>An expression tree that can be used in LINQ queries for efficient database projections.</value>");
        sb.AppendLine($"{memberIndent}/// <example>");
        sb.AppendLine($"{memberIndent}/// <code>");
        sb.AppendLine($"{memberIndent}/// var dtos = context.{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}s");
        sb.AppendLine($"{memberIndent}///     .Where(x => x.IsActive)");
        sb.AppendLine($"{memberIndent}///     .Select({model.Name}.{propertyName})");
        sb.AppendLine($"{memberIndent}///     .ToList();");
        sb.AppendLine($"{memberIndent}/// </code>");
        sb.AppendLine($"{memberIndent}/// </example>");
    }

    /// <summary>
    /// Generates the projection expression body using object initializer syntax for EF Core compatibility.
    /// This allows EF Core to automatically include navigation properties without requiring explicit .Include() calls.
    /// For positional records without a parameterless constructor, uses constructor invocation syntax instead.
    /// </summary>
    private static void GenerateProjectionExpression(
        StringBuilder sb,
        FacetTargetModel model,
        string baseIndent,
        Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        var indent = baseIndent + "    ";
        
        // Check if this is a positional record without a parameterless constructor
        // In this case, we need to use constructor syntax instead of object initializer
        var isPositionalWithoutParameterless = model.IsRecord && 
                                                !model.HasExistingPrimaryConstructor && 
                                                !model.GenerateParameterlessConstructor;
        
        if (isPositionalWithoutParameterless)
        {
            // Use constructor invocation syntax for positional records
            GeneratePositionalRecordProjection(sb, model, indent, facetLookup);
        }
        else
        {
            // Use object initializer syntax (standard approach)
            GenerateObjectInitializerProjection(sb, model, indent, facetLookup);
        }
    }

    /// <summary>
    /// Generates projection using constructor invocation syntax for positional records.
    /// </summary>
    private static void GeneratePositionalRecordProjection(
        StringBuilder sb,
        FacetTargetModel model,
        string indent,
        Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        var visitedTypes = new HashSet<string> { model.Name };
        var includedMembers = model.Members.Where(m => m.MapFromIncludeInProjection).ToArray();
        
        sb.Append($"{indent}source => new {model.Name}(");
        
        for (int i = 0; i < includedMembers.Length; i++)
        {
            var member = includedMembers[i];
            var projectionValue = GetProjectionValueExpression(member, "source", indent, facetLookup, visitedTypes, 0, model.MaxDepth);
            sb.Append(projectionValue);
            
            if (i < includedMembers.Length - 1)
                sb.Append(", ");
        }
        
        sb.AppendLine(");");
    }

    /// <summary>
    /// Generates projection using object initializer syntax (standard approach).
    /// </summary>
    private static void GenerateObjectInitializerProjection(
        StringBuilder sb,
        FacetTargetModel model,
        string indent,
        Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        sb.AppendLine($"{indent}source => new {model.Name}");
        sb.AppendLine($"{indent}{{");

        var members = model.Members;

        // Track which facet types we're currently processing to detect circular references
        var visitedTypes = new HashSet<string> { model.Name };

        // Pre-filter included members to avoid O(n²) comma placement check
        var includedMembers = members.Where(m => m.MapFromIncludeInProjection).ToArray();
        var includedCount = includedMembers.Length;

        for (int i = 0; i < includedCount; i++)
        {
            var member = includedMembers[i];
            var memberIndent = indent + "    ";

            // Generate the property assignment
            var projectionValue = GetProjectionValueExpression(member, "source", memberIndent, facetLookup, visitedTypes, 0, model.MaxDepth);
            sb.Append($"{memberIndent}{member.Name} = {projectionValue}");

            // Add comma if not the last member
            if (i < includedCount - 1)
                sb.Append(",");
            sb.AppendLine();
        }

        sb.AppendLine($"{indent}}};");
    }

    /// <summary>
    /// Gets the projection expression for a member that's compatible with EF Core query translation.
    /// For nested facets, generates nested object initializers instead of constructor calls.
    /// </summary>
    private static string GetProjectionValueExpression(
        FacetMember member,
        string sourceVariableName,
        string indent,
        Dictionary<string, List<FacetTargetModel>> facetLookup,
        HashSet<string> visitedTypes,
        int currentDepth = 0,
        int maxDepth = 0)
    {
        // Check if the member type is nullable
        bool isNullable = member.TypeName.Contains("?");

        if (member.IsNestedFacet && member.IsCollection)
        {
            return BuildCollectionProjection(member, sourceVariableName, isNullable, facetLookup, visitedTypes, currentDepth, maxDepth);
        }
        else if (member.IsNestedFacet)
        {
            return BuildSingleNestedProjection(member, sourceVariableName, isNullable, indent, facetLookup, visitedTypes, currentDepth, maxDepth);
        }

        // Check if this is a MapFrom expression (contains operators or spaces)
        string valueExpression;
        if (member.MapFromSource != null && IsExpression(member.MapFromSource))
        {
            valueExpression = TransformExpression(member.MapFromSource, sourceVariableName);
        }
        else if (member.MapFromSource != null)
        {
            // Use the full MapFromSource path for nested property paths (e.g., "Company.Address")
            valueExpression = $"{sourceVariableName}.{member.MapFromSource}";
        }
        else
        {
            // Regular property - direct assignment using SourcePropertyName
            valueExpression = $"{sourceVariableName}.{member.SourcePropertyName}";
        }

        // Apply enum conversion if this member was converted from an enum type
        if (member.IsEnumConversion && member.OriginalEnumTypeName != null)
        {
            valueExpression = ApplyEnumProjectionConversion(valueExpression, member);
        }

        // Apply MapWhen conditions if present and IncludeInProjection is true
        if (member.MapWhenConditions.Count > 0 && member.MapWhenIncludeInProjection)
        {
            valueExpression = WrapWithMapWhenCondition(member, valueExpression, sourceVariableName);
        }

        return valueExpression;
    }

    private static string BuildCollectionProjection(
        FacetMember member,
        string sourceVariableName,
        bool isNullable,
        Dictionary<string, List<FacetTargetModel>> facetLookup,
        HashSet<string> visitedTypes,
        int currentDepth,
        int maxDepth)
    {
        // Check if we've reached max depth during code generation
        // Note: maxDepth of 0 means unlimited
        if (maxDepth > 0 && currentDepth + 1 > maxDepth)
        {
            return "null";
        }

        // Use SourcePropertyName for accessing the source property (supports MapFrom)
        var sourcePropName = member.SourcePropertyName;

        // For collection nested facets, use Select with nested projection
        var elementTypeName = ExpressionBuilder.ExtractElementTypeFromCollectionTypeName(member.TypeName);
        var nonNullableElementType = elementTypeName.TrimEnd('?');

        var collectionProjection = GenerateNestedCollectionProjection(
            $"{sourceVariableName}.{sourcePropName}",
            nonNullableElementType,
            member.NestedFacetSourceTypeName!,
            member.CollectionWrapper!,
            facetLookup,
            visitedTypes,
            currentDepth + 1,
            maxDepth);

        if (isNullable)
        {
            return $"{sourceVariableName}.{sourcePropName} != null ? {collectionProjection} : null";
        }

        return collectionProjection;
    }

    private static string BuildSingleNestedProjection(
        FacetMember member,
        string sourceVariableName,
        bool isNullable,
        string indent,
        Dictionary<string, List<FacetTargetModel>> facetLookup,
        HashSet<string> visitedTypes,
        int currentDepth,
        int maxDepth)
    {
        // Check if we've reached max depth during code generation
        // Note: maxDepth of 0 means unlimited
        if (maxDepth > 0 && currentDepth + 1 > maxDepth)
        {
            return "null";
        }

        // Use SourcePropertyName for accessing the source property (supports MapFrom)
        var sourcePropName = member.SourcePropertyName;

        // For single nested facets, inline expand the nested facet's members
        var nonNullableTypeName = member.TypeName.TrimEnd('?');
        var nestedSourceExpression = $"{sourceVariableName}.{sourcePropName}";

        // Extract simple type name for circular reference check
        var simpleTypeName = nonNullableTypeName.Replace(Shared.GeneratorUtilities.GlobalPrefix, "").Split('.', ':').Last();

        // Check for circular reference - if we're already processing this type, use constructor
        if (visitedTypes.Contains(simpleTypeName))
        {
            // Circular reference detected - use constructor call to prevent infinite expansion
            var nestedProjection = $"new {nonNullableTypeName}({nestedSourceExpression})";

            if (isNullable)
            {
                return $"{nestedSourceExpression} != null ? {nestedProjection} : null";
            }
            return nestedProjection;
        }

        // Try to look up the nested facet model
        var nestedFacetModel = FindNestedFacetModel(nonNullableTypeName, facetLookup);

        string nestedProjectionResult;
        if (nestedFacetModel != null)
        {
            // Add this type to visited set before recursing
            visitedTypes.Add(simpleTypeName);
            try
            {
                // Recursively inline the nested facet's members
                nestedProjectionResult = GenerateInlineNestedFacetInitializer(
                    nestedFacetModel,
                    nestedSourceExpression,
                    nonNullableTypeName,
                    indent,
                    facetLookup,
                    visitedTypes,
                    currentDepth + 1,
                    maxDepth);
            }
            finally
            {
                // Remove from visited set after recursion completes
                visitedTypes.Remove(simpleTypeName);
            }
        }
        else
        {
            // Fallback to constructor call if we can't find the nested facet model
            nestedProjectionResult = $"new {nonNullableTypeName}({nestedSourceExpression})";
        }

        if (isNullable)
        {
            return $"{nestedSourceExpression} != null ? {nestedProjectionResult} : null";
        }

        return nestedProjectionResult;
    }

    /// <summary>
    /// Generates an inline object initializer for a nested facet, recursively expanding all members.
    /// </summary>
    private static string GenerateInlineNestedFacetInitializer(
        FacetTargetModel nestedFacetModel,
        string sourceExpression,
        string facetTypeName,
        string indent,
        Dictionary<string, List<FacetTargetModel>> facetLookup,
        HashSet<string> visitedTypes,
        int currentDepth = 0,
        int maxDepth = 0)
    {
        var sb = new StringBuilder();
        sb.Append($"new {facetTypeName} {{ ");

        var members = nestedFacetModel.Members;
        for (int i = 0; i < members.Length; i++)
        {
            var member = members[i];
            var projectionValue = GetProjectionValueExpression(member, sourceExpression, indent, facetLookup, visitedTypes, currentDepth, maxDepth);
            sb.Append($"{member.Name} = {projectionValue}");

            if (i < members.Length - 1)
            {
                sb.Append(", ");
            }
        }

        sb.Append(" }");
        return sb.ToString();
    }

    /// <summary>
    /// Generates a collection projection expression for nested facets.
    /// </summary>
    private static string GenerateNestedCollectionProjection(
        string sourceCollectionExpression,
        string elementFacetTypeName,
        string elementSourceTypeName,
        string collectionWrapper,
        Dictionary<string, List<FacetTargetModel>> facetLookup,
        HashSet<string> visitedTypes,
        int currentDepth = 0,
        int maxDepth = 0)
    {
        // Extract simple type name for circular reference check
        var simpleTypeName = elementFacetTypeName.Replace(Shared.GeneratorUtilities.GlobalPrefix, "").Split('.', ':').Last();

        // Check for circular reference
        if (visitedTypes.Contains(simpleTypeName))
        {
            // Circular reference detected - use constructor call
            var circularProjection = $"{sourceCollectionExpression}.Select(x => new {elementFacetTypeName}(x))";
            return collectionWrapper switch
            {
                FacetConstants.CollectionWrappers.Array => $"{circularProjection}.ToArray()",
                FacetConstants.CollectionWrappers.IEnumerable => circularProjection,
                FacetConstants.CollectionWrappers.Collection =>
                    $"new global::System.Collections.ObjectModel.Collection<{elementFacetTypeName}>({circularProjection}.ToList())",
                FacetConstants.CollectionWrappers.ImmutableArray => $"{circularProjection}.ToImmutableArray()",
                FacetConstants.CollectionWrappers.ImmutableList => $"{circularProjection}.ToImmutableList()",
                FacetConstants.CollectionWrappers.ImmutableHashSet => $"{circularProjection}.ToImmutableHashSet()",
                FacetConstants.CollectionWrappers.ImmutableSortedSet => $"{circularProjection}.ToImmutableSortedSet()",
                FacetConstants.CollectionWrappers.ImmutableQueue => $"global::System.Collections.Immutable.ImmutableQueue.CreateRange({circularProjection})",
                FacetConstants.CollectionWrappers.ImmutableStack => $"global::System.Collections.Immutable.ImmutableStack.CreateRange({circularProjection})",
                FacetConstants.CollectionWrappers.IImmutableList => $"{circularProjection}.ToImmutableList()",
                FacetConstants.CollectionWrappers.IImmutableSet => $"{circularProjection}.ToImmutableHashSet()",
                FacetConstants.CollectionWrappers.IImmutableQueue => $"global::System.Collections.Immutable.ImmutableQueue.CreateRange({circularProjection})",
                FacetConstants.CollectionWrappers.IImmutableStack => $"global::System.Collections.Immutable.ImmutableStack.CreateRange({circularProjection})",
                _ => $"{circularProjection}.ToList()"
            };
        }

        // Try to find the nested facet model to inline expand it
        var nestedFacetModel = FindNestedFacetModel(elementFacetTypeName, facetLookup);

        string projection;
        if (nestedFacetModel != null)
        {
            // Add this type to visited set before recursing
            visitedTypes.Add(simpleTypeName);
            try
            {
                // Inline expand the nested facet
                var inlineInitializer = GenerateInlineNestedFacetInitializer(
                    nestedFacetModel, "x", elementFacetTypeName, "", facetLookup, visitedTypes, currentDepth, maxDepth);
                projection = $"{sourceCollectionExpression}.Select(x => {inlineInitializer})";
            }
            finally
            {
                // Remove from visited set after recursion completes
                visitedTypes.Remove(simpleTypeName);
            }
        }
        else
        {
            // Fallback to constructor call
            projection = $"{sourceCollectionExpression}.Select(x => new {elementFacetTypeName}(x))";
        }

        return collectionWrapper switch
        {
            FacetConstants.CollectionWrappers.Array => $"{projection}.ToArray()",
            FacetConstants.CollectionWrappers.IEnumerable => projection,
            FacetConstants.CollectionWrappers.Collection =>
                $"new global::System.Collections.ObjectModel.Collection<{elementFacetTypeName}>({projection}.ToList())",
            FacetConstants.CollectionWrappers.ImmutableArray => $"{projection}.ToImmutableArray()",
            FacetConstants.CollectionWrappers.ImmutableList => $"{projection}.ToImmutableList()",
            FacetConstants.CollectionWrappers.ImmutableHashSet => $"{projection}.ToImmutableHashSet()",
            FacetConstants.CollectionWrappers.ImmutableSortedSet => $"{projection}.ToImmutableSortedSet()",
            FacetConstants.CollectionWrappers.ImmutableQueue => $"global::System.Collections.Immutable.ImmutableQueue.CreateRange({projection})",
            FacetConstants.CollectionWrappers.ImmutableStack => $"global::System.Collections.Immutable.ImmutableStack.CreateRange({projection})",
            FacetConstants.CollectionWrappers.IImmutableList => $"{projection}.ToImmutableList()",
            FacetConstants.CollectionWrappers.IImmutableSet => $"{projection}.ToImmutableHashSet()",
            FacetConstants.CollectionWrappers.IImmutableQueue => $"global::System.Collections.Immutable.ImmutableQueue.CreateRange({projection})",
            FacetConstants.CollectionWrappers.IImmutableStack => $"global::System.Collections.Immutable.ImmutableStack.CreateRange({projection})",
            _ => $"{projection}.ToList()"
        };
    }

    private static FacetTargetModel? FindNestedFacetModel(string typeName, Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        // Strip "global::" prefix and extract simple name
        var lookupName = typeName
            .Replace(Shared.GeneratorUtilities.GlobalPrefix, "")
            .Split('.', ':')
            .Last();

        // First try exact match with the lookup name
        if (facetLookup.TryGetValue(lookupName, out var nestedFacetModels) && nestedFacetModels.Count > 0)
        {
            // For projection purposes, the first model is sufficient since all models with the same
            // FullName share the same structure (union of all members)
            return nestedFacetModels[0];
        }

        // Try matching by simple name or full name
        foreach (var kvp in facetLookup)
        {
            if (kvp.Value.Count > 0)
            {
                var model = kvp.Value[0];
                if (kvp.Key == lookupName ||
                    model.Name == lookupName ||
                    kvp.Key.EndsWith("." + lookupName))
                {
                    return model;
                }
            }
        }

        return null;
    }

    // Expression parsing methods delegated to shared ExpressionHelper
    private static bool IsExpression(string source) => ExpressionHelper.IsExpression(source);
    private static string TransformExpression(string expression, string sourceVariableName) => ExpressionHelper.TransformExpression(expression, sourceVariableName);

    /// <summary>
    /// Applies enum-to-target-type conversion for projection expressions (EF Core compatible).
    /// Uses expression-tree-compatible patterns.
    /// </summary>
    private static string ApplyEnumProjectionConversion(string valueExpression, FacetMember member)
    {
        bool isNullableEnum = member.SourceMemberTypeName?.Contains("?") ?? false;

        if (member.TypeName.TrimEnd('?') == "string")
        {
            // For EF Core projections, .ToString() on enums translates to SQL
            if (isNullableEnum)
            {
                return $"{valueExpression} != null ? {valueExpression}.Value.ToString() : null";
            }
            return $"{valueExpression}.ToString()";
        }
        else if (member.TypeName.TrimEnd('?') == "int")
        {
            // Cast enum to int - EF Core supports this in projections
            if (isNullableEnum)
            {
                return $"(int?){valueExpression}";
            }
            return $"(int){valueExpression}";
        }

        return valueExpression;
    }

    /// <summary>
    /// Wraps a value expression with MapWhen condition(s), generating a ternary expression.
    /// </summary>
    private static string WrapWithMapWhenCondition(FacetMember member, string valueExpression, string sourceVariableName)
    {
        // Combine multiple conditions with &&
        var combinedCondition = string.Join(" && ", member.MapWhenConditions.Select(c =>
            $"({TransformExpression(c, sourceVariableName)})"));

        // Determine the default value
        var defaultValue = member.MapWhenDefault ?? Shared.GeneratorUtilities.GetDefaultValueForType(member.TypeName);

        return $"{combinedCondition} ? {valueExpression} : {defaultValue}";
    }

}
