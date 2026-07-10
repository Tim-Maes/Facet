using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;

namespace Facet.Generators;

/// <summary>
/// The parsed content of Facet EF model manifest files (<c>*.facetmodel.json</c>), written
/// beside the model snapshot by Facet.Extensions.EFCore whenever a migration is added or
/// removed and exposed to the generator as AdditionalFiles. For each entity CLR type it
/// records two sets: the keep-set (property names EF maps as data — scalar columns and
/// complex/value-object members) and the known-set (every member the model has an opinion
/// on: kept, navigation, owned, skip navigation, explicitly ignored, or a service property).
/// When a [GenerateDtos] source type appears here, ExcludeNavigationProperties keeps exactly
/// the keep-set; a settable property outside the known-set means the manifest predates the
/// property and is reported as FAC106 rather than silently mis-shaping the DTO. Wiring any
/// manifest into a project also flips the ExcludeNavigationProperties default to true there
/// (see <see cref="HasAcceptedManifests"/>) — attributes opt out per type with an explicit
/// <c>ExcludeNavigationProperties = false</c>.
/// </summary>
/// <remarks>
/// See <c>FacetEfModelManifest</c> in Facet.Extensions.EFCore for the writer and the schema.
/// Each file is parsed atomically: a manifest that is malformed or declares an unsupported
/// <c>version</c> contributes nothing (never a half-applied entity whose empty keep-set
/// would drop every property) and surfaces a <see cref="ManifestIssue"/> that the generator
/// reports as FAC103/FAC104 — failures are loud, not silent fallbacks. Multiple manifest
/// files (one per DbContext) merge by unioning sets per CLR type. Unknown JSON properties
/// are ignored, so the schema can grow without breaking older generators.
/// </remarks>
internal sealed class EfModelManifest : IEquatable<EfModelManifest>
{
    /// <summary>File name suffix manifests are discovered by among AdditionalFiles.</summary>
    public const string FileExtension = ".facetmodel.json";

    private const int SupportedVersion = 1;

    private static readonly string[] KeepCategories = { "scalar", "complex" };
    private static readonly string[] DropCategories = { "nav", "owned", "skipnav", "ignored", "service" };

    public static readonly EfModelManifest Empty = new EfModelManifest(
        ImmutableDictionary<string, ManifestEntity>.Empty,
        ImmutableArray<ManifestIssue>.Empty,
        hasAcceptedManifests: false);

    private readonly ImmutableDictionary<string, ManifestEntity> _entities;

    private EfModelManifest(ImmutableDictionary<string, ManifestEntity> entities, ImmutableArray<ManifestIssue> issues, bool hasAcceptedManifests)
    {
        _entities = entities;
        Issues = issues;
        HasAcceptedManifests = hasAcceptedManifests;
    }

    /// <summary>Files that were rejected in full, for the generator to report (FAC103/FAC104).</summary>
    public ImmutableArray<ManifestIssue> Issues { get; }

    /// <summary>Number of entity CLR types listed across all accepted manifest files.</summary>
    public int EntityCount => _entities.Count;

    /// <summary>All entity CLR type names, for consumers that sweep the model (fluent queries).</summary>
    public IEnumerable<string> EntityClrNames => _entities.Keys;

    /// <summary>
    /// Whether any manifest file parsed successfully, entities or not. Wiring a manifest into
    /// the compilation is the project-level opt-in that flips the ExcludeNavigationProperties
    /// default to true for attributes that leave it unset. Deliberately false when every
    /// supplied file was rejected: those already fail the build as FAC103/FAC104, and flipping
    /// the default on top would bury the real error under a cascade of FAC105s. A bool rather
    /// than a file count — behavior only depends on presence, and equality (which drives
    /// incremental caching) must not invalidate on a content-preserving file consolidation.
    /// </summary>
    public bool HasAcceptedManifests { get; }

