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
FAC105 | Generator | Warning | GenerateDtos source type is not in the EF model manifest
FAC106 | Generator | Warning | Property is unknown to the EF model manifest
