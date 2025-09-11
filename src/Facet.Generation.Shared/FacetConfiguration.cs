using Microsoft.CodeAnalysis.Diagnostics;

namespace Facet.Generation.Shared;

public sealed class FacetConfiguration
{
    public int MaxChainDepth { get; }
    public bool EnableDebugOutput { get; }
    public bool EmitBuildersFromGenerateDtos { get; }

    public FacetConfiguration(AnalyzerConfigOptionsProvider optionsProvider)
    {
        var maxChainDepthValue = TryGetGlobalOption(optionsProvider, "build_property.FacetMaxChainDepth");
        MaxChainDepth = int.TryParse(maxChainDepthValue, out var depth) && depth > 0 ? depth : 3;
        var debugOutputValue = TryGetGlobalOption(optionsProvider, "build_property.FacetEnableDebugOutput");
        EnableDebugOutput = bool.TryParse(debugOutputValue, out var enableDebug) && enableDebug;
        var emitBuilders = TryGetGlobalOption(optionsProvider, "build_property.FacetEmitBuildersFromGenerateDtos");
        EmitBuildersFromGenerateDtos = bool.TryParse(emitBuilders, out var emit) && emit;
    }

    private static string? TryGetGlobalOption(AnalyzerConfigOptionsProvider optionsProvider, string key)
        => optionsProvider.GlobalOptions.TryGetValue(key, out var value) ? value : null;
}
