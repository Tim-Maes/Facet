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
    public static void Generate(StringBuilder sb, FacetTargetModel model, string indent, Dictionary<string, FacetTargetModel> facetLookup)
    {
        if (model.FlattenToTypes.Length == 0) return;

        // For each flatten target type, generate a FlattenTo method
        foreach (var flattenToType in model.FlattenToTypes)
        {
            GenerateFlattenToMethod(sb, model, flattenToType, indent, facetLookup);
        }
    }

    private static void GenerateFlattenToMethod(StringBuilder sb, FacetTargetModel model, string flattenToType, string indent, Dictionary<string, FacetTargetModel> facetLookup)
    {
        // Extract the simple name from the fully qualified type name
        var flattenToTypeName = ExtractSimpleName(flattenToType);

        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Flattens collection properties into multiple {flattenToTypeName} rows,");
        sb.AppendLine($"{indent}/// combining this facet's properties with each collection item.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <returns>A list of {flattenToTypeName} instances, one per collection item.</returns>");
        sb.AppendLine($"{indent}public global::System.Collections.Generic.List<{flattenToType}> FlattenTo()");
        sb.AppendLine($"{indent}{{");

        // Find collection members in this facet that are nested facets
        var collectionMembers = model.Members
            .Where(m => m.IsCollection && m.IsNestedFacet && !string.IsNullOrEmpty(m.CollectionWrapper))
            .ToList();

        if (collectionMembers.Count == 0)
        {
            // No collection properties - return empty list
            sb.AppendLine($"{indent}    return new global::System.Collections.Generic.List<{flattenToType}>();");
        }
        else
        {
            // Use the first nested facet collection property
            // Note: If multiple collections exist, the first one will be used.
            // In the future, this could be enhanced to:
            // 1. Analyze the target flatten type to determine which collection it expects
            // 2. Allow users to specify which collection to flatten via attribute parameter
            // 3. Generate multiple FlattenTo methods for each collection
            var collectionMember = collectionMembers[0];

            sb.AppendLine($"{indent}    if ({collectionMember.Name} == null)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        return new global::System.Collections.Generic.List<{flattenToType}>();");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    return {collectionMember.Name}.Select(item => new {flattenToType}");
            sb.AppendLine($"{indent}    {{");

            // Generate property mappings - copy all non-collection properties from the parent
            var nonCollectionMembers = model.Members
                .Where(m => !m.IsCollection && !m.IsNestedFacet)
                .ToList();

            foreach (var member in nonCollectionMembers)
            {
                sb.AppendLine($"{indent}        {member.Name} = {member.Name},");
            }

            // Now try to map properties from the collection item
            // We need to look up the nested facet type to see what properties it has
            var nestedFacetTypeName = ExtractNestedTypeName(collectionMember.TypeName);

            // Try to find the nested facet in the lookup - try multiple key variations
            FacetTargetModel? nestedFacet = null;
            if (!string.IsNullOrEmpty(nestedFacetTypeName))
            {
                // Try the full qualified name first
                if (!facetLookup.TryGetValue(nestedFacetTypeName, out nestedFacet))
                {
                    // Try just the simple name
                    var simpleName = ExtractSimpleName(nestedFacetTypeName);
                    facetLookup.TryGetValue(simpleName, out nestedFacet);
                }
            }

            if (nestedFacet != null)
            {
                // Map properties from the nested facet using SmartLeaf-style collision detection
                var collectionPropertyName = collectionMember.Name;

                // First pass: Identify which leaf property names appear multiple times
                var leafNameCounts = new Dictionary<string, int>();
                CollectLeafNames(
                    nestedFacet,
                    facetLookup,
                    nonCollectionMembers,
                    new List<string> { collectionPropertyName },
                    leafNameCounts,
                    maxDepth: 5);

                // Build set of colliding names (names that appear more than once)
                var collidingLeafNames = new HashSet<string>(
                    leafNameCounts.Where(kvp => kvp.Value > 1).Select(kvp => kvp.Key));

                // Second pass: Recursively collect all scalar properties with smart naming
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

            // Close the object initializer
            sb.AppendLine($"{indent}    }}).ToList();");
        }

        sb.AppendLine($"{indent}}}");
    }

    /// <summary>
    /// First pass: Collects all leaf property names and counts their occurrences.
    /// Used to identify which property names collide and need SmartLeaf-style prefixing.
    /// </summary>
    private static void CollectLeafNames(
        FacetTargetModel facet,
        Dictionary<string, FacetTargetModel> facetLookup,
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
                // Recurse into nested facet
                var nestedFacetTypeName = member.TypeName?.Replace("?", "").Trim();
                if (!string.IsNullOrEmpty(nestedFacetTypeName))
                {
                    FacetTargetModel? nestedFacet = null;
                    if (!facetLookup.TryGetValue(nestedFacetTypeName, out nestedFacet))
                    {
                        var simpleName = ExtractSimpleName(nestedFacetTypeName);
                        facetLookup.TryGetValue(simpleName, out nestedFacet);
                    }

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
                // This is a scalar leaf property
                var leafName = member.Name;

                // Always count the occurrence, even if it collides with parent
                // Parent collisions need to be tracked so we can prefix them
                if (leafNameCounts.ContainsKey(leafName))
                {
                    leafNameCounts[leafName]++;
                }
                else
                {
                    // Initialize count
                    // If it also exists in parent, immediately mark as collision
                    int initialCount = parentMembers.Any(pm => pm.Name == leafName) ? 2 : 1;
                    leafNameCounts[leafName] = initialCount;
                }
            }
        }
    }

    /// <summary>
    /// Recursively collects scalar properties from a nested facet and its child nested facets.
    /// Generates property assignments with proper navigation paths (e.g., item.Extended.ExtendedValue).
    /// Uses SmartLeaf-style naming: only adds parent prefix when property names collide.
    /// </summary>
    private static void CollectNestedProperties(
        StringBuilder sb,
        FacetTargetModel facet,
        Dictionary<string, FacetTargetModel> facetLookup,
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
            // Prevent infinite recursion in circular reference scenarios
            return;
        }

        foreach (var member in facet.Members)
        {
            // Skip collection properties - we only flatten scalar values
            if (member.IsCollection)
            {
                continue;
            }

            if (member.IsNestedFacet)
            {
                // This is a nested facet - recurse into it
                var nestedFacetTypeName = member.TypeName?.Replace("?", "").Trim();

                if (!string.IsNullOrEmpty(nestedFacetTypeName))
                {
                    // Try to find the nested facet in the lookup
                    FacetTargetModel? nestedFacet = null;
                    if (!facetLookup.TryGetValue(nestedFacetTypeName, out nestedFacet))
                    {
                        // Try just the simple name
                        var simpleName = ExtractSimpleName(nestedFacetTypeName);
                        facetLookup.TryGetValue(simpleName, out nestedFacet);
                    }

                    if (nestedFacet != null)
                    {
                        // Recursively collect properties from this nested facet
                        // Build navigation path: item.Extended.SubProperty
                        var newNavigationPath = $"{navigationPath}.{member.Name}";

                        // Add this member to the path segments for SmartLeaf naming
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
                // Check if this is actually a scalar property or a navigation property
                // Navigation properties that aren't configured as nested facets should be skipped
                if (!IsScalarType(member.TypeName))
                {
                    // This is likely a navigation property (reference type) that's not configured as a nested facet
                    // Skip it - the user needs to explicitly configure it as a nested facet if they want it included
                    continue;
                }

                // This is a scalar property - add it to the flattened output
                var leafName = member.Name;
                var navigationExpression = $"{navigationPath}.{leafName}";

                // Skip Id properties to avoid collision with parent Id
                if (leafName.Equals("Id", System.StringComparison.Ordinal) &&
                    parentMembers.Any(pm => pm.Name == leafName))
                {
                    // Skip nested Id - it would conflict with parent Id
                    // User can manually add a prefixed Id property if they need it
                    continue;
                }

                // Use SmartLeaf naming strategy for all properties
                var propertyName = GenerateSmartLeafName(pathSegments, leafName, collidingLeafNames);
                sb.AppendLine($"{indent}        {propertyName} = {navigationExpression},");
            }
        }
    }

    /// <summary>
    /// Generates a property name using SmartLeaf strategy:
    /// - If the leaf name doesn't collide with others, use just the leaf name
    /// - If it collides, use parent + leaf name (e.g., "PositionName" instead of "Name")
    /// </summary>
    private static string GenerateSmartLeafName(List<string> pathSegments, string leafName, HashSet<string> collidingLeafNames)
    {
        // If this leaf name collides with another, use parent + leaf
        if (collidingLeafNames.Contains(leafName) && pathSegments.Count >= 1)
        {
            // Use immediate parent + leaf name
            var parentName = pathSegments[pathSegments.Count - 1];
            return parentName + leafName;
        }

        // No collision, use leaf only
        return leafName;
    }

    private static string ExtractSimpleName(string fullyQualifiedName)
    {
        // Remove global:: prefix if present
        var name = fullyQualifiedName.Replace("global::", "");
        
        // Get the last part after the last dot
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
        {
            name = name.Substring(lastDot + 1);
        }

        return name;
    }

    private static string ExtractNestedTypeName(string collectionTypeName)
    {
        // Remove nullable marker
        var typeName = collectionTypeName.Replace("?", "").Trim();
        
        // Find the opening angle bracket for the generic type
        var startIndex = typeName.IndexOf('<');
        if (startIndex < 0) return string.Empty;
        
        // Find the matching closing bracket by counting brackets
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
        
        // Extract the type between the brackets
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

        // Remove nullable marker and global:: prefix
        var cleanType = typeName.Replace("?", "").Replace("global::", "").Trim();

        // Remove namespace qualifications to get just the type name
        var lastDot = cleanType.LastIndexOf('.');
        if (lastDot >= 0)
        {
            cleanType = cleanType.Substring(lastDot + 1);
        }

        // Check for known scalar/value types
        // Primitives
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

        // Common value types
        if (cleanType == "DateTime" || cleanType == "DateTimeOffset" ||
            cleanType == "TimeSpan" || cleanType == "Guid" ||
            cleanType == "DateOnly" || cleanType == "TimeOnly")
        {
            return true;
        }

        // String (reference type but treated as scalar for flattening purposes)
        if (cleanType == "string" || cleanType == "String")
        {
            return true;
        }

        // If it's a generic Nullable<T>, check the inner type
        if (cleanType.StartsWith("Nullable<") || cleanType.StartsWith("Nullable`"))
        {
            var innerType = ExtractNestedTypeName(typeName);
            return IsScalarType(innerType);
        }

        // Everything else is likely a reference type (navigation property)
        return false;
    }
}
