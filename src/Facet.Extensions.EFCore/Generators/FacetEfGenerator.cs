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

        // Combine EF model with discovered DTOs
        var combined = efModel
            .Combine(allDtos)
            .Select(static (pair, _) => new { Model = pair.Left, Dtos = pair.Right });

        // Generate code
        context.RegisterSourceOutput(combined, static (context, data) =>
        {
            var efModel = data.Model;
            var facetDtos = data.Dtos;
            if (efModel == null || facetDtos.Length == 0) return;

            try
            {
                // Generate shape interfaces (base properties only)
                ShapeInterfacesEmitter.Emit(context, efModel, facetDtos);

                // Generate capability interfaces (for navigation inclusion)
                CapabilityInterfacesEmitter.Emit(context, efModel, facetDtos);

                // Generate fluent builders
                FluentBuilderEmitter.Emit(context, efModel, facetDtos);

                // Generate selectors with EF includes
                SelectorsEmitter.Emit(context, efModel, facetDtos);
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

    private FacetDtoInfo(string entityTypeName, string dtoTypeName, string dtoNamespace)
    {
        EntityTypeName = entityTypeName;
        DtoTypeName = dtoTypeName;
        DtoNamespace = dtoNamespace;
    }

    public static FacetDtoInfo? TryCreate(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol dtoSymbol)
            return null;

        var facetAttribute = context.Attributes.FirstOrDefault();
        if (facetAttribute?.ConstructorArguments.Length == 0)
            return null;

        var entityTypeArg = facetAttribute.ConstructorArguments[0];
        if (entityTypeArg.Value is not INamedTypeSymbol entitySymbol)
            return null;

        var entityTypeName = entitySymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var dtoTypeName = dtoSymbol.Name;
        var dtoNamespace = dtoSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        return new FacetDtoInfo(entityTypeName!, dtoTypeName, dtoNamespace);
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

        return new FacetDtoInfo(entityTypeName, dtoTypeName, dtoNamespace);
    }
}