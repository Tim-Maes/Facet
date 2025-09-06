using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Facet.Extensions.EFCore.Generators.Emission;

namespace Facet.Extensions.EFCore.Generators;

/// <summary>
/// Source generator for EF Core fluent navigation builders.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class FacetEfGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Read EF model from JSON
        var efModel = EfJsonReader.Configure(context);

        // Discover Facet DTOs from attributes (both FacetAttribute and GenerateDtosAttribute)
        var facetDtos = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Facet.FacetAttribute",
                static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                static (context, cancellationToken) => FacetDtoInfo.TryCreate(context, cancellationToken))
            .Where(static dto => dto != null)
            .Select(static (dto, _) => dto!);

        // Also discover entities with GenerateDtosAttribute
        var generateDtoEntities = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Facet.GenerateDtosAttribute",
                static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                static (context, cancellationToken) => FacetDtoInfo.TryCreateFromGenerateDtos(context, cancellationToken))
            .Where(static dto => dto != null)
            .Select(static (dto, _) => dto!);

        // Combine both sources of DTOs by collecting them first and deduplicate
        var allDtos = facetDtos.Collect()
            .Combine(generateDtoEntities.Collect())
            .Select(static (pair, _) => pair.Left.Concat(pair.Right)
                .GroupBy(dto => dto.EntityTypeName)
                .Select(group => group.First())
                .ToImmutableArray());

        // Discover chain usage patterns in code
        var chainUses = ChainUseDiscovery.Configure(context);

        // Combine EF model with discovered DTOs and chain usage
        var combined = efModel
            .Combine(allDtos)
            .Combine(chainUses.Collect())
            .Select(static (pair, _) => new { 
                Model = pair.Left.Left, 
                Dtos = pair.Left.Right, 
                ChainUses = pair.Right 
            });

        // Generate code
        context.RegisterSourceOutput(combined, static (context, data) =>
        {
            var efModel = data.Model;
            var facetDtos = data.Dtos;
            var chainUses = data.ChainUses;
            if (efModel == null || facetDtos.Length == 0) return;

            // Apply depth capping with diagnostics
            var usedChains = ChainUseDiscovery.GroupAndNormalizeWithDepthCapping(chainUses, context);

            try
            {
                // Generate shape interfaces (base properties only - always linear)
                ShapeInterfacesEmitter.Emit(context, efModel, facetDtos);

                // Generate capability interfaces (for navigation inclusion - always linear)
                CapabilityInterfacesEmitter.Emit(context, efModel, facetDtos);

                // Generate fluent builders (conditional on discovered chains)
                FluentBuilderEmitter.Emit(context, efModel, facetDtos, usedChains);

                // Generate selectors with EF includes (conditional on discovered chains)
                SelectorsEmitter.Emit(context, efModel, facetDtos, usedChains);
            }
            catch (System.Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.GenerationError,
                    Location.None,
                    ex.Message));
            }
        });
    }
}

/// <summary>
/// Information about a discovered Facet DTO.
/// </summary>
internal sealed class FacetDtoInfo
{
    public string EntityTypeName { get; }
    public string DtoTypeName { get; }
    public string DtoNamespace { get; }
    public ImmutableArray<DtoPropertyInfo> Properties { get; }
    public ImmutableArray<string> TypeScriptAttributes { get; }

    private FacetDtoInfo(string entityTypeName, string dtoTypeName, string dtoNamespace, ImmutableArray<DtoPropertyInfo> properties, ImmutableArray<string> typeScriptAttributes = default)
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

        var entityTypeName = entitySymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var dtoTypeName = dtoSymbol.Name;
        var dtoNamespace = dtoSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        // Analyze DTO properties for projection mapping
        var properties = AnalyzeDtoProperties(dtoSymbol);
        
        // For FacetAttribute, no TypeScript attributes by default
        var tsAttributes = ImmutableArray<string>.Empty;
        
