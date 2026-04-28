using Facet.Generators.Shared;
using System.Collections.Generic;
using System.Linq;

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
            // Use SourcePropertyName for regular properties
            valueExpression = $"{sourceVariableName}.{member.SourcePropertyName}";
        }

        // Apply enum conversion if this member was converted from an enum type
        if (member.IsEnumConversion && member.OriginalEnumTypeName != null)
        {
            valueExpression = ApplyEnumToTargetConversion(valueExpression, member);
        }

        // Apply MapWhen conditions if present
        if (member.MapWhenConditions.Count > 0)
        {
            valueExpression = WrapWithMapWhenCondition(member, valueExpression, sourceVariableName);
        }

        return valueExpression;
    }

    /// <summary>
    /// Gets the appropriate value expression for mapping back to the source type (backward mapping: facet to source).
    /// For child facets, returns "this.PropertyName.ToSource()" with null checks if nullable.
    /// For collection child facets, returns "this.PropertyName.Select(x => x.ToSource()).ToList()" with null checks if nullable.
    /// For regular members, returns "this.PropertyName" with nullable-to-non-nullable conversion if needed.
    /// </summary>
    /// <param name="facetLookup">Dictionary mapping facet type names to their model lists (for resolving multi-source nested facets).</param>
    /// <param name="parentSourceTypeName">The source type name of the parent facet (used to determine which ToSource method to call for multi-source nested facets).</param>
    public static string GetToSourceValueExpression(FacetMember member, Dictionary<string, List<FacetTargetModel>>? facetLookup = null, string? parentSourceTypeName = null)
    {
        // Check if the member type is nullable (ends with ?)
        bool facetTypeIsNullable = member.TypeName.Contains("?");

        // Check if the source type is nullable
        bool sourceTypeIsNullable = member.SourceMemberTypeName?.Contains("?") ?? facetTypeIsNullable;

        if (member.IsNestedFacet && member.IsCollection)
        {
            return BuildCollectionToSourceExpression(member, facetTypeIsNullable, facetLookup, parentSourceTypeName);
        }
        else if (member.IsNestedFacet)
        {
            return BuildSingleToSourceExpression(member, facetTypeIsNullable, facetLookup, parentSourceTypeName);
        }

        // Handle enum conversion (reverse: string/int back to enum)
        if (member.IsEnumConversion && member.OriginalEnumTypeName != null)
        {
            return ApplyTargetToEnumConversion(member);
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

        // Use SourcePropertyName for accessing the source property (supports MapFrom)
        var sourcePropName = member.SourcePropertyName;

        // Check if we should stop due to max depth
        if (useDepthParameter && maxDepth > 0)
        {
            var updatedProcessed = preserveReferences
                ? $"(__processed != null ? new System.Collections.Generic.HashSet<object>(__processed, System.Collections.Generic.ReferenceEqualityComparer.Instance) {{ {sourceVariableName} }} : new System.Collections.Generic.HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance) {{ {sourceVariableName} }})"
                : "__processed";

            var sourceCollection = preserveReferences
                ? $"{sourceVariableName}.{sourcePropName}.Distinct(System.Collections.Generic.ReferenceEqualityComparer.Instance).Cast<{sourceElementTypeName}>()"
                : $"{sourceVariableName}.{sourcePropName}";

            var projection = preserveReferences
                ? $"{sourceCollection}.Select(x => __processed != null && __processed.Contains(x) ? null : new {elementTypeName}(x, __depth + 1, {updatedProcessed})).Where(x => x != null).OfType<{elementTypeName}>()"
                : $"{sourceCollection}.Select(x => new {elementTypeName}(x, __depth + 1, {updatedProcessed}))";

            // Convert back to the appropriate collection type
            var collectionExpression = WrapCollectionProjection(projection, member.CollectionWrapper!, elementTypeName);

            if (isNullable)
            {
                return $"__depth < {maxDepth} && {sourceVariableName}.{sourcePropName} != null ? {collectionExpression} : null";
            }

            // For non-nullable collections, add null check with descriptive exception when within depth,
            // and use appropriate default value when depth is exceeded
            var depthExceededDefault = GetCollectionDefaultValue(member.CollectionWrapper!, member.TypeName);
            return $"__depth < {maxDepth} ? ({sourceVariableName}.{sourcePropName} != null ? {collectionExpression} : throw new System.ArgumentNullException(\"{sourcePropName}\", \"Required nested facet collection property '{sourcePropName}' on source type was null. Ensure the source property is populated before mapping.\")) : {depthExceededDefault}";
        }
        else
        {
            var updatedProcessed = preserveReferences && useDepthParameter
                ? $"(__processed != null ? new System.Collections.Generic.HashSet<object>(__processed, System.Collections.Generic.ReferenceEqualityComparer.Instance) {{ {sourceVariableName} }} : new System.Collections.Generic.HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance) {{ {sourceVariableName} }})"
                : "__processed";

            var sourceCollection = preserveReferences && useDepthParameter
                ? $"{sourceVariableName}.{sourcePropName}.Distinct(System.Collections.Generic.ReferenceEqualityComparer.Instance).Cast<{sourceElementTypeName}>()"
                : $"{sourceVariableName}.{sourcePropName}";

            var projection = useDepthParameter
                ? (preserveReferences
                    ? $"{sourceCollection}.Select(x => __processed != null && __processed.Contains(x) ? null : new {elementTypeName}(x, __depth + 1, {updatedProcessed})).Where(x => x != null).OfType<{elementTypeName}>()"
                    : $"{sourceCollection}.Select(x => new {elementTypeName}(x, __depth + 1, {updatedProcessed}))")
                : $"{sourceCollection}.Select(x => new {elementTypeName}(x))";

            // Convert back to the appropriate collection type
            var collectionExpression = WrapCollectionProjection(projection, member.CollectionWrapper!, elementTypeName);

            if (isNullable)
            {
                return $"{sourceVariableName}.{sourcePropName} != null ? {collectionExpression} : null";
            }

            // For non-nullable collections without depth tracking, add null check with descriptive exception
            return $"{sourceVariableName}.{sourcePropName} != null ? {collectionExpression} : throw new System.ArgumentNullException(\"{sourcePropName}\", \"Required nested facet collection property '{sourcePropName}' on source type was null. Ensure the source property is populated before mapping.\")";
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
        // Use SourcePropertyName for accessing the source property (supports MapFrom)
        var sourcePropName = member.SourcePropertyName;

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
                // For non-nullable types, use null-forgiving operator to avoid CS8601 compiler warnings
                var nullFallback = isNullable ? "null" : "null!";
                return $"(__processed != null && __processed.Contains({sourceExpr}) ? {nullFallback} : {ctorCall})";
            }

            return ctorCall;
        }

        // Check if we should stop due to max depth
        if (useDepthParameter && maxDepth > 0)
        {
            var constructorCall = BuildConstructorCall($"{sourceVariableName}.{sourcePropName}");

            if (isNullable)
            {
                return $"__depth < {maxDepth} && {sourceVariableName}.{sourcePropName} != null ? {constructorCall} : null";
            }

            // For non-nullable properties, add null check with descriptive exception
            // This prevents NullReferenceException inside the nested constructor when source property is unexpectedly null
            return $"__depth < {maxDepth} ? ({sourceVariableName}.{sourcePropName} != null ? {constructorCall} : throw new System.ArgumentNullException(\"{sourcePropName}\", \"Required nested facet property '{sourcePropName}' on source type was null. Ensure the source property is populated before mapping.\")) : null!";
        }
        else
        {
            var constructorCall = BuildConstructorCall($"{sourceVariableName}.{sourcePropName}");

            if (isNullable)
            {
                return $"{sourceVariableName}.{sourcePropName} != null ? {constructorCall} : null";
            }

            // For non-nullable properties without depth tracking, add null check with descriptive exception
            return $"{sourceVariableName}.{sourcePropName} != null ? {constructorCall} : throw new System.ArgumentNullException(\"{sourcePropName}\", \"Required nested facet property '{sourcePropName}' on source type was null. Ensure the source property is populated before mapping.\")";
        }
    }

    private static string BuildCollectionToSourceExpression(FacetMember member, bool facetTypeIsNullable, Dictionary<string, List<FacetTargetModel>>? facetLookup, string? parentSourceTypeName)
    {
        // Determine the correct ToSource method name for the nested facet
        var toSourceMethodName = GetToSourceMethodName(member.TypeName, member.NestedFacetSourceTypeName, facetLookup, parentSourceTypeName);

        // Use LINQ Select to map each element back
        var projection = $"this.{member.Name}.Select(x => x.{toSourceMethodName}())";

        // Use the original source collection wrapper (before any CollectionTargetType override)
        // so that the generated expression produces the correct source type.
        var toSourceWrapper = member.SourceCollectionWrapper ?? member.CollectionWrapper!;
        var collectionExpression = WrapCollectionProjection(projection, toSourceWrapper, member.NestedFacetSourceTypeName);

        // Add null check for nullable collections
        if (facetTypeIsNullable)
        {
            return $"this.{member.Name} != null ? {collectionExpression} : null";
        }

        return collectionExpression;
    }

    private static string BuildSingleToSourceExpression(FacetMember member, bool facetTypeIsNullable, Dictionary<string, List<FacetTargetModel>>? facetLookup, string? parentSourceTypeName)
    {
        // Determine the correct ToSource method name for the nested facet
        var toSourceMethodName = GetToSourceMethodName(member.TypeName, member.NestedFacetSourceTypeName, facetLookup, parentSourceTypeName);

        // Add null check for nullable nested facets
        if (facetTypeIsNullable)
        {
            return $"this.{member.Name} != null ? this.{member.Name}.{toSourceMethodName}() : null";
        }

        // Use the child facet's generated ToSource method
        return $"this.{member.Name}.{toSourceMethodName}()";
    }

    private static string WrapCollectionProjection(string projection, string collectionWrapper, string? elementTypeName = null)
    {
        return collectionWrapper switch
        {
            FacetConstants.CollectionWrappers.List => $"{projection}.ToList()",
            FacetConstants.CollectionWrappers.IList => $"{projection}.ToList()",
            FacetConstants.CollectionWrappers.ICollection => $"{projection}.ToList()",
            FacetConstants.CollectionWrappers.IReadOnlyList => $"{projection}.ToList()",
            FacetConstants.CollectionWrappers.IReadOnlyCollection => $"{projection}.ToList()",
            FacetConstants.CollectionWrappers.IEnumerable => projection,
            FacetConstants.CollectionWrappers.Array => $"{projection}.ToArray()",
            FacetConstants.CollectionWrappers.Collection when elementTypeName != null =>
                $"new global::System.Collections.ObjectModel.Collection<{elementTypeName}>({projection}.ToList())",
            FacetConstants.CollectionWrappers.Collection => $"{projection}.ToList()",
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
            _ => projection
        };
    }

    // Expression parsing methods delegated to shared ExpressionHelper
    private static bool IsExpression(string source) => ExpressionHelper.IsExpression(source);
    private static string TransformExpression(string expression, string sourceVariableName) => ExpressionHelper.TransformExpression(expression, sourceVariableName);

    /// <summary>
    /// Applies enum-to-target-type conversion (source enum ? facet string/int).
    /// </summary>
    private static string ApplyEnumToTargetConversion(string valueExpression, FacetMember member)
    {
        // Check if this is a collection of enums
        if (member.IsCollection && member.CollectionWrapper != null)
        {
            return ApplyEnumCollectionToTargetConversion(valueExpression, member);
        }

        // Determine if the source enum property is nullable
        bool isNullableEnum = member.SourceMemberTypeName?.Contains("?") ?? false;

        if (member.TypeName.TrimEnd('?') == "string")
        {
            // Enum to string conversion
            if (isNullableEnum)
            {
                return $"{valueExpression}?.ToString()";
            }
            return $"{valueExpression}.ToString()";
        }
        else if (member.TypeName.TrimEnd('?') == "int")
        {
            // Enum to int conversion
            if (isNullableEnum)
            {
                return $"(int?){valueExpression}";
            }
            return $"(int){valueExpression}";
        }

        return valueExpression;
    }

    /// <summary>
    /// Applies enum collection to target collection conversion (source List&lt;enum&gt; ? facet List&lt;string/int&gt;).
    /// </summary>
    private static string ApplyEnumCollectionToTargetConversion(string valueExpression, FacetMember member)
    {
        // Check if the collection itself is nullable
        bool isCollectionNullable = member.TypeName.Contains("?");
        string targetElementType = member.TypeName.TrimEnd('?');

        // Extract just the element type name from List<string> or List<int>
        if (targetElementType.Contains("<") && targetElementType.Contains(">"))
        {
            int startIdx = targetElementType.IndexOf('<') + 1;
            int endIdx = targetElementType.LastIndexOf('>');
            targetElementType = targetElementType.Substring(startIdx, endIdx - startIdx).Trim();
        }

        string conversionExpression;
        if (targetElementType.TrimEnd('?') == "string")
        {
            // Convert each enum to string using ToString()
            conversionExpression = $"{valueExpression}.Select(x => x.ToString())";
        }
        else if (targetElementType.TrimEnd('?') == "int")
        {
            // Convert each enum to int using cast
            conversionExpression = $"{valueExpression}.Select(x => (int)x)";
        }
        else
        {
            return valueExpression;
        }

        // Wrap in the appropriate collection type
        var finalExpression = WrapCollectionProjection(conversionExpression, member.CollectionWrapper);

        // Apply null check if collection is nullable
        if (isCollectionNullable)
        {
            return $"{valueExpression} != null ? {finalExpression} : null";
        }

        return finalExpression;
    }

    /// <summary>
    /// Applies target-type-to-enum conversion (facet string/int ? source enum) for ToSource mapping.
    /// </summary>
    private static string ApplyTargetToEnumConversion(FacetMember member)
    {
        // Check if this is a collection of enums
        if (member.IsCollection && member.CollectionWrapper != null)
        {
            return ApplyTargetCollectionToEnumConversion(member);
        }

        var enumTypeName = member.OriginalEnumTypeName!;
        bool facetTypeIsNullable = member.TypeName.Contains("?");
        bool sourceTypeIsNullable = member.SourceMemberTypeName?.Contains("?") ?? false;

        if (member.TypeName.TrimEnd('?') == "string")
        {
            // String to enum conversion
            if (facetTypeIsNullable && sourceTypeIsNullable)
            {
                return $"this.{member.Name} != null ? ({enumTypeName}?)System.Enum.Parse<{enumTypeName}>(this.{member.Name}) : null";
            }
            else if (facetTypeIsNullable)
            {
                // Facet is nullable string but source expects non-nullable enum
                return $"System.Enum.Parse<{enumTypeName}>(this.{member.Name}!)";
            }
            else
            {
                return $"System.Enum.Parse<{enumTypeName}>(this.{member.Name})";
            }
        }
        else if (member.TypeName.TrimEnd('?') == "int")
        {
            // Int to enum conversion
            if (facetTypeIsNullable && sourceTypeIsNullable)
            {
                return $"this.{member.Name} != null ? ({enumTypeName}?)({enumTypeName})this.{member.Name}.Value : null";
            }
            else if (facetTypeIsNullable)
            {
                // Facet is nullable int but source expects non-nullable enum
                return $"({enumTypeName})(this.{member.Name} ?? default)";
            }
            else
            {
                return $"({enumTypeName})this.{member.Name}";
            }
        }

        return $"this.{member.Name}";
    }

    /// <summary>
    /// Applies target collection to enum collection conversion (facet List&lt;string/int&gt; ? source List&lt;enum&gt;) for ToSource mapping.
    /// </summary>
    private static string ApplyTargetCollectionToEnumConversion(FacetMember member)
    {
        var enumTypeName = member.OriginalEnumTypeName!;
        bool facetCollectionIsNullable = member.TypeName.Contains("?");
        bool sourceCollectionIsNullable = member.SourceMemberTypeName?.Contains("?") ?? false;

        string targetElementType = member.TypeName.TrimEnd('?');

        // Extract just the element type name from List<string> or List<int>
        if (targetElementType.Contains("<") && targetElementType.Contains(">"))
        {
            int startIdx = targetElementType.IndexOf('<') + 1;
            int endIdx = targetElementType.LastIndexOf('>');
            targetElementType = targetElementType.Substring(startIdx, endIdx - startIdx).Trim();
        }

        string conversionExpression;
        if (targetElementType.TrimEnd('?') == "string")
        {
            // Convert each string to enum using Enum.Parse
            conversionExpression = $"this.{member.Name}.Select(x => System.Enum.Parse<{enumTypeName}>(x))";
        }
        else if (targetElementType.TrimEnd('?') == "int")
        {
            // Convert each int to enum using cast
            conversionExpression = $"this.{member.Name}.Select(x => ({enumTypeName})x)";
        }
        else
        {
            return $"this.{member.Name}";
        }

        // Get the source collection wrapper (from SourceMemberTypeName or use CollectionWrapper)
        string sourceWrapper = member.SourceCollectionWrapper ?? member.CollectionWrapper!;

        // Wrap in the appropriate collection type
        var finalExpression = WrapCollectionProjection(conversionExpression, sourceWrapper);

        // Apply null check if collection is nullable
        if (facetCollectionIsNullable && sourceCollectionIsNullable)
        {
            return $"this.{member.Name} != null ? {finalExpression} : null";
        }
        else if (facetCollectionIsNullable)
        {
            // Facet is nullable but source expects non-nullable - use null-forgiving
            return $"{finalExpression}!";
        }

        return finalExpression;
    }

    /// <summary>
    /// Gets the appropriate default value for a collection type when depth is exceeded.
    /// For ImmutableArray (value type), returns default(ImmutableArray&lt;T&gt;).
    /// For other collections, returns an empty collection expression that matches the declared type.
    /// </summary>
    private static string GetCollectionDefaultValue(string collectionWrapper, string collectionTypeName)
    {
        // Extract the element type from the collection type name for constructing empty collections
        var elementType = ExtractElementTypeFromCollectionTypeName(collectionTypeName);

        return collectionWrapper switch
        {
            FacetConstants.CollectionWrappers.ImmutableArray => $"default({collectionTypeName})",
            FacetConstants.CollectionWrappers.ImmutableList => $"global::System.Collections.Immutable.ImmutableList<{elementType}>.Empty",
            FacetConstants.CollectionWrappers.ImmutableHashSet => $"global::System.Collections.Immutable.ImmutableHashSet<{elementType}>.Empty",
            FacetConstants.CollectionWrappers.ImmutableSortedSet => $"global::System.Collections.Immutable.ImmutableSortedSet<{elementType}>.Empty",
            FacetConstants.CollectionWrappers.ImmutableQueue => $"global::System.Collections.Immutable.ImmutableQueue<{elementType}>.Empty",
            FacetConstants.CollectionWrappers.ImmutableStack => $"global::System.Collections.Immutable.ImmutableStack<{elementType}>.Empty",
            FacetConstants.CollectionWrappers.IImmutableList => $"global::System.Collections.Immutable.ImmutableList<{elementType}>.Empty",
            FacetConstants.CollectionWrappers.IImmutableSet => $"global::System.Collections.Immutable.ImmutableHashSet<{elementType}>.Empty",
            FacetConstants.CollectionWrappers.IImmutableQueue => $"global::System.Collections.Immutable.ImmutableQueue<{elementType}>.Empty",
            FacetConstants.CollectionWrappers.IImmutableStack => $"global::System.Collections.Immutable.ImmutableStack<{elementType}>.Empty",
            FacetConstants.CollectionWrappers.Array => $"System.Array.Empty<{elementType}>()",
            FacetConstants.CollectionWrappers.Collection => $"new global::System.Collections.ObjectModel.Collection<{elementType}>()",
            _ => $"new global::System.Collections.Generic.List<{elementType}>()"
        };
    }

    /// <summary>
    /// Determines the correct ToSource method name for a nested facet, handling multi-source scenarios.
    /// For single-source facets, returns "ToSource".
    /// For multi-source facets, returns the source-specific method name like "ToUnitEntity".
    /// </summary>
    /// <param name="nestedFacetTypeName">The type name of the nested facet (may include nullable marker and generic arguments).</param>
    /// <param name="nestedFacetSourceTypeName">The source type that the nested facet maps from.</param>
    /// <param name="facetLookup">Dictionary mapping facet type names to their model lists.</param>
    /// <param name="parentSourceTypeName">The source type name of the parent facet.</param>
    /// <returns>The ToSource method name to call (e.g., "ToSource" or "ToUnitEntity").</returns>
    private static string GetToSourceMethodName(
        string nestedFacetTypeName,
        string? nestedFacetSourceTypeName,
        Dictionary<string, List<FacetTargetModel>>? facetLookup,
        string? parentSourceTypeName)
    {
        // Default to "ToSource" if we don't have lookup information
        if (facetLookup == null || nestedFacetSourceTypeName == null)
            return "ToSource";

        // Find the nested facet models in the lookup
        var nestedFacetModels = FindNestedFacetModels(nestedFacetTypeName, facetLookup);
        if (nestedFacetModels == null || nestedFacetModels.Count <= 1)
        {
            // Single-source facet: use the default "ToSource" method
            return "ToSource";
        }

        // Multi-source facet: determine which ToSource method to call
        // The method name is "To" + simple source type name
        var sourceSimpleName = CodeGenerationHelpers.GetSimpleTypeName(nestedFacetSourceTypeName);
        var angleBracket = sourceSimpleName.IndexOf('<');
        if (angleBracket > 0)
            sourceSimpleName = sourceSimpleName.Substring(0, angleBracket);

        return "To" + sourceSimpleName;
    }

    /// <summary>
    /// Finds the list of facet models for a given nested facet type name.
    /// </summary>
    private static List<FacetTargetModel>? FindNestedFacetModels(string typeName, Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        // Strip nullable marker and extract the non-nullable type name
        var nonNullableTypeName = typeName.TrimEnd('?');

        // For collection types, extract the element type
        if (nonNullableTypeName.Contains('<'))
        {
            var elementType = ExtractElementTypeFromCollectionTypeName(nonNullableTypeName);
            nonNullableTypeName = elementType;
        }

        // Strip "global::" prefix and extract simple name
        var lookupName = nonNullableTypeName
            .Replace(Shared.GeneratorUtilities.GlobalPrefix, "")
            .Split('.', ':')
            .Last();

        // First try exact match with the lookup name
        if (facetLookup.TryGetValue(lookupName, out var nestedFacetModels))
        {
            return nestedFacetModels;
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
                    return kvp.Value;
                }
            }
        }

        return null;
    }

    #endregion
}
