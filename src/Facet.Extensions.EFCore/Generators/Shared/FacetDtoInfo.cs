using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Threading;

namespace Facet.Extensions.EFCore.Generators.Shared;

/// <summary>
/// Information about a Facet DTO discovered from source code.
/// </summary>
public class FacetDtoInfo
{
    public string DtoTypeName { get; set; } = string.Empty;
    public string EntityTypeName { get; set; } = string.Empty;
    public string DtoNamespace { get; set; } = string.Empty;
    public List<PropertyInfo> Properties { get; set; } = new();

    public static FacetDtoInfo? TryCreate(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        // Implementation for [Facet] attribute discovery
        var symbol = context.TargetSymbol as INamedTypeSymbol;
        if (symbol == null) return null;

        var facetAttribute = symbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == "FacetAttribute");

        if (facetAttribute == null) return null;

        // Extract entity type from attribute argument
        var entityType = facetAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
        if (string.IsNullOrEmpty(entityType)) return null;

        return new FacetDtoInfo
        {
            DtoTypeName = symbol.Name,
            EntityTypeName = entityType,
            DtoNamespace = symbol.ContainingNamespace.ToDisplayString(),
            Properties = ExtractProperties(symbol)
        };
    }

    public static FacetDtoInfo? TryCreateFromGenerateDtos(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        // Implementation for [GenerateDtos] attribute discovery
        var symbol = context.TargetSymbol as INamedTypeSymbol;
        if (symbol == null) return null;

        var generateDtosAttribute = symbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == "GenerateDtosAttribute");

        if (generateDtosAttribute == null) return null;

        return new FacetDtoInfo
        {
            DtoTypeName = $"{symbol.Name}Response", // Assume Response DTO naming convention
            EntityTypeName = symbol.ToDisplayString(),
            DtoNamespace = symbol.ContainingNamespace.ToDisplayString(),
            Properties = ExtractProperties(symbol)
        };
    }

    private static List<PropertyInfo> ExtractProperties(INamedTypeSymbol symbol)
    {
        var properties = new List<PropertyInfo>();

        foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.DeclaredAccessibility != Accessibility.Public) continue;

            properties.Add(new PropertyInfo
            {
                Name = member.Name,
                TypeName = member.Type.ToDisplayString(),
                IsNavigation = IsNavigationProperty(member)
            });
        }

        return properties;
    }

    private static bool IsNavigationProperty(IPropertySymbol property)
    {
        // Simple heuristic: if it's a reference type that's not a string or primitive, consider it navigation
        var type = property.Type;
        if (type.TypeKind == TypeKind.Class && type.SpecialType != SpecialType.System_String)
            return true;
        if (type.TypeKind == TypeKind.Interface)
            return true;
        return false;
    }
}

/// <summary>
/// Information about a property in a DTO.
/// </summary>
public class PropertyInfo
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool IsNavigation { get; set; }
}