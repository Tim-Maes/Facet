using Microsoft.CodeAnalysis;

namespace Facet.Extensions.EFCore.Generators.Emission;

/// <summary>
/// Diagnostic descriptors for the Facet EF Core generator.
/// </summary>
internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor GenerationError = new(
        id: "FACET_EF001",
        title: "Facet EF Core generation error",
        messageFormat: "Error during Facet EF Core code generation: {0}",
        category: "Facet.Extensions.EFCore",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidModel = new(
        id: "FACET_EF002", 
        title: "Invalid EF model",
        messageFormat: "Invalid EF model data: {0}",
        category: "Facet.Extensions.EFCore",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DtoNotFound = new(
        id: "FACET_EF003",
        title: "DTO not found",
        messageFormat: "Could not find DTO type '{0}' for entity '{1}'",
        category: "Facet.Extensions.EFCore", 
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}