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
                // Map properties from the nested facet using the "item" variable
                // Use the collection property name as the prefix (e.g., "Extended" from "Extended" property)
                // rather than the facet type name (e.g., "ExtendedFacet")
                var collectionPropertyName = collectionMember.Name;
                
                foreach (var nestedMember in nestedFacet.Members.Where(m => !m.IsCollection && !m.IsNestedFacet))
                {
                    // Check if this property name exists in the parent to avoid duplication
                    if (!nonCollectionMembers.Any(pm => pm.Name == nestedMember.Name))
                    {
                        sb.AppendLine($"{indent}        {nestedMember.Name} = item.{nestedMember.Name},");
                    }
                    else
                    {
                        // Property name collision - try to use a prefixed name based on the collection property name
                        // E.g., if collection is "Extended" and member is "Name", try "ExtendedName"
                        // Skip Id properties since they're often ignored in flattened outputs (IgnoreNestedIds)
                        if (nestedMember.Name.Equals("Id", System.StringComparison.Ordinal))
                        {
                            // Skip nested Id - it would conflict with parent Id
                            // User can manually add ExtendedId property if they need it
                            continue;
                        }
                        
                        var prefixedName = $"{collectionPropertyName}{nestedMember.Name}";
                        sb.AppendLine($"{indent}        {prefixedName} = item.{nestedMember.Name},");
                    }
                }
            }

            // Close the object initializer
            sb.AppendLine($"{indent}    }}).ToList();");
        }

        sb.AppendLine($"{indent}}}");
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
}
