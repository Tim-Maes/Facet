using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Facet.Generators.Shared;

/// <summary>
/// Decides which of a source member's attributes <c>CopyAttributes</c> carries onto the generated
/// member.
/// </summary>
/// <remarks>
/// <para>
/// Copying an attribute copies its dependency. <c>[Precision(18, 2)]</c> lives in
/// <c>Microsoft.EntityFrameworkCore.Abstractions</c>, so once it is written onto a DTO that DTO's
/// assembly carries an <c>AssemblyRef</c> to EF Core — and a consumer that reflects over the
/// property (Swagger schema generation, DataAnnotations validation, a serializer) throws
/// <c>FileNotFoundException</c> unless EF Core happens to be deployed alongside. The failure is at
/// runtime, under reflection, which is about the worst place for it to surface.
/// </para>
/// <para>
/// The built-in list is therefore exactly the attributes that can cause that: ones whose assembly
/// is a package the consumer may not have. It deliberately does <em>not</em> include attributes the
/// shared framework carries — <c>[Column]</c>, <c>[Table]</c>, <c>[Key]</c>, <c>[Timestamp]</c> and
/// the rest live alongside <c>[Required]</c> in <c>System.ComponentModel.Annotations</c>, so
/// copying them adds no reference and breaks nothing. Whether a column name belongs on a DTO is a
/// judgement for the project, not for this list, and dropping them by default would silently change
/// what existing projects generate.
/// </para>
/// <para>
/// So both directions are configurable: <c>Facet_CopyAttributesExclude</c>/
/// <c>CopyAttributesExclude</c> extend the list — which is how a project that wants its contracts
/// free of storage detail asks for <c>DataAnnotations.Schema.*</c> or <c>[Key]</c> to be dropped —
/// and <c>Facet_CopyAttributesInclude</c>/<c>CopyAttributesInclude</c> force a copy back on, over a
/// built-in entry included.
/// </para>
/// </remarks>
internal sealed class AttributeCopyPolicy
{
    /// <summary>
    /// Attributes Facet never copies, in any mode. Not configurable: copying one of these does not
    /// merely add a reference, it breaks the generated code or another generator.
    /// </summary>
    /// <remarks>
    /// This is exactly the set excluded before <see cref="AttributeCopyMode"/> existed, so
    /// <see cref="AttributeCopyMode.Preserve"/> reproduces the previous behaviour precisely. In
    /// particular <c>CommunityToolkit.Mvvm.*</c> stays here rather than moving into a configurable
    /// list, so that a project setting its own exclusions cannot accidentally drop the protection
    /// added for issue #277.
    /// </remarks>
    private static readonly string[] AlwaysExcludedNamespaces = new[]
    {
        // Compiler plumbing: never meaningful on a generated member.
        "System.Runtime.CompilerServices.",

        // Consumed by another source generator to trigger generation. Copying one onto a facet
        // produces analyzer errors, because the facet is not wired into that generator's pipeline.
        // See GitHub issue #277.
        "CommunityToolkit.Mvvm.",
    };

    /// <summary>
    /// The abstract base of the validation attributes, which is not itself applicable.
    /// </summary>
    private const string ValidationAttributeBase = "System.ComponentModel.DataAnnotations.ValidationAttribute";

    /// <summary>
    /// Namespaces skipped under <see cref="AttributeCopyMode.ContractSafe"/>, because each lives in
    /// a package rather than the shared framework — so copying one can leave the generated type
    /// unloadable for a consumer that does not reference that package.
    /// </summary>
    private static readonly string[] PackageAttributeNamespaces = new[]
    {
        // EF Core's own attributes: Precision, Comment, Index, Keyless, Owned, BackingField,
        // Unicode, DeleteBehavior. In Microsoft.EntityFrameworkCore.Abstractions — a package
        // reference, so copying one of these is what actually breaks a consumer at runtime.
        "Microsoft.EntityFrameworkCore.",

        // Other ORMs, same reasoning as EF Core.
        "System.Data.Linq.Mapping.",
        "MongoDB.Bson.Serialization.Attributes.",
        "NHibernate.Mapping.Attributes.",
        "Dapper.Contrib.Extensions.",
        "ServiceStack.DataAnnotations.",
        "LinqToDB.Mapping.",
    };

    /// <summary>
    /// Individual attributes excluded by default. Empty: every attribute worth excluding on
    /// reference grounds is covered by a namespace above, and excluding one that resolves
    /// everywhere would change what existing projects generate for no technical gain.
    /// <c>[Key]</c>, <c>[Timestamp]</c> and <c>[ConcurrencyCheck]</c> are the candidates, and a
    /// project that wants them gone names them in <c>Facet_CopyAttributesExclude</c>.
    /// </summary>
    private static readonly string[] DefaultExcludedTypes = new string[0];

