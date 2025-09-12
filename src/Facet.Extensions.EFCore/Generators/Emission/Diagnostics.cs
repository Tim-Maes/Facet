using Microsoft.CodeAnalysis;

namespace Facet.Extensions.EFCore.Generators.Emission;

/// <summary>
/// Diagnostic descriptors for the Facet EF generator.
/// </summary>
internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor InvalidModel = new DiagnosticDescriptor(
        "FACET001",
        "Invalid EF model",
        "EF model validation failed: {0}",
        "Facet",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GenerationError = new DiagnosticDescriptor(
        "FACET002",
        "Code generation error",
        "Code generation failed: {0}",
        "Facet",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ChainDiscoveryDebug = new DiagnosticDescriptor(
        "FACET003",
        "Chain discovery debug",
        "Found {0} {1}: {2}",
        "Facet",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}