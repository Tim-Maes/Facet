using Facet.Generators.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facet.Generators;

/// <summary>
/// Generates FlattenTo methods that unpack collection properties into flattened rows.
/// </summary>
internal static class FlattenToGenerator
{
    /// <summary>
    /// Generates FlattenTo methods for all configured flatten target types.
    /// </summary>
    public static void Generate(StringBuilder sb, FacetTargetModel model, string indent, Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        if (model.FlattenToTypes.Length == 0) return;

        foreach (var flattenToType in model.FlattenToTypes)
        {
            GenerateFlattenToMethod(sb, model, flattenToType, indent, facetLookup);
        }
    }

    private static void GenerateFlattenToMethod(StringBuilder sb, FacetTargetModel model, string flattenToType, string indent, Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        var flattenToTypeName = ExtractSimpleName(flattenToType);

        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Flattens collection properties into multiple {flattenToTypeName} rows,");
        sb.AppendLine($"{indent}/// combining this facet's properties with each collection item.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <returns>A list of {flattenToTypeName} instances, one per collection item.</returns>");
        sb.AppendLine($"{indent}public global::System.Collections.Generic.List<{flattenToType}> FlattenTo()");
        sb.AppendLine($"{indent}{{");

        var collectionMembers = model.Members
            .Where(m => m.IsCollection && m.IsNestedFacet && !string.IsNullOrEmpty(m.CollectionWrapper))
            .ToList();

        if (collectionMembers.Count == 0)
        {
            sb.AppendLine($"{indent}    return new global::System.Collections.Generic.List<{flattenToType}>();");
        }
        else
        {
            var collectionMember = collectionMembers[0];

            sb.AppendLine($"{indent}    if ({collectionMember.Name} == null)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return new global::System.Collections.Generic.List<{flattenToType}>();");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    return {collectionMember.Name}.Select(item => new {flattenToType}");
            sb.AppendLine($"{indent}    {{");

            var nonCollectionMembers = model.Members
                .Where(m => !m.IsCollection && !m.IsNestedFacet)
                .ToList();

            foreach (var member in nonCollectionMembers)
            {
                sb.AppendLine($"{indent}        {member.Name} = {member.Name},");
            }

            var nestedFacetTypeName = ExtractNestedTypeName(collectionMember.TypeName);

            FacetTargetModel? nestedFacet = null;
            if (!string.IsNullOrEmpty(nestedFacetTypeName))
            {
                nestedFacet = FindFacetModel(nestedFacetTypeName, facetLookup);
            }

            if (nestedFacet != null)
            {
                var collectionPropertyName = collectionMember.Name;

                var leafNameCounts = new Dictionary<string, int>();
                CollectLeafNames(
                    nestedFacet,
                    facetLookup,
                    nonCollectionMembers,
                    new List<string> { collectionPropertyName },
                    leafNameCounts,
                    maxDepth: 5);

                var collidingLeafNames = new HashSet<string>(
                    leafNameCounts.Where(kvp => kvp.Value > 1).Select(kvp => kvp.Key));

                CollectNestedProperties(
                    sb,
                    nestedFacet,
                    facetLookup,
                    nonCollectionMembers,
                    "item",
                    new List<string> { collectionPropertyName },
                    collidingLeafNames,
                    indent,
                    maxDepth: 5);
            }

            sb.AppendLine($"{indent}    }}).ToList();");
        }

        sb.AppendLine($"{indent}}}");
    }

    private static FacetTargetModel? FindFacetModel(string typeName, Dictionary<string, List<FacetTargetModel>> facetLookup)
    {
        if (string.IsNullOrEmpty(typeName)) return null;

        if (facetLookup.TryGetValue(typeName, out var facetModels) && facetModels.Count > 0)
        {
            return facetModels[0];
        }

        var simpleName = ExtractSimpleName(typeName);
        if (facetLookup.TryGetValue(simpleName, out facetModels) && facetModels.Count > 0)
        {
            return facetModels[0];
        }

        var withoutGlobal = typeName.Replace("global::", "");
        if (facetLookup.TryGetValue(withoutGlobal, out facetModels) && facetModels.Count > 0)
        {
            return facetModels[0];
        }

        return null;
    }

    private static void CollectLeafNames(
        FacetTargetModel facet,
        Dictionary<string, List<FacetTargetModel>> facetLookup,
        List<FacetMember> parentMembers,
        List<string> pathSegments,
        Dictionary<string, int> leafNameCounts,
        int maxDepth,
        int currentDepth = 0)
    {
        if (currentDepth >= maxDepth)
        {
            return;
        }

        foreach (var member in facet.Members)
        {
            if (member.IsCollection)
            {
                continue;
            }

            if (member.IsNestedFacet)
            {
                var nestedFacetTypeName = member.TypeName?.Replace("?", "").Trim();
                if (!string.IsNullOrEmpty(nestedFacetTypeName))
                {
                    var nestedFacet = FindFacetModel(nestedFacetTypeName, facetLookup);
                    if (nestedFacet != null)
                    {
                        var newPathSegments = new List<string>(pathSegments) { member.Name };
                        CollectLeafNames(
                            nestedFacet,
                            facetLookup,
                            parentMembers,
                            newPathSegments,
                            leafNameCounts,
                            maxDepth,
                            currentDepth + 1);
                    }
                }
            }
            else if (IsScalarType(member.TypeName))
            {
                var leafName = member.Name;

                // Track parent collisions so later names can be prefixed.
                if (leafNameCounts.ContainsKey(leafName))
                {
                    leafNameCounts[leafName]++;
                }
                else
                {
                    int initialCount = parentMembers.Any(pm => pm.Name == leafName) ? 2 : 1;
                    leafNameCounts[leafName] = initialCount;
                }
            }
        }
    }

    private static void CollectNestedProperties(
        StringBuilder sb,
        FacetTargetModel facet,
        Dictionary<string, List<FacetTargetModel>> facetLookup,
        List<FacetMember> parentMembers,
        string navigationPath,
        List<string> pathSegments,
        HashSet<string> collidingLeafNames,
        string indent,
        int maxDepth,
        int currentDepth = 0)
    {
        if (currentDepth >= maxDepth)
        {
            return;
        }

        foreach (var member in facet.Members)
        {
            if (member.IsCollection)
            {
                continue;
            }

            if (member.IsNestedFacet)
            {
                var nestedFacetTypeName = member.TypeName?.Replace("?", "").Trim();

                if (!string.IsNullOrEmpty(nestedFacetTypeName))
                {
                    var nestedFacet = FindFacetModel(nestedFacetTypeName, facetLookup);
                    if (nestedFacet != null)
                    {
                        var newNavigationPath = $"{navigationPath}.{member.Name}";

                        var newPathSegments = new List<string>(pathSegments) { member.Name };

                        CollectNestedProperties(
                            sb,
                            nestedFacet,
                            facetLookup,
                            parentMembers,
                            newNavigationPath,
                            newPathSegments,
                            collidingLeafNames,
                            indent,
                            maxDepth,
                            currentDepth + 1);
                    }
                }
            }
            else
            {
                // Skip non-scalar members unless they are nested facets.
                if (!IsScalarType(member.TypeName))
                {
                    continue;
                }

                var leafName = member.Name;
                var navigationExpression = $"{navigationPath}.{leafName}";

                if (leafName.Equals("Id", System.StringComparison.Ordinal) &&
                    parentMembers.Any(pm => pm.Name == leafName))
                {
                    continue;
                }

                var propertyName = GenerateSmartLeafName(pathSegments, leafName, collidingLeafNames);
                sb.AppendLine($"{indent}        {propertyName} = {navigationExpression},");
            }
        }
    }

    private static string GenerateSmartLeafName(List<string> pathSegments, string leafName, HashSet<string> collidingLeafNames)
    {
        if (collidingLeafNames.Contains(leafName) && pathSegments.Count >= 1)
        {
            var parentName = pathSegments[pathSegments.Count - 1];
            return parentName + leafName;
        }

        return leafName;
    }

    private static string ExtractSimpleName(string fullyQualifiedName)
    {
        var name = fullyQualifiedName.Replace("global::", "");
        
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
        {
            name = name.Substring(lastDot + 1);
        }

        return name;
    }

    private static string ExtractNestedTypeName(string collectionTypeName)
    {
        var typeName = collectionTypeName.Replace("?", "").Trim();
        
        var startIndex = typeName.IndexOf('<');
        if (startIndex < 0) return string.Empty;
        
        var depth = 0;
        var endIndex = -1;
        for (var i = startIndex; i < typeName.Length; i++)
        {
            if (typeName[i] == '<')
            {
                depth++;
            }
            else if (typeName[i] == '>')
            {
                depth--;
                if (depth == 0)
                {
                    endIndex = i;
                    break;
                }
            }
        }
        
        if (endIndex < 0) return string.Empty;
        
        var innerType = typeName.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();

        return innerType;
    }

    /// <summary>
    /// Determines if a type name represents a scalar/value type that should be included in flattening.
    /// Returns false for reference types (navigation properties) that aren't configured as nested facets.
    /// </summary>
    private static bool IsScalarType(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return false;

        var cleanType = typeName.Replace("?", "").Replace("global::", "").Trim();

        var lastDot = cleanType.LastIndexOf('.');
        if (lastDot >= 0)
        {
            cleanType = cleanType.Substring(lastDot + 1);
        }

        if (cleanType == "int" || cleanType == "Int32" ||
            cleanType == "long" || cleanType == "Int64" ||
            cleanType == "short" || cleanType == "Int16" ||
            cleanType == "byte" || cleanType == "Byte" ||
            cleanType == "sbyte" || cleanType == "SByte" ||
            cleanType == "uint" || cleanType == "UInt32" ||
            cleanType == "ulong" || cleanType == "UInt64" ||
            cleanType == "ushort" || cleanType == "UInt16" ||
            cleanType == "bool" || cleanType == "Boolean" ||
            cleanType == "float" || cleanType == "Single" ||
            cleanType == "double" || cleanType == "Double" ||
            cleanType == "char" || cleanType == "Char" ||
            cleanType == "decimal" || cleanType == "Decimal")
        {
            return true;
        }

        if (cleanType == "DateTime" || cleanType == "DateTimeOffset" ||
            cleanType == "TimeSpan" || cleanType == "Guid" ||
            cleanType == "DateOnly" || cleanType == "TimeOnly")
        {
            return true;
        }

        if (cleanType == "string" || cleanType == "String")
        {
            return true;
        }

        if (cleanType.StartsWith("Nullable<") || cleanType.StartsWith("Nullable`"))
        {
            var innerType = ExtractNestedTypeName(typeName);
            return IsScalarType(innerType);
        }

        return false;
    }
}
