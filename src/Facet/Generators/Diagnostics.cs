using Microsoft.CodeAnalysis;

namespace Facet.Generators;

/// <summary>
/// Diagnostic descriptors for the Facet source generators.
/// </summary>
internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor GenerateDtosError = new(
        id: "FACET001",
        title: "GenerateDtos generation error",
        messageFormat: "Error during GenerateDtos code generation: {0}",
        category: "Facet.GenerateDtos",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GenerateDtosAttributeParsingError = new(
        id: "FACET002",
        title: "GenerateDtos attribute parsing error",
        messageFormat: "Error parsing GenerateDtos attribute on '{0}': {1}",
        category: "Facet.GenerateDtos",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GenerateDtosSymbolError = new(
        id: "FACET003",
        title: "GenerateDtos symbol analysis error",
        messageFormat: "Error analyzing symbol '{0}' for GenerateDtos: {1}",
        category: "Facet.GenerateDtos",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GenerateDtosTypeNotFound = new(
        id: "FACET004",
        title: "GenerateDtos type not found",
        messageFormat: "Could not find type information for GenerateDtos on '{0}'",
        category: "Facet.GenerateDtos",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}