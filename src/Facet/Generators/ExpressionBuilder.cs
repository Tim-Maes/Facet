namespace Facet.Generators;

/// <summary>
/// Builds C# expressions for mapping between source and facet types.
/// Handles nested facets, collections, nullability, and depth tracking for circular reference prevention.
/// </summary>
internal static class ExpressionBuilder
{
    /// <summary>
    /// Gets the appropriate source value expression for a member (forward mapping: source to facet).
    /// For nested facets, returns "new NestedFacetType(source.PropertyName)" with null checks if nullable.
    /// For collection nested facets, returns "source.PropertyName.Select(x => new NestedFacetType(x)).ToList()" with null checks if nullable.
    /// For regular members, returns "source.PropertyName".
    /// </summary>
    public static string GetSourceValueExpression(
        FacetMember member,
        string sourceVariableName,
        int maxDepth = 0,
        bool useDepthParameter = false,
        bool preserveReferences = false)
    {
        bool isNullable = member.TypeName.Contains("?");

        if (member.IsNestedFacet && member.IsCollection)
        {
            return BuildCollectionNestedFacetExpression(
                member, sourceVariableName, isNullable, maxDepth, useDepthParameter, preserveReferences);
        }
        else if (member.IsNestedFacet)
        {
            return BuildSingleNestedFacetExpression(
                member, sourceVariableName, isNullable, maxDepth, useDepthParameter, preserveReferences);
        }

        return $"{sourceVariableName}.{member.Name}";
    }

    /// <summary>
    /// Gets the appropriate value expression for mapping back to the source type (backward mapping: facet to source).
    /// For child facets, returns "this.PropertyName.BackTo()" with null checks if nullable.
    /// For collection child facets, returns "this.PropertyName.Select(x => x.BackTo()).ToList()" with null checks if nullable.
    /// For regular members, returns "this.PropertyName" with nullable-to-non-nullable conversion if needed.
    /// </summary>
    public static string GetBackToValueExpression(FacetMember member)
    {
        // Check if the member type is nullable (ends with ?)
        bool facetTypeIsNullable = member.TypeName.Contains("?");

        // Check if the source type is nullable
        bool sourceTypeIsNullable = member.SourceMemberTypeName?.Contains("?") ?? facetTypeIsNullable;

        if (member.IsNestedFacet && member.IsCollection)
        {
            return BuildCollectionBackToExpression(member, facetTypeIsNullable);
        }
        else if (member.IsNestedFacet)
        {
            return BuildSingleBackToExpression(member, facetTypeIsNullable);
        }

        // For regular properties/fields:
        // If the facet type is nullable but the source type is not, we need to unwrap the nullable
        if (facetTypeIsNullable && !sourceTypeIsNullable)
        {
            // Use null-coalescing with default value for value types, or null-forgiving for reference types
            if (member.IsValueType)
            {
                // For value types like int?, use: this.Property ?? default
                return $"this.{member.Name} ?? default";
            }
            else
            {
                // For reference types, use null-forgiving operator since the source expects non-null
                return $"this.{member.Name}!";
            }
        }

        return $"this.{member.Name}";
    }

    /// <summary>
    /// Extracts the element type name from a collection type name.
    /// For example: "global::System.Collections.Generic.List<global::MyNamespace.MyType>" => "global::MyNamespace.MyType"
    /// Also handles nullable collections: "List<MyType>?" => "MyType"
    /// </summary>
    public static string ExtractElementTypeFromCollectionTypeName(string collectionTypeName)
    {
        // Strip nullable marker from collection first (e.g., "List<T>?" => "List<T>")
        var nonNullableCollectionType = collectionTypeName.TrimEnd('?');

        // Handle array syntax (e.g., "MyType[]" => "MyType")
        if (nonNullableCollectionType.EndsWith("[]"))
        {
            var elementType = nonNullableCollectionType.Substring(0, nonNullableCollectionType.Length - 2);
            // Remove any trailing nullable marker from the element type itself
            return elementType.TrimEnd('?');
        }

        // Handle generic collection syntax (e.g., "List<MyType>" => "MyType")
        var startIndex = nonNullableCollectionType.IndexOf('<');
        var endIndex = nonNullableCollectionType.LastIndexOf('>');

        if (startIndex > 0 && endIndex > startIndex)
        {
            var elementType = nonNullableCollectionType.Substring(startIndex + 1, endIndex - startIndex - 1);
            // Remove any trailing nullable marker from the element type itself
            return elementType.TrimEnd('?');
        }

        return nonNullableCollectionType.TrimEnd('?');
    }

    #region Private Helper Methods