    /// <summary>
    /// Looks up the manifest entry for a CLR type name (namespace-qualified, dot-separated —
    /// matching Roslyn display strings). Returns false for types no manifest lists. An empty
    /// keep-set is a valid answer: an entity whose only mapped data lives in shadow state
    /// keeps no CLR properties.
    /// </summary>
    public bool TryGetEntity(string clrTypeName, out ManifestEntity? entity)
        => _entities.TryGetValue(clrTypeName, out entity);

    public static EfModelManifest Parse(IEnumerable<(string Path, string Text)> manifestFiles)
    {
        var merged = new Dictionary<string, MutableEntity>(StringComparer.Ordinal);
        var issues = ImmutableArray.CreateBuilder<ManifestIssue>();
        var acceptedFileCount = 0;

        // Sorted by path so merge results and issue order do not depend on
        // AdditionalFiles enumeration order.
        foreach (var file in manifestFiles.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(file.Text))
            {
                issues.Add(new ManifestIssue(file.Path, ManifestIssueKind.Malformed, "the file is empty"));
                continue;
            }

            var issue = ParseSingle(file.Path, file.Text, merged);
            if (issue != null) issues.Add(issue);
            else acceptedFileCount++;
        }

        if (acceptedFileCount == 0 && issues.Count == 0) return Empty;

        var builder = ImmutableDictionary.CreateBuilder<string, ManifestEntity>(StringComparer.Ordinal);
        foreach (var entity in merged)
        {
            builder.Add(entity.Key, new ManifestEntity(
                entity.Value.Keep.ToImmutableHashSet(StringComparer.Ordinal),
                entity.Value.Known.ToImmutableHashSet(StringComparer.Ordinal),
                entity.Value.Chainable.ToImmutableHashSet(StringComparer.Ordinal),
                entity.Value.OptionalNavs.ToImmutableHashSet(StringComparer.Ordinal),
                entity.Value.KeyConflicted || entity.Value.Key == null
                    ? ImmutableArray<string>.Empty
                    : entity.Value.Key.ToImmutableArray()));
        }

