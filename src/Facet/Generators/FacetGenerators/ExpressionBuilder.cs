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
        bool preserveReferences = false,
        HashSet<string>? sourcePropertyNames = null)
    {
        bool isNullable = member.TypeName.EndsWith("?");

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

        string valueExpression;
        if (member.MapFromSource != null && IsExpression(member.MapFromSource))
        {
            valueExpression = TransformExpression(member.MapFromSource, sourceVariableName, sourcePropertyNames);
        }
        else if (member.MapFromSource != null)
        {
            valueExpression = $"{sourceVariableName}.{member.MapFromSource}";
        }
        else
        {
            valueExpression = $"{sourceVariableName}.{member.SourcePropertyName}";
        }

        if (member.IsEnumConversion && member.OriginalEnumTypeName != null)
        {
            valueExpression = ApplyEnumToTargetConversion(valueExpression, member);
        }

        if (member.MapWhenConditions.Count > 0)
        {
            valueExpression = WrapWithMapWhenCondition(member, valueExpression, sourceVariableName, sourcePropertyNames);
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
    /// <param name="maxDepthToSource">When &gt; 0, depth-aware expressions are emitted that guard nested calls with a depth check.</param>
    /// <param name="useDepthParameter">When true the expression is being emitted inside a depth-aware <c>ToSource(int __depth)</c> overload.</param>
    public static string GetToSourceValueExpression(
        FacetMember member,
        Dictionary<string, List<FacetTargetModel>>? facetLookup = null,
        string? parentSourceTypeName = null,
        int maxDepthToSource = 0,
        bool useDepthParameter = false)
    {
        bool facetTypeIsNullable = member.TypeName.EndsWith("?");

        bool sourceTypeIsNullable = member.SourceMemberTypeName?.EndsWith("?") ?? facetTypeIsNullable;

        if (member.IsNestedFacet && member.IsCollection)
        {
            return BuildCollectionToSourceExpression(member, facetTypeIsNullable, facetLookup, parentSourceTypeName, maxDepthToSource, useDepthParameter);
        }
        else if (member.IsNestedFacet)
        {
            return BuildSingleToSourceExpression(member, facetTypeIsNullable, facetLookup, parentSourceTypeName, maxDepthToSource, useDepthParameter);
        }

        if (member.IsEnumConversion && member.OriginalEnumTypeName != null)
        {
            return ApplyTargetToEnumConversion(member);
        }

        if (facetTypeIsNullable && !sourceTypeIsNullable)
        {
            if (member.IsValueType)
            {
                return $"this.{member.Name} ?? default";
            }
            else
            {
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
        var nonNullableCollectionType = collectionTypeName.TrimEnd('?');

        if (nonNullableCollectionType.EndsWith("[]"))
        {
            var elementType = nonNullableCollectionType.Substring(0, nonNullableCollectionType.Length - 2);
            
            return elementType.TrimEnd('?');
        }

        var startIndex = nonNullableCollectionType.IndexOf('<');
        var endIndex = nonNullableCollectionType.LastIndexOf('>');

        if (startIndex > 0 && endIndex > startIndex)
        {
            var elementType = nonNullableCollectionType.Substring(startIndex + 1, endIndex - startIndex - 1);
            
            return elementType.TrimEnd('?');
        }

        return nonNullableCollectionType.TrimEnd('?');
    }

    #region Private Helper Methods

    /// <summary>
    /// Wraps a value expression with MapWhen condition(s), generating a ternary expression.
    /// </summary>
    private static string WrapWithMapWhenCondition(FacetMember member, string valueExpression, string sourceVariableName, HashSet<string>? sourcePropertyNames = null)
    {
        var combinedCondition = string.Join(" && ", member.MapWhenConditions.Select(c =>
            $"({TransformExpression(c, sourceVariableName, sourcePropertyNames)})"));

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

        var sourcePropName = member.SourcePropertyName;

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

            var collectionExpression = WrapCollectionProjection(projection, member.CollectionWrapper!, elementTypeName);

            if (isNullable)
            {
                return $"__depth < {maxDepth} && {sourceVariableName}.{sourcePropName} != null ? {collectionExpression} : null";
            }

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

            var collectionExpression = WrapCollectionProjection(projection, member.CollectionWrapper!, elementTypeName);

            if (isNullable)
            {
                return $"{sourceVariableName}.{sourcePropName} != null ? {collectionExpression} : null";
            }

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
        
        var sourcePropName = member.SourcePropertyName;

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
                var nullFallback = isNullable ? "null" : "null!";
                return $"(__processed != null && __processed.Contains({sourceExpr}) ? {nullFallback} : {ctorCall})";
            }

            return ctorCall;
        }

        if (useDepthParameter && maxDepth > 0)
        {
            var constructorCall = BuildConstructorCall($"{sourceVariableName}.{sourcePropName}");

            if (isNullable)
            {
                return $"__depth < {maxDepth} && {sourceVariableName}.{sourcePropName} != null ? {constructorCall} : null";
            }

            return $"__depth < {maxDepth} ? ({sourceVariableName}.{sourcePropName} != null ? {constructorCall} : throw new System.ArgumentNullException(\"{sourcePropName}\", \"Required nested facet property '{sourcePropName}' on source type was null. Ensure the source property is populated before mapping.\")) : null!";
        }
        else
        {
            var constructorCall = BuildConstructorCall($"{sourceVariableName}.{sourcePropName}");

            if (isNullable)
            {
                return $"{sourceVariableName}.{sourcePropName} != null ? {constructorCall} : null";
            }

            return $"{sourceVariableName}.{sourcePropName} != null ? {constructorCall} : throw new System.ArgumentNullException(\"{sourcePropName}\", \"Required nested facet property '{sourcePropName}' on source type was null. Ensure the source property is populated before mapping.\")";
        }
    }

    private static string BuildCollectionToSourceExpression(FacetMember member, bool facetTypeIsNullable, Dictionary<string, List<FacetTargetModel>>? facetLookup, string? parentSourceTypeName, int maxDepthToSource = 0, bool useDepthParameter = false)
    {
        var toSourceMethodName = GetToSourceMethodName(member.TypeName, member.NestedFacetSourceTypeName, facetLookup, parentSourceTypeName);

        var childToSourceCall = useDepthParameter && ChildHasDepthAwareToSource(member.TypeName, facetLookup)
            ? $"x.{toSourceMethodName}(__depth + 1)"
            : $"x.{toSourceMethodName}()";

        var projection = $"this.{member.Name}.Select(x => {childToSourceCall})";

        var toSourceWrapper = member.SourceCollectionWrapper ?? member.CollectionWrapper!;
        var collectionExpression = WrapCollectionProjection(projection, toSourceWrapper, member.NestedFacetSourceTypeName);

        if (useDepthParameter && maxDepthToSource > 0)
        {
            if (facetTypeIsNullable)
            {
                return $"__depth < {maxDepthToSource} ? (this.{member.Name} != null ? {collectionExpression} : null) : null";
            }

            var depthExceededDefault = GetToSourceCollectionDefault(toSourceWrapper, member.NestedFacetSourceTypeName);
            return $"__depth < {maxDepthToSource} ? (this.{member.Name} != null ? {collectionExpression} : {depthExceededDefault}) : {depthExceededDefault}";
        }

        if (facetTypeIsNullable)
        {
            return $"this.{member.Name} != null ? {collectionExpression} : null";
        }

        return $"this.{member.Name} != null ? {collectionExpression} : default!";
    }

    private static string BuildSingleToSourceExpression(FacetMember member, bool facetTypeIsNullable, Dictionary<string, List<FacetTargetModel>>? facetLookup, string? parentSourceTypeName, int maxDepthToSource = 0, bool useDepthParameter = false)
    {
        var toSourceMethodName = GetToSourceMethodName(member.TypeName, member.NestedFacetSourceTypeName, facetLookup, parentSourceTypeName);

        var childToSourceCall = useDepthParameter && ChildHasDepthAwareToSource(member.TypeName, facetLookup)
            ? $"this.{member.Name}.{toSourceMethodName}(__depth + 1)"
            : $"this.{member.Name}.{toSourceMethodName}()";

        if (useDepthParameter && maxDepthToSource > 0)
        {
            if (facetTypeIsNullable)
            {
                return $"__depth < {maxDepthToSource} ? (this.{member.Name} != null ? {childToSourceCall} : null) : null";
            }

            return $"__depth < {maxDepthToSource} ? (this.{member.Name} != null ? {childToSourceCall} : null!) : null!";
        }

        if (facetTypeIsNullable)
        {
            return $"this.{member.Name} != null ? this.{member.Name}.{toSourceMethodName}() : null";
        }

        return $"this.{member.Name} != null ? this.{member.Name}.{toSourceMethodName}() : default!";
    }

    /// <summary>
    /// Returns true if any model for the given facet type name has <c>MaxDepthToSource &gt; 0</c>
    /// (meaning it has a depth-aware <c>ToSource(int __depth)</c> overload).
    /// </summary>
    private static bool ChildHasDepthAwareToSource(string facetTypeName, Dictionary<string, List<FacetTargetModel>>? facetLookup)
    {
        if (facetLookup == null) return false;

        var models = FindNestedFacetModels(facetTypeName, facetLookup);
        return models != null && models.Any(m => m.MaxDepthToSource > 0);
    }

    /// <summary>
    /// Returns a safe default expression for a collection property when the depth limit is exceeded
    /// during <c>ToSource()</c> — always an empty (non-null) collection so the source type remains valid.
    /// </summary>
    private static string GetToSourceCollectionDefault(string collectionWrapper, string? elementTypeName)
    {
        var et = elementTypeName ?? "object";
        return collectionWrapper switch
        {
            FacetConstants.CollectionWrappers.Array => $"global::System.Array.Empty<{et}>()",
            FacetConstants.CollectionWrappers.ImmutableArray => $"global::System.Collections.Immutable.ImmutableArray<{et}>.Empty",
            FacetConstants.CollectionWrappers.ImmutableList => $"global::System.Collections.Immutable.ImmutableList<{et}>.Empty",
            FacetConstants.CollectionWrappers.ImmutableHashSet => $"global::System.Collections.Immutable.ImmutableHashSet<{et}>.Empty",
            FacetConstants.CollectionWrappers.ImmutableSortedSet => $"global::System.Collections.Immutable.ImmutableSortedSet<{et}>.Empty",
            FacetConstants.CollectionWrappers.ImmutableQueue => $"global::System.Collections.Immutable.ImmutableQueue<{et}>.Empty",
            FacetConstants.CollectionWrappers.ImmutableStack => $"global::System.Collections.Immutable.ImmutableStack<{et}>.Empty",
            FacetConstants.CollectionWrappers.IImmutableList => $"global::System.Collections.Immutable.ImmutableList<{et}>.Empty",
            FacetConstants.CollectionWrappers.IImmutableSet => $"global::System.Collections.Immutable.ImmutableHashSet<{et}>.Empty",
            FacetConstants.CollectionWrappers.IImmutableQueue => $"global::System.Collections.Immutable.ImmutableQueue<{et}>.Empty",
            FacetConstants.CollectionWrappers.IImmutableStack => $"global::System.Collections.Immutable.ImmutableStack<{et}>.Empty",
            _ => $"new global::System.Collections.Generic.List<{et}>()"
        };
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

    private static bool IsExpression(string source) => ExpressionHelper.IsExpression(source);
    private static string TransformExpression(string expression, string sourceVariableName, HashSet<string>? sourcePropertyNames = null) => ExpressionHelper.TransformExpression(expression, sourceVariableName, sourcePropertyNames);

    /// <summary>
    /// Applies enum-to-target-type conversion (source enum ? facet string/int).
    /// </summary>
    private static string ApplyEnumToTargetConversion(string valueExpression, FacetMember member)
    {
        if (member.IsCollection && member.CollectionWrapper != null)
        {
            return ApplyEnumCollectionToTargetConversion(valueExpression, member);
        }

        bool isNullableEnum = member.SourceMemberTypeName?.EndsWith("?") ?? false;

        if (member.TypeName.TrimEnd('?') == "string")
        {
            if (isNullableEnum)
            {
                return $"{valueExpression}?.ToString()";
            }
            return $"{valueExpression}.ToString()";
        }
        else if (member.TypeName.TrimEnd('?') == "int")
        {
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
        bool isCollectionNullable = member.TypeName.EndsWith("?");
        string targetElementType = member.TypeName.TrimEnd('?');

        if (targetElementType.Contains("<") && targetElementType.Contains(">"))
        {
            int startIdx = targetElementType.IndexOf('<') + 1;
            int endIdx = targetElementType.LastIndexOf('>');
            targetElementType = targetElementType.Substring(startIdx, endIdx - startIdx).Trim();
        }

        string conversionExpression;
        if (targetElementType.TrimEnd('?') == "string")
        {
            conversionExpression = $"{valueExpression}.Select(x => x.ToString())";
        }
        else if (targetElementType.TrimEnd('?') == "int")
        {
            conversionExpression = $"{valueExpression}.Select(x => (int)x)";
        }
        else
        {
            return valueExpression;
        }

        var finalExpression = WrapCollectionProjection(conversionExpression, member.CollectionWrapper);

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
        if (member.IsCollection && member.CollectionWrapper != null)
        {
            return ApplyTargetCollectionToEnumConversion(member);
        }

        var enumTypeName = member.OriginalEnumTypeName!;
        bool facetTypeIsNullable = member.TypeName.EndsWith("?");
        bool sourceTypeIsNullable = member.SourceMemberTypeName?.EndsWith("?") ?? false;

        if (member.TypeName.TrimEnd('?') == "string")
        {
            if (facetTypeIsNullable && sourceTypeIsNullable)
            {
                return $"this.{member.Name} != null ? ({enumTypeName}?)System.Enum.Parse<{enumTypeName}>(this.{member.Name}) : null";
            }
            else if (facetTypeIsNullable)
            {
                return $"System.Enum.Parse<{enumTypeName}>(this.{member.Name}!)";
            }
            else
            {
                return $"System.Enum.Parse<{enumTypeName}>(this.{member.Name})";
            }
        }
        else if (member.TypeName.TrimEnd('?') == "int")
        {
            if (facetTypeIsNullable && sourceTypeIsNullable)
            {
                return $"this.{member.Name} != null ? ({enumTypeName}?)({enumTypeName})this.{member.Name}.Value : null";
            }
            else if (facetTypeIsNullable)
            {
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
        bool facetCollectionIsNullable = member.TypeName.EndsWith("?");
        bool sourceCollectionIsNullable = member.SourceMemberTypeName?.EndsWith("?") ?? false;

        string targetElementType = member.TypeName.TrimEnd('?');

        if (targetElementType.Contains("<") && targetElementType.Contains(">"))
        {
            int startIdx = targetElementType.IndexOf('<') + 1;
            int endIdx = targetElementType.LastIndexOf('>');
            targetElementType = targetElementType.Substring(startIdx, endIdx - startIdx).Trim();
        }

        string conversionExpression;
        if (targetElementType.TrimEnd('?') == "string")
        {
            conversionExpression = $"this.{member.Name}.Select(x => System.Enum.Parse<{enumTypeName}>(x))";
        }
        else if (targetElementType.TrimEnd('?') == "int")
        {
            conversionExpression = $"this.{member.Name}.Select(x => ({enumTypeName})x)";
        }
        else
        {
            return $"this.{member.Name}";
        }

        string sourceWrapper = member.SourceCollectionWrapper ?? member.CollectionWrapper!;

        var finalExpression = WrapCollectionProjection(conversionExpression, sourceWrapper);

        if (facetCollectionIsNullable && sourceCollectionIsNullable)
        {
            return $"this.{member.Name} != null ? {finalExpression} : null";
        }
        else if (facetCollectionIsNullable)
        {
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
        if (facetLookup == null || nestedFacetSourceTypeName == null)
            return "ToSource";

        var nestedFacetModels = FindNestedFacetModels(nestedFacetTypeName, facetLookup);
        if (nestedFacetModels == null || nestedFacetModels.Count <= 1)
        {
            return "ToSource";
        }

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
        var nonNullableTypeName = typeName.TrimEnd('?');

        if (nonNullableTypeName.Contains('<'))
        {
            var elementType = ExtractElementTypeFromCollectionTypeName(nonNullableTypeName);
            nonNullableTypeName = elementType;
        }

        var lookupName = nonNullableTypeName
            .Replace(Shared.GeneratorUtilities.GlobalPrefix, "")
            .Split('.', ':')
            .Last();

        if (facetLookup.TryGetValue(lookupName, out var nestedFacetModels))
        {
            return nestedFacetModels;
        }

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