    private static string BuildCollectionNestedFacetExpression(
        FacetMember member,
        string sourceVariableName,
        bool isNullable,
        int maxDepth,
        bool useDepthParameter,
        bool preserveReferences)
    {
        var elementTypeName = ExtractElementTypeFromCollectionTypeName(member.TypeName);
        
        var sourceElementTypeName = member.NestedFacetSourceTypeName ??
            (member.SourceMemberTypeName != null
                ? ExtractElementTypeFromCollectionTypeName(member.SourceMemberTypeName)
                : elementTypeName);

        // Check if we should stop due to max depth
        if (useDepthParameter && maxDepth > 0)
        {
            var updatedProcessed = preserveReferences
                ? $"(__processed != null ? new System.Collections.Generic.HashSet<object>(__processed, System.Collections.Generic.ReferenceEqualityComparer.Instance) {{ {sourceVariableName} }} : new System.Collections.Generic.HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance) {{ {sourceVariableName} }})"
                : "__processed";

            var sourceCollection = preserveReferences
                ? $"{sourceVariableName}.{member.Name}.Distinct(System.Collections.Generic.ReferenceEqualityComparer.Instance).Cast<{sourceElementTypeName}>()"
                : $"{sourceVariableName}.{member.Name}";

            var projection = preserveReferences
                ? $"{sourceCollection}.Select(x => __processed != null && __processed.Contains(x) ? null : new {elementTypeName}(x, __depth + 1, {updatedProcessed})).Where(x => x != null)"
                : $"{sourceCollection}.Select(x => new {elementTypeName}(x, __depth + 1, {updatedProcessed}))";

            // Convert back to the appropriate collection type
            var collectionExpression = WrapCollectionProjection(projection, member.CollectionWrapper!);

            if (isNullable)
            {
                return $"__depth < {maxDepth} && {sourceVariableName}.{member.Name} != null ? {collectionExpression} : null";
            }

            return $"__depth < {maxDepth} ? {collectionExpression} : null";
        }
        else
        {
            var updatedProcessed = preserveReferences && useDepthParameter
                ? $"(__processed != null ? new System.Collections.Generic.HashSet<object>(__processed, System.Collections.Generic.ReferenceEqualityComparer.Instance) {{ {sourceVariableName} }} : new System.Collections.Generic.HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance) {{ {sourceVariableName} }})"
                : "__processed";

            var sourceCollection = preserveReferences && useDepthParameter
                ? $"{sourceVariableName}.{member.Name}.Distinct(System.Collections.Generic.ReferenceEqualityComparer.Instance).Cast<{sourceElementTypeName}>()"
                : $"{sourceVariableName}.{member.Name}";

            var projection = useDepthParameter
                ? (preserveReferences
                    ? $"{sourceCollection}.Select(x => __processed != null && __processed.Contains(x) ? null : new {elementTypeName}(x, __depth + 1, {updatedProcessed})).Where(x => x != null)"
                    : $"{sourceCollection}.Select(x => new {elementTypeName}(x, __depth + 1, {updatedProcessed}))")
                : $"{sourceCollection}.Select(x => new {elementTypeName}(x))";

            // Convert back to the appropriate collection type
            var collectionExpression = WrapCollectionProjection(projection, member.CollectionWrapper!);

            if (isNullable)
            {
                return $"{sourceVariableName}.{member.Name} != null ? {collectionExpression} : null";
            }

            return collectionExpression;
        }
    }

    private static string BuildSingleNestedFacetExpression(
        FacetMember member,
        string sourceVariableName,
        bool isNullable,
        int maxDepth,
        bool useDepthParameter,
        bool preserveReferences)
    {
        var nonNullableTypeName = member.TypeName.TrimEnd('?');

        // Build the constructor call with reference checking if needed
        string BuildConstructorCall(string sourceExpr)
        {
            var updatedProcessed = preserveReferences && useDepthParameter
                ? $"(__processed != null ? new System.Collections.Generic.HashSet<object>(__processed, System.Collections.Generic.ReferenceEqualityComparer.Instance) {{ {sourceVariableName} }} : new System.Collections.Generic.HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance) {{ {sourceVariableName} }})"
                : "__processed";

            var ctorCall = useDepthParameter
                ? $"new {nonNullableTypeName}({sourceExpr}, __depth + 1, {updatedProcessed})"
                : $"new {nonNullableTypeName}({sourceExpr})";

            if (preserveReferences && useDepthParameter)
            {
                // Check against __processed (not updatedProcessed) to detect if this exact object was already processed
                return $"(__processed != null && __processed.Contains({sourceExpr}) ? null : {ctorCall})";
            }

            return ctorCall;
        }

        // Check if we should stop due to max depth
        if (useDepthParameter && maxDepth > 0)
        {
            var constructorCall = BuildConstructorCall($"{sourceVariableName}.{member.Name}");

            if (isNullable)
            {
                return $"__depth < {maxDepth} && {sourceVariableName}.{member.Name} != null ? {constructorCall} : null";
            }

            return $"__depth < {maxDepth} ? {constructorCall} : null";
        }
        else
        {
            var constructorCall = BuildConstructorCall($"{sourceVariableName}.{member.Name}");

            if (isNullable)
            {
                return $"{sourceVariableName}.{member.Name} != null ? {constructorCall} : null";
            }

            // Use the nested facet's generated constructor
            return constructorCall;
        }
    }

    private static string BuildCollectionBackToExpression(FacetMember member, bool facetTypeIsNullable)
    {
        // Use LINQ Select to map each element back
        var projection = $"this.{member.Name}.Select(x => x.BackTo())";

        // Convert back to the appropriate collection type
        var collectionExpression = WrapCollectionProjection(projection, member.CollectionWrapper!);

        // Add null check for nullable collections
        if (facetTypeIsNullable)
        {
            return $"this.{member.Name} != null ? {collectionExpression} : null";
        }

        return collectionExpression;
    }

    private static string BuildSingleBackToExpression(FacetMember member, bool facetTypeIsNullable)
    {
        // Add null check for nullable nested facets
        if (facetTypeIsNullable)
        {
            return $"this.{member.Name} != null ? this.{member.Name}.BackTo() : null";
        }

        // Use the child facet's generated BackTo method
        return $"this.{member.Name}.BackTo()";
    }

    private static string WrapCollectionProjection(string projection, string collectionWrapper)
    {
        return collectionWrapper switch
        {
            FacetConstants.CollectionWrappers.List => $"{projection}.ToList()",
            FacetConstants.CollectionWrappers.IList => $"{projection}.ToList()",
            FacetConstants.CollectionWrappers.ICollection => $"{projection}.ToList()",
            FacetConstants.CollectionWrappers.IEnumerable => projection,
            FacetConstants.CollectionWrappers.Array => $"{projection}.ToArray()",
            _ => projection
        };
    }

    #endregion
}
