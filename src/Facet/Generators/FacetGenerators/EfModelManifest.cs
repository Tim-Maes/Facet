using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;

namespace Facet.Generators;

/// <summary>
/// The parsed content of Facet EF model manifest files (<c>*.facetmodel.json</c>), written
/// beside the model snapshot by Facet.Extensions.EFCore whenever a migration is added or
/// removed and exposed to the generator as AdditionalFiles. For each entity CLR type it
/// records the keep-set: property names EF maps as data (scalar columns and complex/
/// value-object members). When a [GenerateDtos] source type appears here,
/// ExcludeNavigationProperties keeps exactly the keep-set — navigations, skip navigations,
/// owned references, and EF-ignored properties all drop because the model says so, not
/// because a heuristic guessed. Types not listed fall back to the symbol heuristic.
/// </summary>
/// <remarks>
/// See <c>FacetEfModelManifest</c> in Facet.Extensions.EFCore for the writer and the schema.
/// Each file is parsed atomically: a manifest that is malformed or declares an unsupported
/// <c>version</c> is ignored in full (heuristic fallback) — never half-applied, because an
/// entity left behind with an empty keep-set would silently drop every property. Multiple
/// manifest files (one per DbContext) merge by unioning keep-sets per CLR type: a property
/// mapped as data in any context stays in the DTO. Unknown JSON properties are ignored, so
/// the schema can grow without breaking older generators.
/// </remarks>
internal sealed class EfModelManifest : IEquatable<EfModelManifest>
{
    /// <summary>File name suffix manifests are discovered by among AdditionalFiles.</summary>
    public const string FileExtension = ".facetmodel.json";

    private const int SupportedVersion = 1;

    public static readonly EfModelManifest Empty = new EfModelManifest(
        ImmutableDictionary<string, ImmutableHashSet<string>>.Empty);

    private readonly ImmutableDictionary<string, ImmutableHashSet<string>> _keepByType;

    private EfModelManifest(ImmutableDictionary<string, ImmutableHashSet<string>> keepByType)
    {
        _keepByType = keepByType;
    }

    /// <summary>Number of entity CLR types listed across all accepted manifest files.</summary>
    public int EntityCount => _keepByType.Count;

    /// <summary>
    /// Looks up the keep-set for a CLR type name (namespace-qualified, dot-separated —
    /// matching Roslyn display strings). Returns false for types no manifest lists. An empty
    /// keep-set is a valid answer: an entity whose only mapped data lives in shadow state
    /// keeps no CLR properties.
    /// </summary>
    public bool TryGetKeepSet(string clrTypeName, out ImmutableHashSet<string>? keepSet)
        => _keepByType.TryGetValue(clrTypeName, out keepSet);

    public static EfModelManifest Parse(IEnumerable<string> manifestTexts)
    {
        var merged = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var text in manifestTexts)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            var single = ParseSingle(text);
            if (single == null) continue;

            foreach (var entity in single)
            {
                if (merged.TryGetValue(entity.Key, out var existing))
                {
                    existing.UnionWith(entity.Value);
                }
                else
                {
                    merged.Add(entity.Key, entity.Value);
                }
            }
        }

        if (merged.Count == 0) return Empty;

        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>(StringComparer.Ordinal);
        foreach (var entity in merged)
        {
            builder.Add(entity.Key, entity.Value.ToImmutableHashSet(StringComparer.Ordinal));
        }

        return new EfModelManifest(builder.ToImmutable());
    }

    /// <summary>
    /// Parses one manifest file into a local buffer, returning null — file ignored in full —
    /// for malformed JSON, a non-object root, or an unsupported <c>version</c>.
    /// </summary>
    private static Dictionary<string, HashSet<string>>? ParseSingle(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            if (!root.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var versionNumber)
                || versionNumber != SupportedVersion)
            {
                return null;
            }

            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (!root.TryGetProperty("entities", out var entities) || entities.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var entity in entities.EnumerateArray())
            {
                if (entity.ValueKind != JsonValueKind.Object) continue;
                if (!entity.TryGetProperty("clrType", out var clrTypeProperty)
                    || clrTypeProperty.ValueKind != JsonValueKind.String) continue;

                var clrType = clrTypeProperty.GetString();
                if (string.IsNullOrEmpty(clrType)) continue;

                if (!result.TryGetValue(clrType!, out var keepSet))
                {
                    keepSet = new HashSet<string>(StringComparer.Ordinal);
                    result.Add(clrType!, keepSet);
                }

                // Only keep categories are read: dropped members are simply everything
                // outside the keep-set, so "nav"/"owned"/"skipnav" need no interpretation.
                AddMembers(entity, "scalar", keepSet);
                AddMembers(entity, "complex", keepSet);
            }

            return result;
        }
        catch (JsonException)
        {
            // A manifest that does not parse is ignored (heuristic fallback) rather than
            // failing or degrading the build.
            return null;
        }
    }

    private static void AddMembers(JsonElement entity, string category, HashSet<string> keepSet)
    {
        if (!entity.TryGetProperty(category, out var members) || members.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var member in members.EnumerateArray())
        {
            if (member.ValueKind != JsonValueKind.String) continue;
            var name = member.GetString();
            if (!string.IsNullOrEmpty(name)) keepSet.Add(name!);
        }
    }

    /// <summary>
    /// Structural equality (same types, same keep-sets) drives incremental caching, so
    /// formatting-only manifest edits — comments, ordering, whitespace — do not invalidate
    /// cached generator output.
    /// </summary>
    public bool Equals(EfModelManifest? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_keepByType.Count != other._keepByType.Count) return false;

        foreach (var entity in _keepByType)
        {
            if (!other._keepByType.TryGetValue(entity.Key, out var otherKeepSet)) return false;
            if (!entity.Value.SetEquals(otherKeepSet)) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EfModelManifest other && Equals(other);

    public override int GetHashCode()
    {
        // Order-independent aggregation: dictionaries and sets have no defined enumeration
        // order, and equal manifests must hash identically.
        unchecked
        {
            var hash = 0;
            foreach (var entity in _keepByType)
            {
                var setHash = 0;
                foreach (var member in entity.Value)
                {
                    setHash += member.GetHashCode();
                }

                hash += entity.Key.GetHashCode() * 397 ^ setHash;
            }

            return hash;
        }
    }
}
