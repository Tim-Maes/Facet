; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FAC023 | Usage | Warning | GenerateToSource is set to true, but ToSource cannot be generated
FAC025 | Performance | Warning | MaxDepthToSource value is unusual
FAC101 | Generator | Error | GenerateDtos OutputType combines multiple concrete output kinds
FAC102 | Generator | Error | GenerateDtos OutputType sets the Partial modifier without an output kind
FAC103 | Generator | Error | EF model manifest could not be read
FAC104 | Generator | Error | EF model manifest version is not supported
FAC105 | Generator | Error | GenerateDtos source type is not in the EF model manifest (no heuristic fallback)
FAC106 | Generator | Warning | Property is unknown to the EF model manifest
FAC116 | Usage | Warning | Contract facet exposes a type from the layer it was shaped from
FAC117 | Usage | Warning | Facet mapping hook cannot run because mappings are suppressed
FAC110 | Generator | Warning | Facet mapper leaves a property unmapped
FAC111 | Generator | Error | Facet mapper registration is not usable
FAC112 | Generator | Warning | Facet mapper cannot generate a reverse mapping
FAC113 | Generator | Warning | Two Facet mappers would generate the same class
FAC114 | Generator | Warning | Facet mapper cannot see a facet declared in the same compilation
FAC115 | Coverage | Warning | Facet has no mapper in an assembly that maps its contract assembly