        return new FacetDtoInfo(entityTypeName!, dtoTypeName, dtoNamespace, properties, tsAttributes);
    }

    public static FacetDtoInfo? TryCreateFromGenerateDtos(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol entitySymbol)
            return null;

        // For GenerateDtosAttribute, the entity itself is the source, and we'll generate DTOs for it
        // Remove the "global::" prefix that FullyQualifiedFormat adds
        var entityTypeName = entitySymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");
        var entityName = entitySymbol.Name;
        var entityNamespace = entitySymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        
        // We'll use the entity name as the DTO type name (the actual DTOs will be generated by the main Facet generator)
        // This entry just tells the EF generator that this entity should have fluent builders generated
        var dtoTypeName = entityName;
        var dtoNamespace = entityNamespace;

        // For GenerateDtos entities, analyze the entity properties to understand what will be generated
        var properties = AnalyzeEntityProperties(entitySymbol);
        
        // Extract TypeScript attributes from GenerateDtosAttribute
        var tsAttributes = ExtractTypeScriptAttributes(context);
        
        return new FacetDtoInfo(entityTypeName, dtoTypeName, dtoNamespace, properties, tsAttributes);
    }
    
    private static ImmutableArray<DtoPropertyInfo> AnalyzeDtoProperties(INamedTypeSymbol dtoSymbol)
    {
        var properties = ImmutableArray.CreateBuilder<DtoPropertyInfo>();
        
        // Analyze all public properties and fields
        foreach (var member in dtoSymbol.GetMembers())
        {
            if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public)
            {
                var isNavigation = IsNavigationProperty(prop);
                properties.Add(new DtoPropertyInfo(prop.Name, prop.Type.ToDisplayString(), isNavigation));
            }
            else if (member is IFieldSymbol field && field.DeclaredAccessibility == Accessibility.Public)
            {
                var isNavigation = IsNavigationProperty(field);
                properties.Add(new DtoPropertyInfo(field.Name, field.Type.ToDisplayString(), isNavigation));
            }
        }
        
        return properties.ToImmutable();
    }
    
    private static ImmutableArray<DtoPropertyInfo> AnalyzeEntityProperties(INamedTypeSymbol entitySymbol)
    {
        var properties = ImmutableArray.CreateBuilder<DtoPropertyInfo>();
        
        // For entities with GenerateDtos, analyze the entity properties that would be projected
        foreach (var member in entitySymbol.GetMembers())
        {
            if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public)
            {
                var isNavigation = IsNavigationProperty(prop);
                // For entity analysis, we include all properties that would be projected to DTOs
                if (!isNavigation) // Only include scalar properties for basic mapping
                {
                    properties.Add(new DtoPropertyInfo(prop.Name, prop.Type.ToDisplayString(), isNavigation));
                }
            }
        }
        
        return properties.ToImmutable();
    }
    
    private static bool IsNavigationProperty(ISymbol symbol)
    {
        if (symbol is IPropertySymbol prop)
        {
            var typeName = prop.Type.ToDisplayString();
            // Simple heuristic: navigation properties are typically complex types or collections
            return typeName.Contains("ICollection") || typeName.Contains("List") ||
                   (!IsScalarType(typeName) && !typeName.Contains("string") && !typeName.Contains("Guid"));
        }
        if (symbol is IFieldSymbol field)
        {
            var typeName = field.Type.ToDisplayString();
            return typeName.Contains("ICollection") || typeName.Contains("List") ||
                   (!IsScalarType(typeName) && !typeName.Contains("string") && !typeName.Contains("Guid"));
        }
        return false;
    }
    
    private static bool IsScalarType(string typeName)
    {
        // Simple list of scalar types that are typically not navigation properties
        var scalarTypes = new[] { "int", "long", "decimal", "double", "float", "bool", "DateTime", "DateTimeOffset", "byte" };
        return scalarTypes.Any(t => typeName.Contains(t));
    }
    
    private static ImmutableArray<string> ExtractTypeScriptAttributes(GeneratorAttributeSyntaxContext context)
    {
        var attributes = ImmutableArray.CreateBuilder<string>();
        
        // Look for TypeScriptAttributes property in GenerateDtosAttribute
        foreach (var attribute in context.Attributes)
        {
            if (attribute.AttributeClass?.Name == "GenerateDtosAttribute" || 
                attribute.AttributeClass?.Name == "GenerateAuditableDtosAttribute")
            {
                // Look for TypeScriptAttributes named parameter
                foreach (var namedArg in attribute.NamedArguments)
                {
                    if (namedArg.Key == "TypeScriptAttributes" && namedArg.Value.Values != null)
                    {
                        foreach (var value in namedArg.Value.Values)
                        {
                            if (value.Value is string tsAttr)
                            {
                                attributes.Add(tsAttr);
                            }
                        }
                    }
                }
            }
        }
        
        return attributes.ToImmutable();
    }
}

/// <summary>
/// Information about a property in a DTO.
/// </summary>
internal sealed class DtoPropertyInfo
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