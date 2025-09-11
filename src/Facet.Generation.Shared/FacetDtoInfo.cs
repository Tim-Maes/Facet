using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Facet.Extensions.EFCore.Generators;

/// <summary>
/// Information about a discovered Facet DTO.
/// </summary>
public sealed class FacetDtoInfo
{
    public string EntityTypeName { get; }
    public string DtoTypeName { get; }
    public string DtoNamespace { get; }
    public ImmutableArray<DtoPropertyInfo> Properties { get; }
    public ImmutableArray<string> TypeScriptAttributes { get; }

    public FacetDtoInfo(string entityTypeName, string dtoTypeName, string dtoNamespace, ImmutableArray<DtoPropertyInfo> properties, ImmutableArray<string> typeScriptAttributes = default)
    {
        EntityTypeName = entityTypeName;
        DtoTypeName = dtoTypeName;
        DtoNamespace = dtoNamespace;
        Properties = properties;
        TypeScriptAttributes = typeScriptAttributes.IsDefault ? ImmutableArray<string>.Empty : typeScriptAttributes;
    }

    public static FacetDtoInfo? TryCreate(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol dtoSymbol)
            return null;

        var facetAttribute = context.Attributes.FirstOrDefault();
        if (facetAttribute == null || facetAttribute.ConstructorArguments.Length == 0)
            return null;

        var entityTypeArg = facetAttribute.ConstructorArguments[0];
        if (entityTypeArg.Value is not INamedTypeSymbol entitySymbol)
            return null;

        var entityTypeName = entitySymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
        var dtoTypeName = dtoSymbol.Name;
        var dtoNamespace = dtoSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        var properties = AnalyzeDtoProperties(dtoSymbol);
        var tsAttributes = ImmutableArray<string>.Empty;
        return new FacetDtoInfo(entityTypeName!, dtoTypeName, dtoNamespace, properties, tsAttributes);
    }

    /// <summary>
    /// Attempt to create FacetDtoInfo from an entity decorated with GenerateDtos / GenerateAuditableDtos.
    /// Only returns a DTO if the Response bit (value 4) is set in the Types named argument (defaults to all if missing).
    /// </summary>
    public static FacetDtoInfo? TryCreateFromGenerateDtos(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol entitySymbol)
            return null;

        var entityTypeName = entitySymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
        var entityName = entitySymbol.Name;
        var entityNamespace = entitySymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        foreach (var attr in context.Attributes)
        {
            var shortName = attr.AttributeClass?.Name;
            if (shortName is not ("GenerateDtosAttribute" or "GenerateAuditableDtosAttribute"))
                continue;

            int typesValue = -1; // -1 means unspecified -> assume all
            string? customNamespace = null; string? prefix = null; string? suffix = null;
            var tsAttributes = ImmutableArray<string>.Empty;

            foreach (var kv in attr.NamedArguments)
            {
                switch (kv.Key)
                {
                    case "Types":
                        if (kv.Value.Value is int raw) typesValue = raw; break;
                    case "Namespace":
                        customNamespace = kv.Value.Value as string; break;
                    case "Prefix":
                        prefix = kv.Value.Value as string; break;
                    case "Suffix":
                        suffix = kv.Value.Value as string; break;
                    case "TypeScriptAttributes":
                        if (kv.Value.Kind == TypedConstantKind.Array)
                        {
                            var b = ImmutableArray.CreateBuilder<string>();
                            foreach (var v in kv.Value.Values) if (v.Value is string s) b.Add(s);
                            tsAttributes = b.ToImmutable();
                        }
                        break;
                }
            }

            // If typesValue unspecified treat as all bits set (Create=1, Update=2, Response=4, Query=8, Upsert=16 => 31)
            if (typesValue == -1) typesValue = 31;
            bool includeResponse = (typesValue & 4) != 0; // Response flag
            if (!includeResponse) continue;

            var responseDtoName = BuildDtoName(entityName, "", "Response", prefix, suffix);
            var dtoNamespace = customNamespace ?? entityNamespace;
            var properties = AnalyzeEntityProperties(entitySymbol);
            return new FacetDtoInfo(entityTypeName, responseDtoName, dtoNamespace, properties, tsAttributes);
        }

        // Fallback: attempt baseline Get{Entity}Response naming with scalar properties
        var fallbackName = "Get" + entityName + "Response";
        var fallbackProperties = AnalyzeEntityProperties(entitySymbol);
        return new FacetDtoInfo(entityTypeName, fallbackName, entityNamespace, fallbackProperties, ImmutableArray<string>.Empty);
    }

    private static string BuildDtoName(string sourceTypeName, string prefix, string suffix, string? customPrefix, string? customSuffix)
    {
        var name = sourceTypeName;
        if (!string.IsNullOrWhiteSpace(customPrefix)) name = customPrefix + name;
        if (!string.IsNullOrWhiteSpace(prefix)) name = prefix + name;
        if (!string.IsNullOrWhiteSpace(suffix)) name = name + suffix;
        if (!string.IsNullOrWhiteSpace(customSuffix)) name = name + customSuffix;
        return name;
    }

    public static ImmutableArray<DtoPropertyInfo> AnalyzeDtoProperties(INamedTypeSymbol dtoSymbol)
    {
        var properties = ImmutableArray.CreateBuilder<DtoPropertyInfo>();
        foreach (var member in dtoSymbol.GetMembers())
        {
            if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public)
            {
                var isNavigation = IsNavigationProperty(prop.Type);
                properties.Add(new DtoPropertyInfo(prop.Name, prop.Type.ToDisplayString(), isNavigation));
            }
            else if (member is IFieldSymbol field && field.DeclaredAccessibility == Accessibility.Public)
            {
                var isNavigation = IsNavigationProperty(field.Type);
                properties.Add(new DtoPropertyInfo(field.Name, field.Type.ToDisplayString(), isNavigation));
            }
        }
        return properties.ToImmutable();
    }

    public static ImmutableArray<DtoPropertyInfo> AnalyzeEntityProperties(INamedTypeSymbol entitySymbol)
    {
        var properties = ImmutableArray.CreateBuilder<DtoPropertyInfo>();
        foreach (var member in entitySymbol.GetMembers())
        {
            if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public)
            {
                var isNavigation = IsNavigationProperty(prop.Type);
                if (!isNavigation)
                {
                    properties.Add(new DtoPropertyInfo(prop.Name, prop.Type.ToDisplayString(), isNavigation));
                }
            }
        }
        return properties.ToImmutable();
    }

    private static bool IsNavigationProperty(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString();
        return typeName.Contains("ICollection") || typeName.Contains("List") ||
               (!IsScalarType(typeName) && !typeName.Contains("string") && !typeName.Contains("Guid"));
    }

    private static bool IsScalarType(string typeName)
    {
        var scalarTypes = new[] { "int", "long", "decimal", "double", "float", "bool", "DateTime", "DateTimeOffset", "byte" };
        return scalarTypes.Any(t => typeName.Contains(t));
    }
}

/// <summary>
/// Information about a property in a DTO.
/// </summary>
public sealed class DtoPropertyInfo
{
    public string Name { get; }
    public string TypeName { get; }
    public bool IsNavigation { get; }
    public DtoPropertyInfo(string name, string typeName, bool isNavigation)
    {
        Name = name;
        TypeName = typeName;
        IsNavigation = isNavigation;
    }
}