    /// <summary>
    /// The policy with no configuration applied: <see cref="AttributeCopyMode.Preserve"/>, which
    /// is the behaviour Facet had before the mode existed.
    /// </summary>
    public static AttributeCopyPolicy Default { get; } =
        new AttributeCopyPolicy(AttributeCopyMode.Preserve, ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);

    private readonly AttributeCopyMode _mode;
    private readonly ImmutableArray<string> _extraExcludes;
    private readonly ImmutableArray<string> _includes;

    private AttributeCopyPolicy(
        AttributeCopyMode mode,
        ImmutableArray<string> extraExcludes,
        ImmutableArray<string> includes)
    {
        _mode = mode;
        _extraExcludes = extraExcludes;
        _includes = includes;
    }

    /// <summary>
    /// Whether <paramref name="attributeFullName"/> is one the <see cref="AttributeCopyMode.ContractSafe"/>
    /// mode skips — i.e. one whose assembly is a package the consumer may not have. Shared with the
    /// FAC109 analyzer so the diagnostic and the policy cannot disagree about which they are.
    /// </summary>
    public static bool IsPackageAttribute(string attributeFullName)
    {
        var name = GeneratorUtilities.StripGlobalPrefix(attributeFullName);

        foreach (var ns in PackageAttributeNamespaces)
        {
            if (name.StartsWith(ns, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Builds a policy from the project-wide defaults and this type's own overrides. Per-type
    /// entries are additive to the project's, so a <c>Directory.Build.props</c> rule stays in
    /// force unless a type's include list overrides a specific attribute.
    /// </summary>
    public static AttributeCopyPolicy Create(
        AttributeCopyMode mode,
        IEnumerable<string>? globalExcludes,
        IEnumerable<string>? globalIncludes,
        IEnumerable<string>? typeExcludes = null,
        IEnumerable<string>? typeIncludes = null)
    {
        var excludes = Combine(globalExcludes, typeExcludes);
        var includes = Combine(globalIncludes, typeIncludes);

        return mode == AttributeCopyMode.Preserve && excludes.IsEmpty && includes.IsEmpty
            ? Default
            : new AttributeCopyPolicy(mode, excludes, includes);
    }

    /// <summary>
    /// Whether an attribute should be copied onto the generated member.
    /// </summary>
    /// <param name="attributeFullName">
    /// The attribute type's fully qualified name, with or without the <c>global::</c> prefix.
    /// </param>
    public bool ShouldCopy(string attributeFullName)
    {
        var name = GeneratorUtilities.StripGlobalPrefix(attributeFullName);

        // Never copied, whatever the mode or the configuration says.
        foreach (var ns in AlwaysExcludedNamespaces)
        {
            if (name.StartsWith(ns, StringComparison.Ordinal))
                return false;
        }

        if (string.Equals(name, ValidationAttributeBase, StringComparison.Ordinal))
            return false;

        // An explicit include wins over everything below: that is the whole point of having it.
        if (MatchesAny(_includes, name))
            return true;

        if (MatchesAny(_extraExcludes, name))
            return false;

        if (_mode == AttributeCopyMode.ContractSafe && IsPackageAttribute(name))
            return false;

        foreach (var type in DefaultExcludedTypes)
        {
            if (string.Equals(name, type, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Matches a name against the configured patterns. Three forms are accepted, so that a rule
    /// can be written at whatever precision the author has to hand:
    /// <c>Some.Namespace.*</c> (everything under a namespace), a fully qualified type name, or a
    /// bare type name. The trailing <c>Attribute</c> is optional throughout, matching how the
    /// attribute is written in source.
    /// </summary>
    private static bool MatchesAny(ImmutableArray<string> patterns, string attributeFullName)
    {
        if (patterns.IsEmpty)
            return false;

        var simpleName = SimpleName(attributeFullName);

        foreach (var pattern in patterns)
        {
            if (pattern.EndsWith(".*", StringComparison.Ordinal))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                if (attributeFullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;

                continue;
            }

            var candidate = pattern.Contains('.') ? attributeFullName : simpleName;

            if (string.Equals(candidate, pattern, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(candidate, pattern + "Attribute", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string SimpleName(string attributeFullName)
    {
        var lastDot = attributeFullName.LastIndexOf('.');

        return lastDot >= 0 ? attributeFullName.Substring(lastDot + 1) : attributeFullName;
    }

    private static ImmutableArray<string> Combine(IEnumerable<string>? first, IEnumerable<string>? second)
    {
        if (first is null && second is null)
            return ImmutableArray<string>.Empty;

        var builder = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pattern in (first ?? Enumerable.Empty<string>()).Concat(second ?? Enumerable.Empty<string>()))
        {
            var trimmed = pattern?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed!))
                builder.Add(trimmed!);
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Splits a semicolon- or comma-separated MSBuild property value into patterns.
    /// </summary>
    public static ImmutableArray<string> ParsePatternList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ImmutableArray<string>.Empty;

        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var part in value!.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
                builder.Add(trimmed);
        }

        return builder.ToImmutable();
    }
}
