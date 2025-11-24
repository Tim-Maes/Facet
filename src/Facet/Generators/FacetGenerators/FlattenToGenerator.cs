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
            // For now, we'll assume the first collection property is the one to flatten
            // In a more sophisticated implementation, we could detect which collection to use
            // based on the flatten target type's source type
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
                foreach (var nestedMember in nestedFacet.Members.Where(m => !m.IsCollection && !m.IsNestedFacet))
                {
                    // Check if this property name exists in the parent to avoid duplication
                    if (!nonCollectionMembers.Any(pm => pm.Name == nestedMember.Name))
                    {
                        sb.AppendLine($"{indent}        {nestedMember.Name} = item.{nestedMember.Name},");
                    }
                    else
                    {
                        // Property name collision - need to use a different name in the target
                        // The user should have defined it with a prefix like "ExtendedName"
                        // We'll try common patterns but this is a heuristic
                        var itemTypeName = ExtractSimpleName(nestedFacetTypeName);
                        var prefixedName = $"{itemTypeName}{nestedMember.Name}";
                        sb.AppendLine($"{indent}        // {prefixedName} = item.{nestedMember.Name}, // Uncomment and adjust property name if needed");
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
        
        // Find the matching closing bracket
        var endIndex = typeName.LastIndexOf('>');
        if (endIndex < 0) return string.Empty;
        
        // Extract the type between the brackets
        var innerType = typeName.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
        
        return innerType;
    }
}
