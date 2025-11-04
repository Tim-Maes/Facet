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
    public static void GenerateProjectionProperty(
        StringBuilder sb,
        FacetTargetModel model,
        string memberIndent,
        Dictionary<string, FacetTargetModel> facetLookup)
    {
        sb.AppendLine();

        if (model.HasExistingPrimaryConstructor && model.IsRecord)
        {
            GenerateProjectionNotSupportedComment(sb, model, memberIndent);
        }
        else
        {
            GenerateProjectionDocumentation(sb, model, memberIndent);
            sb.AppendLine($"{memberIndent}public static Expression<Func<{model.SourceTypeName}, {model.Name}>> Projection =>");

            // Generate object initializer projection for EF Core compatibility
            GenerateProjectionExpression(sb, model, memberIndent, facetLookup);
        }
    }

    private static void GenerateProjectionNotSupportedComment(StringBuilder sb, FacetTargetModel model, string memberIndent)
    {
        // For records with existing primary constructors, the projection can't use the standard constructor approach
        sb.AppendLine($"{memberIndent}// Note: Projection generation is not supported for records with existing primary constructors.");
        sb.AppendLine($"{memberIndent}// You must manually create projection expressions or use the FromSource factory method.");
        sb.AppendLine($"{memberIndent}// Example: source => new {model.Name}(defaultPrimaryConstructorValue) {{ PropA = source.PropA, PropB = source.PropB }}");
    }


    private static void GenerateProjectionDocumentation(StringBuilder sb, FacetTargetModel model, string memberIndent)
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
        sb.AppendLine($"{memberIndent}///     .Select({model.Name}.Projection)");
        sb.AppendLine($"{memberIndent}///     .ToList();");
        sb.AppendLine($"{memberIndent}/// </code>");
        sb.AppendLine($"{memberIndent}/// </example>");
    }

    /// <summary>
    /// Generates the projection expression body using object initializer syntax for EF Core compatibility.
    /// This allows EF Core to automatically include navigation properties without requiring explicit .Include() calls.
    /// </summary>
    private static void GenerateProjectionExpression(
        StringBuilder sb,
        FacetTargetModel model,
        string baseIndent,
        Dictionary<string, FacetTargetModel> facetLookup)
    {
        var indent = baseIndent + "    ";
        sb.AppendLine($"{indent}source => new {model.Name}");
        sb.AppendLine($"{indent}{{");

        var members = model.Members;
        var memberCount = members.Length;

        // Track which facet types we're currently processing to detect circular references
        var visitedTypes = new HashSet<string> { model.Name };

        for (int i = 0; i < memberCount; i++)
        {
            var member = members[i];
            var comma = i < memberCount - 1 ? "," : "";
            var memberIndent = indent + "    ";

            // Generate the property assignment
            var projectionValue = GetProjectionValueExpression(member, "source", memberIndent, facetLookup, visitedTypes, 0, model.MaxDepth);
            sb.Append($"{memberIndent}{member.Name} = {projectionValue}{comma}");

            // Add newline
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
        Dictionary<string, FacetTargetModel> facetLookup,
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

        // Regular property - direct assignment
        return $"{sourceVariableName}.{member.Name}";
    }

    private static string BuildCollectionProjection(
        FacetMember member,
        string sourceVariableName,
        bool isNullable,
        Dictionary<string, FacetTargetModel> facetLookup,
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

        // For collection nested facets, use Select with nested projection
        var elementTypeName = ExpressionBuilder.ExtractElementTypeFromCollectionTypeName(member.TypeName);
        var nonNullableElementType = elementTypeName.TrimEnd('?');

        var collectionProjection = GenerateNestedCollectionProjection(
            $"{sourceVariableName}.{member.Name}",
            nonNullableElementType,
            member.NestedFacetSourceTypeName!,
            member.CollectionWrapper!,
            facetLookup,
            visitedTypes,
            currentDepth + 1,
            maxDepth);

        if (isNullable)
        {
            return $"{sourceVariableName}.{member.Name} != null ? {collectionProjection} : null";
        }

        return collectionProjection;
    }

    private static string BuildSingleNestedProjection(
        FacetMember member,
        string sourceVariableName,
        bool isNullable,
        string indent,
        Dictionary<string, FacetTargetModel> facetLookup,
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

        // For single nested facets, inline expand the nested facet's members
        var nonNullableTypeName = member.TypeName.TrimEnd('?');
        var nestedSourceExpression = $"{sourceVariableName}.{member.Name}";

        // Extract simple type name for circular reference check
        var simpleTypeName = nonNullableTypeName.Replace("global::", "").Split('.', ':').Last();

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
        Dictionary<string, FacetTargetModel> facetLookup,
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
        Dictionary<string, FacetTargetModel> facetLookup,
        HashSet<string> visitedTypes,
        int currentDepth = 0,
        int maxDepth = 0)
    {
        // Extract simple type name for circular reference check
        var simpleTypeName = elementFacetTypeName.Replace("global::", "").Split('.', ':').Last();

        // Check for circular reference
        if (visitedTypes.Contains(simpleTypeName))
        {
            // Circular reference detected - use constructor call
            var circularProjection = $"{sourceCollectionExpression}.Select(x => new {elementFacetTypeName}(x))";
            return collectionWrapper switch
            {
                FacetConstants.CollectionWrappers.Array => $"{circularProjection}.ToArray()",
                FacetConstants.CollectionWrappers.IEnumerable => circularProjection,
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
            _ => $"{projection}.ToList()"
        };
    }

    private static FacetTargetModel? FindNestedFacetModel(string typeName, Dictionary<string, FacetTargetModel> facetLookup)
    {
        // Strip "global::" prefix and extract simple name
        var lookupName = typeName
            .Replace("global::", "")
            .Split('.', ':')
            .Last();

        // First try exact match with the lookup name
        if (facetLookup.TryGetValue(lookupName, out var nestedFacetModel))
        {
            return nestedFacetModel;
        }

        // Try matching by simple name or full name
        foreach (var kvp in facetLookup)
        {
            if (kvp.Key == lookupName ||
                kvp.Value.Name == lookupName ||
                kvp.Key.EndsWith("." + lookupName))
            {
                return kvp.Value;
            }
        }

        return null;
    }
}
