using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Facet.Generators.Shared;

/// <summary>
/// The reference-free trail a facet assembly leaves describing which facet types it declares and
/// what each was shaped from.
/// </summary>
/// <remarks>
/// <para>
/// A contract assembly built with <c>Facet_EmitAttributeMetadata=false</c> has no <c>[Facet]</c>
/// attributes in its metadata — that is the point, since the <c>typeof</c> in one is what pins a
/// reference to the source layer. But it also means nothing downstream can tell that its types are
/// facets at all: an analyzer looking for facets without mappers has nothing to enumerate, and the
/// mapper generator cannot discover pairs on its own.
/// </para>
/// <para>
/// So the pairs are recorded as <see cref="System.Reflection.AssemblyMetadataAttribute"/> entries,
/// which carry two strings and name no types:
/// </para>
/// <code>
/// [assembly: AssemblyMetadata("Facet.Contract.ApiModels.UserApiModel", "AppLayer.UserUseCaseModel")]
/// </code>
/// <para>
/// <c>AssemblyMetadataAttribute</c> lives in the framework every project already references, so
/// this adds no dependency of any kind, and Roslyn can read assembly attributes off a referenced
/// assembly. That makes the manifest readable by <c>[assembly: FacetMappersForAssembly]</c> (to register
/// every pair without restating it) and by the coverage analyzer (to report the ones left behind).
/// </para>
/// </remarks>
internal static class ContractManifest
{
    /// <summary>
    /// Prefix of the <c>AssemblyMetadata</c> key. The facet type's fully qualified name follows it.
    /// </summary>
    public const string KeyPrefix = "Facet.Contract.";

    public const string AssemblyMetadataAttributeFullName = "System.Reflection.AssemblyMetadataAttribute";

    /// <summary>
    /// Builds the manifest file for a set of (facet, source) pairs, or null when there is nothing
    /// to record.
    /// </summary>
    public static string? Generate(IEnumerable<(string FacetFullName, string SourceFullName)> pairs)
    {
        var entries = new SortedSet<string>();

        foreach (var (facetFullName, sourceFullName) in pairs)
        {
            if (string.IsNullOrWhiteSpace(facetFullName) || string.IsNullOrWhiteSpace(sourceFullName))
                continue;

            entries.Add(
                $"[assembly: global::System.Reflection.AssemblyMetadata(" +
                $"\"{KeyPrefix}{facetFullName}\", \"{sourceFullName}\")]");
        }

        if (entries.Count == 0)
            return null;

        var sb = new StringBuilder();
        GeneratorUtilities.AppendGeneratedFileHeader(sb);
        sb.AppendLine("// Records which facet types this assembly declares and what each was shaped from, as");
        sb.AppendLine("// plain strings that name no types — so the record survives");
        sb.AppendLine("// Facet_EmitAttributeMetadata=false without reintroducing a reference to the source layer.");
        sb.AppendLine("// Read by [assembly: FacetMappersForAssembly] and by the FAC115 coverage analyzer.");
        sb.AppendLine("// Set <Facet_EmitContractManifest>false</Facet_EmitContractManifest> to omit it.");
        sb.AppendLine();

        foreach (var entry in entries)
            sb.AppendLine(entry);

        return sb.ToString();
    }

    /// <summary>
    /// Reads the manifest an assembly carries, as (facet, source) fully qualified name pairs.
    /// </summary>
    /// <remarks>
    /// One facet may appear more than once: <c>[Facet]</c> allows multiple sources per target, and
    /// <c>AssemblyMetadataAttribute</c> allows repeated keys.
    /// </remarks>
    public static ImmutableArray<(string FacetFullName, string SourceFullName)> Read(IAssemblySymbol assembly)
    {
        var builder = ImmutableArray.CreateBuilder<(string, string)>();

        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != AssemblyMetadataAttributeFullName)
                continue;

            if (attribute.ConstructorArguments.Length != 2)
                continue;

            if (attribute.ConstructorArguments[0].Value is not string key
                || attribute.ConstructorArguments[1].Value is not string sourceFullName)
                continue;

            if (!key.StartsWith(KeyPrefix, System.StringComparison.Ordinal))
                continue;

            var facetFullName = key.Substring(KeyPrefix.Length);
            if (facetFullName.Length > 0 && sourceFullName.Length > 0)
                builder.Add((facetFullName, sourceFullName));
        }

        return builder.ToImmutable();
    }
}
