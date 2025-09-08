using System.Linq;
using Microsoft.CodeAnalysis;

namespace Facet.UnitTests.SourceGeneration;

/// <summary>
/// Represents a simplified snapshot of generated source code for verification.
/// Contains only the essential information needed for testing, without metadata bloat.
/// </summary>
public record GeneratedSourceSnapshot
{
    public required string HintName { get; init; }
    public required string Source { get; init; }
}

/// <summary>
/// Helper utilities for source generator testing.
/// </summary>
public static class SourceGeneratorTestHelpers
{
    /// <summary>
    /// Extracts just the generated source code content from the generator run result,
    /// excluding all the metadata and syntax tree information that clutters test snapshots.
    /// </summary>
    /// <param name="runResult">The generator driver run result</param>
    /// <returns>Array of simplified source snapshots containing only the generated C# code</returns>
    public static GeneratedSourceSnapshot[] ExtractGeneratedSources(GeneratorDriverRunResult runResult)
    {
        return runResult.Results
            .SelectMany(result => result.GeneratedSources)
            .Select(source => new GeneratedSourceSnapshot
            {
                HintName = source.HintName,
                Source = source.SourceText.ToString()
            })
            .ToArray();
    }
}