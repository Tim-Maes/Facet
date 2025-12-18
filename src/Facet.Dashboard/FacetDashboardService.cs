using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text;

namespace Facet.Dashboard;

/// <summary>
/// Service that provides dashboard functionality.
/// </summary>
public sealed class FacetDashboardService
{
    private readonly FacetDashboardOptions _options;
    private IReadOnlyList<FacetMappingInfo>? _cachedMappings;

    // SVG icons as constants
    private const string CheckIcon = @"<svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""3""><polyline points=""20,6 9,17 4,12""/></svg>";
    private const string XIcon = @"<svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><line x1=""18"" y1=""6"" x2=""6"" y2=""18""/><line x1=""6"" y1=""6"" x2=""18"" y2=""18""/></svg>";

    /// <summary>
    /// Creates a new instance of the <see cref="FacetDashboardService"/>.
    /// </summary>
    public FacetDashboardService(IOptions<FacetDashboardOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Gets all discovered facet mappings.
    /// </summary>
    public IReadOnlyList<FacetMappingInfo> GetFacetMappings()
    {
        if (_cachedMappings != null)
            return _cachedMappings;

        var assemblies = new HashSet<Assembly>();

        // Add entry assembly and its references
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null)
        {
            assemblies.Add(entryAssembly);
            foreach (var reference in entryAssembly.GetReferencedAssemblies())
            {
                try
                {
                    assemblies.Add(Assembly.Load(reference));
                }
                catch
                {
                    // Skip unloadable assemblies
                }
            }
        }

        // Add additional assemblies from options
        foreach (var assembly in _options.AdditionalAssemblies)
        {
            assemblies.Add(assembly);
        }

        _cachedMappings = FacetDiscovery.DiscoverFacets(assemblies);
        return _cachedMappings;
    }

    /// <summary>
    /// Gets the dashboard HTML page.
    /// </summary>
    public string GetDashboardHtml()
    {
        var mappings = GetFacetMappings();
        return GenerateDashboardHtml(mappings);
    }

    private string GenerateDashboardHtml(IReadOnlyList<FacetMappingInfo> mappings)
    {
        var totalFacets = mappings.Sum(m => m.Facets.Count);
        var sourceCards = GenerateSourceCards(mappings);
        var emptyState = mappings.Count == 0 ? TemplateEngine.LoadTemplate("empty-state.html") : "";

        var tokens = new Dictionary<string, string>
        {
            { "title", EscapeHtml(_options.Title) },
            { "accentColor", _options.AccentColor },
            { "sourceCount", mappings.Count.ToString() },
            { "facetCount", totalFacets.ToString() },
            { "sourceCards", sourceCards },
            { "emptyState", emptyState }
        };

        return TemplateEngine.RenderTemplate("dashboard.html", tokens);
    }

    private string GenerateSourceCards(IReadOnlyList<FacetMappingInfo> mappings)
    {
        var sb = new StringBuilder();

        foreach (var mapping in mappings)
        {
            var initial = mapping.SourceTypeSimpleName.Length > 0
                ? mapping.SourceTypeSimpleName[0].ToString().ToUpperInvariant()
                : "?";
            var facetCount = mapping.Facets.Count;

            var tokens = new Dictionary<string, string>
            {
                { "initial", initial },
                { "sourceName", EscapeHtml(mapping.SourceTypeSimpleName) },
                { "sourceNamespace", EscapeHtml(mapping.SourceTypeNamespace ?? "") },
                { "facetCount", facetCount.ToString() },
                { "facetPlural", facetCount != 1 ? "s" : "" },
                { "propertyCount", mapping.SourceMembers.Count.ToString() },
                { "membersTable", GenerateMembersTable(mapping.SourceMembers) },
                { "facetCards", GenerateFacetCards(mapping.Facets) }
            };

            sb.AppendLine(TemplateEngine.RenderTemplate("source-card.html", tokens));
        }

        return sb.ToString();
    }

    private string GenerateMembersTable(IReadOnlyList<FacetMemberInfo> members)
    {
        if (members.Count == 0)
            return @"<p style=""color: var(--text-secondary); font-size: 0.875rem;"">No public members found.</p>";

        var sb = new StringBuilder();

        foreach (var member in members)
        {
            var badges = new List<string>();
            if (member.IsNullable) badges.Add(@"<span class=""member-badge member-badge-nullable"">Nullable</span>");
            if (member.IsRequired) badges.Add(@"<span class=""member-badge member-badge-required"">Required</span>");
            if (member.IsInitOnly) badges.Add(@"<span class=""member-badge member-badge-init"">Init</span>");
            if (member.IsCollection) badges.Add(@"<span class=""member-badge member-badge-collection"">Collection</span>");
            if (member.IsNestedFacet) badges.Add(@"<span class=""member-badge member-badge-nested"">Nested Facet</span>");

            var rowTokens = new Dictionary<string, string>
            {
                { "memberName", EscapeHtml(member.Name) },
                { "memberType", EscapeHtml(member.TypeName) },
                { "memberBadges", string.Join("", badges) }
            };

            sb.AppendLine(TemplateEngine.RenderTemplate("member-row.html", rowTokens));
        }

        var tableTokens = new Dictionary<string, string>
        {
            { "memberRows", sb.ToString() }
        };

        return TemplateEngine.RenderTemplate("members-table.html", tableTokens);
    }

    private string GenerateFacetCards(IReadOnlyList<FacetTypeInfo> facets)
    {
        var sb = new StringBuilder();

        foreach (var facet in facets)
        {
            var exclusions = facet.ExcludedProperties.Count > 0
                ? $@"<div class=""facet-config"">Excludes: {EscapeHtml(string.Join(", ", facet.ExcludedProperties))}</div>"
                : "";

            var inclusions = facet.IncludedProperties?.Count > 0
                ? $@"<div class=""facet-config"">Includes: {EscapeHtml(string.Join(", ", facet.IncludedProperties))}</div>"
                : "";

            var tokens = new Dictionary<string, string>
            {
                { "facetName", EscapeHtml(facet.FacetTypeSimpleName) },
                { "typeKind", EscapeHtml(facet.TypeKind) },
                { "constructorClass", facet.HasConstructor ? "feature-enabled" : "feature-disabled" },
                { "constructorIcon", facet.HasConstructor ? CheckIcon : XIcon },
                { "projectionClass", facet.HasProjection ? "feature-enabled" : "feature-disabled" },
                { "projectionIcon", facet.HasProjection ? CheckIcon : XIcon },
                { "toSourceClass", facet.HasToSource ? "feature-enabled" : "feature-disabled" },
                { "toSourceIcon", facet.HasToSource ? CheckIcon : XIcon },
                { "exclusions", exclusions },
                { "inclusions", inclusions },
                { "memberCount", facet.Members.Count.ToString() },
                { "memberPlural", facet.Members.Count != 1 ? "s" : "" }
            };

            sb.AppendLine(TemplateEngine.RenderTemplate("facet-card.html", tokens));
        }

        return sb.ToString();
    }

    private static string EscapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