        return new EfModelManifest(builder.ToImmutable(), issues.ToImmutable(), acceptedFileCount > 0);
    }

    /// <summary>
    /// Parses one manifest file into a local buffer that is merged only on success; a
    /// malformed or unsupported-version file contributes nothing and returns its issue.
    /// </summary>
    private static ManifestIssue? ParseSingle(string path, string text, Dictionary<string, MutableEntity> merged)
    {
        Dictionary<string, MutableEntity> local;
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new ManifestIssue(path, ManifestIssueKind.Malformed, "the root is not a JSON object");
            }

            if (!root.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var versionNumber))
            {
                return new ManifestIssue(path, ManifestIssueKind.Malformed, "the 'version' property is missing or not an integer");
            }

            if (versionNumber != SupportedVersion)
            {
                return new ManifestIssue(path, ManifestIssueKind.UnsupportedVersion,
                    $"version {versionNumber} (this generator reads version {SupportedVersion})");
            }

            local = new Dictionary<string, MutableEntity>(StringComparer.Ordinal);
            if (root.TryGetProperty("entities", out var entities) && entities.ValueKind == JsonValueKind.Array)
            {
                foreach (var entity in entities.EnumerateArray())
                {
                    if (entity.ValueKind != JsonValueKind.Object) continue;
                    if (!entity.TryGetProperty("clrType", out var clrTypeProperty)
                        || clrTypeProperty.ValueKind != JsonValueKind.String) continue;

                    var clrType = clrTypeProperty.GetString();
                    if (string.IsNullOrEmpty(clrType)) continue;

                    if (!local.TryGetValue(clrType!, out var record))
                    {
                        record = new MutableEntity();
                        local.Add(clrType!, record);
                    }

                    foreach (var category in KeepCategories)
                    {
                        AddMembers(entity, category, record.Keep);
                        AddMembers(entity, category, record.Known);
                    }

                    foreach (var category in DropCategories)
                    {
                        AddMembers(entity, category, record.Known);
                    }

                    // Fluent-navigation metadata (additive fields; absent in older
                    // manifests). Chainable = plain + skip navigations; owned references
                    // are not chainable. navOptional marks reference navs that may be
                    // null; "key" is ordered, so it is a list, never a set.
                    AddMembers(entity, "nav", record.Chainable);
                    AddMembers(entity, "skipnav", record.Chainable);
                    AddMembers(entity, "navOptional", record.OptionalNavs);
                    if (entity.TryGetProperty("key", out var keyElement) && keyElement.ValueKind == JsonValueKind.Array)
                    {
                        var keyMembers = new List<string>();
                        foreach (var member in keyElement.EnumerateArray())
                        {
                            if (member.ValueKind != JsonValueKind.String) continue;
                            var name = member.GetString();
                            if (!string.IsNullOrEmpty(name)) keyMembers.Add(name!);
                        }

                        record.MergeKey(keyMembers);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            return new ManifestIssue(path, ManifestIssueKind.Malformed, ex.Message);
        }

        foreach (var entity in local)
        {
            if (merged.TryGetValue(entity.Key, out var existing))
            {
                existing.Keep.UnionWith(entity.Value.Keep);
                existing.Known.UnionWith(entity.Value.Known);
                existing.Chainable.UnionWith(entity.Value.Chainable);
                existing.OptionalNavs.UnionWith(entity.Value.OptionalNavs);
                if (entity.Value.Key != null)
                {
                    existing.MergeKey(entity.Value.Key);
                }
            }
            else
            {
                merged.Add(entity.Key, entity.Value);
            }
        }

        return null;
    }

    private static void AddMembers(JsonElement entity, string category, HashSet<string> target)
    {
        if (!entity.TryGetProperty(category, out var members) || members.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var member in members.EnumerateArray())
        {
            if (member.ValueKind != JsonValueKind.String) continue;
            var name = member.GetString();
            if (!string.IsNullOrEmpty(name)) target.Add(name!);
        }
    }

    private sealed class MutableEntity
    {
        public HashSet<string> Keep { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> Known { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> Chainable { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> OptionalNavs { get; } = new HashSet<string>(StringComparer.Ordinal);
        public List<string>? Key { get; private set; }
        public bool KeyConflicted { get; private set; }

        /// <summary>
        /// First key wins; a later disagreeing key (same CLR type mapped with different
        /// primary keys across contexts) drops the key entirely — order matters, so a
        /// union would be wrong and a guess worse.
        /// </summary>
        public void MergeKey(List<string> key)
        {
            if (Key == null)
            {
                Key = key;
            }
            else if (!Key.SequenceEqual(key, StringComparer.Ordinal))
            {
                KeyConflicted = true;
            }
        }
    }

    /// <summary>
    /// Structural equality (same types, same sets, same issues) drives incremental caching,
    /// so formatting-only manifest edits — comments, ordering, whitespace — do not invalidate
    /// cached generator output.
    /// </summary>
    public bool Equals(EfModelManifest? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (HasAcceptedManifests != other.HasAcceptedManifests) return false;
        if (_entities.Count != other._entities.Count) return false;
        if (!Issues.SequenceEqual(other.Issues)) return false;

        foreach (var entity in _entities)
        {
            if (!other._entities.TryGetValue(entity.Key, out var otherEntity)) return false;
            if (!entity.Value.Equals(otherEntity)) return false;
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
            var hash = HasAcceptedManifests ? 1 : 0;
            foreach (var entity in _entities)
            {
                hash += entity.Key.GetHashCode() * 397 ^ entity.Value.GetHashCode();
            }

            foreach (var issue in Issues)
            {
                hash = hash * 31 + issue.GetHashCode();
            }

            return hash;
        }
    }
}

/// <summary>One entity's manifest entry: what to keep, and everything the model knows about.</summary>
internal sealed class ManifestEntity : IEquatable<ManifestEntity>
{
    public ManifestEntity(
        ImmutableHashSet<string> keep,
        ImmutableHashSet<string> known,
        ImmutableHashSet<string>? chainableNavs = null,
        ImmutableHashSet<string>? optionalNavs = null,
        ImmutableArray<string> key = default)
    {
        Keep = keep;
        Known = known;
        ChainableNavs = chainableNavs ?? ImmutableHashSet<string>.Empty;
        OptionalNavs = optionalNavs ?? ImmutableHashSet<string>.Empty;
        Key = key.IsDefault ? ImmutableArray<string>.Empty : key;
    }

    /// <summary>Property names EF maps as data (scalar + complex) — the DTO member set.</summary>
    public ImmutableHashSet<string> Keep { get; }

    /// <summary>
    /// Every member the model has an opinion on: <see cref="Keep"/> plus navigations, owned
    /// references, skip navigations, explicitly ignored members, and service properties.
    /// A settable CLR property outside this set is unknown to the model — almost always a
    /// property added after the manifest was last generated (FAC106).
    /// </summary>
    public ImmutableHashSet<string> Known { get; }

    /// <summary>
    /// Navigations the fluent query surface can include: plain + skip navigations (owned
    /// references are not chainable). Empty for manifests written before the field existed.
    /// </summary>
    public ImmutableHashSet<string> ChainableNavs { get; }

    /// <summary>
    /// Reference navigations whose relationship the model marks optional — their shape
    /// members are nullable. A chainable reference nav outside this set is required.
    /// Manifests written before the field existed leave it empty; the fluent generator then
    /// treats every reference nav as optional, which is pessimistic but never lies.
    /// </summary>
    public ImmutableHashSet<string> OptionalNavs { get; }

    /// <summary>
    /// Primary-key property names in key order; empty for keyless entities, owned types,
    /// pre-field manifests, and cross-context key conflicts. May name shadow properties.
    /// </summary>
    public ImmutableArray<string> Key { get; }

    public bool Equals(ManifestEntity? other)
        => other != null
            && Keep.SetEquals(other.Keep)
            && Known.SetEquals(other.Known)
            && ChainableNavs.SetEquals(other.ChainableNavs)
            && OptionalNavs.SetEquals(other.OptionalNavs)
            && Key.SequenceEqual(other.Key, StringComparer.Ordinal);

    public override bool Equals(object? obj) => obj is ManifestEntity other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 0;
            foreach (var member in Keep) hash += member.GetHashCode();
            foreach (var member in Known) hash += member.GetHashCode() * 397;
            foreach (var member in ChainableNavs) hash += member.GetHashCode() * 31;
            foreach (var member in OptionalNavs) hash += member.GetHashCode() * 17;
            foreach (var member in Key) hash = hash * 31 + member.GetHashCode();
            return hash;
        }
    }
}

internal enum ManifestIssueKind
{
    /// <summary>The file is not readable as a manifest (bad JSON, wrong shape). FAC103.</summary>
    Malformed,

    /// <summary>The file declares a version this generator does not read. FAC104.</summary>
    UnsupportedVersion,
}

/// <summary>A manifest file rejected in full, carried on the parsed manifest for reporting.</summary>
internal sealed class ManifestIssue : IEquatable<ManifestIssue>
{
    public ManifestIssue(string filePath, ManifestIssueKind kind, string detail)
    {
        FilePath = filePath;
        Kind = kind;
        Detail = detail;
    }

    public string FilePath { get; }
    public ManifestIssueKind Kind { get; }
    public string Detail { get; }

    public bool Equals(ManifestIssue? other)
        => other != null && FilePath == other.FilePath && Kind == other.Kind && Detail == other.Detail;

    public override bool Equals(object? obj) => obj is ManifestIssue other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = FilePath.GetHashCode();
            hash = hash * 31 + (int)Kind;
            hash = hash * 31 + Detail.GetHashCode();
            return hash;
        }
    }
}
