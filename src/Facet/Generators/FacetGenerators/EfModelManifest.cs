using System;
using System.Collections.Generic;
using System.Linq;

namespace Facet.Generators;

/// <summary>
/// The parsed content of Facet EF model manifest files (<c>*.facetmodel</c>), written beside
/// the model snapshot by Facet.Extensions.EFCore whenever a migration is added or removed and
/// exposed to the generator as AdditionalFiles. For each entity CLR type it records the
/// keep-set: property names EF maps as data (scalar columns and complex/value-object members).
/// When a [GenerateDtos] source type appears here, ExcludeNavigationProperties keeps exactly
/// the keep-set — navigations, skip navigations, owned references, and EF-ignored properties
/// all drop because the model says so, not because a heuristic guessed. Types not listed fall
/// back to the symbol heuristic.
/// </summary>
/// <remarks>
/// The format is line-based (<c>entity Full.Type.Name</c> / <c>scalar Name</c> /
/// <c>complex Name</c>, '#' comments, unknown record kinds ignored) so this reader needs no
/// JSON dependency — analyzers cannot assume one in every compiler host. Multiple manifest
/// files (one per DbContext) merge by unioning keep-sets per CLR type: a property mapped as
/// data in any context stays in the DTO.
/// </remarks>
internal sealed class EfModelManifest : IEquatable<EfModelManifest>
{
    /// <summary>File name suffix manifests are discovered by among AdditionalFiles.</summary>
    public const string FileExtension = ".facetmodel";

    public static readonly EfModelManifest Empty = new EfModelManifest(
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal), string.Empty);

    private readonly Dictionary<string, HashSet<string>> _keepByType;

    // Equality drives incremental caching: two manifests are the same iff their raw text is.
    private readonly string _fingerprint;

    private EfModelManifest(Dictionary<string, HashSet<string>> keepByType, string fingerprint)
    {
        _keepByType = keepByType;
        _fingerprint = fingerprint;
    }

    public bool IsEmpty => _keepByType.Count == 0;

    /// <summary>
    /// Looks up the keep-set for a CLR type name (namespace-qualified, dot-separated —
    /// matching Roslyn display strings). Returns false for types no manifest lists.
    /// </summary>
    public bool TryGetKeepSet(string clrTypeName, out HashSet<string>? keepSet)
        => _keepByType.TryGetValue(clrTypeName, out keepSet);

    public static EfModelManifest Parse(IEnumerable<string> manifestTexts)
    {
        var keepByType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        // Sorted so the fingerprint (and therefore incremental cache identity) does not
        // depend on AdditionalFiles enumeration order.
        var texts = manifestTexts.Where(t => !string.IsNullOrEmpty(t)).OrderBy(t => t, StringComparer.Ordinal).ToList();
        if (texts.Count == 0) return Empty;

        foreach (var text in texts)
        {
            ParseSingle(text, keepByType);
        }

        return new EfModelManifest(keepByType, string.Join("\n---\n", texts));
    }

    private static void ParseSingle(string text, Dictionary<string, HashSet<string>> keepByType)
    {
        HashSet<string>? current = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            var separator = line.IndexOf(' ');
            if (separator <= 0) continue;

            var kind = line.Substring(0, separator);
            var value = line.Substring(separator + 1).Trim();
            if (value.Length == 0) continue;

            switch (kind)
            {
                case "version":
                    // A future major format revision may change what existing records mean;
                    // ignoring the whole file (and falling back to the heuristic) is safer
                    // than misreading it.
                    if (value != "1") return;
                    break;

                case "entity":
                    if (!keepByType.TryGetValue(value, out current))
                    {
                        current = new HashSet<string>(StringComparer.Ordinal);
                        keepByType.Add(value, current);
                    }
                    break;

                case "scalar":
                case "complex":
                    current?.Add(value);
                    break;

                // "nav"/"owned"/"skipnav" and unknown future kinds: dropped members are
                // simply everything outside the keep-set, so only keep records are read.
                default:
                    break;
            }
        }
    }

    public bool Equals(EfModelManifest? other)
        => other != null && string.Equals(_fingerprint, other._fingerprint, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is EfModelManifest other && Equals(other);

    public override int GetHashCode() => _fingerprint.GetHashCode();
}
