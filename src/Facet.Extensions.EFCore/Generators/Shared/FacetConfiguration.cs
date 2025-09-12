using Microsoft.CodeAnalysis.Diagnostics;

namespace Facet.Extensions.EFCore.Generators.Shared;

/// <summary>
/// Configuration for Facet code generation.
/// </summary>
public class FacetConfiguration
{
    public bool EnableDebugOutput { get; set; }
    public int MaxChainDepth { get; set; } = 3;

    public FacetConfiguration(AnalyzerConfigOptionsProvider optionsProvider)
    {
        // Read configuration from MSBuild properties
        if (optionsProvider.GlobalOptions.TryGetValue("build_property.FacetEnableDebugOutput", out var debugValue))
        {
            EnableDebugOutput = bool.TryParse(debugValue, out var debug) && debug;
        }

        if (optionsProvider.GlobalOptions.TryGetValue("build_property.FacetMaxChainDepth", out var depthValue))
        {
            if (int.TryParse(depthValue, out var depth) && depth > 0)
            {
                MaxChainDepth = depth;
            }
        }
    }
}