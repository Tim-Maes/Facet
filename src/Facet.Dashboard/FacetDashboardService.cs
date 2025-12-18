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
        var accentColor = _options.AccentColor;
        var title = _options.Title;
        var totalFacets = mappings.Sum(m => m.Facets.Count);

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{EscapeHtml(title)}</title>
    <style>
        :root {{
            --accent: {accentColor};
            --accent-light: {accentColor}22;
            --bg: #ffffff;
            --bg-secondary: #f8fafc;
            --text: #1e293b;
            --text-secondary: #64748b;
            --border: #e2e8f0;
            --success: #22c55e;
            --warning: #f59e0b;
            --error: #ef4444;
            --info: #3b82f6;
        }}

        @media (prefers-color-scheme: dark) {{
            :root {{
                --bg: #0f172a;
                --bg-secondary: #1e293b;
                --text: #f1f5f9;
                --text-secondary: #94a3b8;
                --border: #334155;
            }}
        }}

        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: var(--bg);
            color: var(--text);
            line-height: 1.6;
        }}

        .header {{
            background: linear-gradient(135deg, var(--accent), #8b5cf6);
            color: white;
            padding: 2rem;
            text-align: center;
        }}

        .header h1 {{
            font-size: 2rem;
            font-weight: 700;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 0.75rem;
        }}

        .header h1 svg {{
            width: 40px;
            height: 40px;
        }}

        .header .stats {{
            display: flex;
            justify-content: center;
            gap: 2rem;
            margin-top: 1rem;
            font-size: 0.875rem;
            opacity: 0.9;
        }}

        .header .stat {{
            display: flex;
            align-items: center;
            gap: 0.5rem;
        }}

        .header .stat-value {{
            font-weight: 700;
            font-size: 1.25rem;
        }}

        .container {{
            max-width: 1400px;
            margin: 0 auto;
            padding: 2rem;
        }}

        .search-bar {{
            margin-bottom: 2rem;
        }}

        .search-bar input {{
            width: 100%;
            padding: 1rem 1.5rem;
            font-size: 1rem;
            border: 2px solid var(--border);
            border-radius: 12px;
            background: var(--bg);
            color: var(--text);
            transition: border-color 0.2s;
        }}

        .search-bar input:focus {{
            outline: none;
            border-color: var(--accent);
        }}

        .source-card {{
            background: var(--bg-secondary);
            border: 1px solid var(--border);
            border-radius: 16px;
            margin-bottom: 1.5rem;
            overflow: hidden;
        }}

        .source-header {{
            padding: 1.25rem 1.5rem;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: space-between;
            transition: background 0.2s;
        }}

        .source-header:hover {{
            background: var(--accent-light);
        }}

        .source-title {{
            display: flex;
            align-items: center;
            gap: 0.75rem;
        }}

        .source-icon {{
            width: 40px;
            height: 40px;
            background: var(--accent);
            border-radius: 10px;
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-weight: 700;
            font-size: 1.1rem;
        }}

        .source-name {{
            font-size: 1.1rem;
            font-weight: 600;
        }}

        .source-namespace {{
            font-size: 0.75rem;
            color: var(--text-secondary);
        }}

        .source-badges {{
            display: flex;
            gap: 0.5rem;
        }}

        .badge {{
            padding: 0.25rem 0.75rem;
            border-radius: 999px;
            font-size: 0.75rem;
            font-weight: 600;
        }}

        .badge-primary {{
            background: var(--accent-light);
            color: var(--accent);
        }}

        .badge-success {{
            background: #dcfce7;
            color: #15803d;
        }}

        .chevron {{
            transition: transform 0.3s;
        }}

        .source-card.expanded .chevron {{
            transform: rotate(180deg);
        }}

        .source-content {{
            display: none;
            padding: 0 1.5rem 1.5rem;
        }}

        .source-card.expanded .source-content {{
            display: block;
        }}

        .section-title {{
            font-size: 0.875rem;
            font-weight: 600;
            color: var(--text-secondary);
            text-transform: uppercase;
            letter-spacing: 0.05em;
            margin: 1.5rem 0 0.75rem;
        }}

        .members-table {{
            width: 100%;
            border-collapse: collapse;
            font-size: 0.875rem;
        }}

        .members-table th,
        .members-table td {{
            padding: 0.75rem;
            text-align: left;
            border-bottom: 1px solid var(--border);
        }}

        .members-table th {{
            background: var(--bg);
            font-weight: 600;
            color: var(--text-secondary);
        }}

        .members-table tr:hover {{
            background: var(--accent-light);
        }}

        .type-name {{
            color: var(--accent);
            font-family: 'SF Mono', Monaco, monospace;
            font-size: 0.8rem;
        }}

        .member-badges {{
            display: flex;
            gap: 0.25rem;
            flex-wrap: wrap;
        }}

        .member-badge {{
            padding: 0.125rem 0.5rem;
            border-radius: 4px;
            font-size: 0.65rem;
            font-weight: 600;
            text-transform: uppercase;
        }}

        .member-badge-nullable {{
            background: #fef9c3;
            color: #854d0e;
        }}

        .member-badge-required {{
            background: #fee2e2;
            color: #b91c1c;
        }}

        .member-badge-init {{
            background: #e0e7ff;
            color: #3730a3;
        }}

        .member-badge-collection {{
            background: #d1fae5;
            color: #065f46;
        }}

        .member-badge-nested {{
            background: #f3e8ff;
            color: #7c3aed;
        }}

        .facet-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
            gap: 1rem;
            margin-top: 0.75rem;
        }}

        .facet-card {{
            background: var(--bg);
            border: 1px solid var(--border);
            border-radius: 12px;
            padding: 1rem;
        }}

        .facet-header {{
            display: flex;
            align-items: center;
            justify-content: space-between;
            margin-bottom: 0.75rem;
        }}

        .facet-name {{
            font-weight: 600;
            color: var(--accent);
        }}

        .facet-type-kind {{
            font-size: 0.7rem;
            color: var(--text-secondary);
            background: var(--bg-secondary);
            padding: 0.125rem 0.5rem;
            border-radius: 4px;
        }}

        .facet-features {{
            display: flex;
            flex-wrap: wrap;
            gap: 0.5rem;
            margin-bottom: 0.75rem;
        }}

        .feature {{
            display: flex;
            align-items: center;
            gap: 0.25rem;
            font-size: 0.75rem;
            color: var(--text-secondary);
        }}

        .feature svg {{
            width: 14px;
            height: 14px;
        }}

        .feature-enabled svg {{
            color: var(--success);
        }}

        .feature-disabled svg {{
            color: var(--text-secondary);
            opacity: 0.5;
        }}

        .facet-config {{
            font-size: 0.75rem;
            color: var(--text-secondary);
        }}

        .empty-state {{
            text-align: center;
            padding: 4rem 2rem;
            color: var(--text-secondary);
        }}

        .empty-state svg {{
            width: 64px;
            height: 64px;
            margin-bottom: 1rem;
            opacity: 0.5;
        }}

        .footer {{
            text-align: center;
            padding: 2rem;
            color: var(--text-secondary);
            font-size: 0.875rem;
        }}

        .footer a {{
            color: var(--accent);
            text-decoration: none;
        }}

        @media (max-width: 768px) {{
            .container {{
                padding: 1rem;
            }}

            .header .stats {{
                flex-direction: column;
                gap: 0.5rem;
            }}

            .facet-grid {{
                grid-template-columns: 1fr;
            }}

            .members-table {{
                display: block;
                overflow-x: auto;
            }}
        }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>
            <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"">
                <polygon points=""12 2 22 8.5 22 15.5 12 22 2 15.5 2 8.5 12 2""/>
                <line x1=""12"" y1=""22"" x2=""12"" y2=""15.5""/>
                <polyline points=""22,8.5 12,15.5 2,8.5""/>
                <polyline points=""2,15.5 12,8.5 22,15.5""/>
                <line x1=""12"" y1=""2"" x2=""12"" y2=""8.5""/>
            </svg>
            {EscapeHtml(title)}
        </h1>
        <div class=""stats"">
            <div class=""stat"">
                <span>Source Types</span>
                <span class=""stat-value"">{mappings.Count}</span>
            </div>
            <div class=""stat"">
                <span>Total Facets</span>
                <span class=""stat-value"">{totalFacets}</span>
            </div>
        </div>
    </div>

    <div class=""container"">
        <div class=""search-bar"">
            <input type=""text"" id=""searchInput"" placeholder=""Search source types or facets..."" />
        </div>

        <div id=""sourceList"">
            {GenerateSourceCards(mappings)}
        </div>

        {(mappings.Count == 0 ? GenerateEmptyState() : "")}
    </div>

    <div class=""footer"">
        <p>Powered by <a href=""https://github.com/Tim-Maes/Facet"" target=""_blank"">Facet</a> — Compile-time DTO generation for .NET</p>
    </div>

    <script>
        // Toggle source card expansion
        document.querySelectorAll('.source-header').forEach(header => {{
            header.addEventListener('click', () => {{
                header.closest('.source-card').classList.toggle('expanded');
            }});
        }});

        // Search functionality
        document.getElementById('searchInput').addEventListener('input', function(e) {{
            const query = e.target.value.toLowerCase();
            document.querySelectorAll('.source-card').forEach(card => {{
                const text = card.textContent.toLowerCase();
                card.style.display = text.includes(query) ? '' : 'none';
            }});
        }});
    </script>
</body>
</html>";
    }

    private static string GenerateSourceCards(IReadOnlyList<FacetMappingInfo> mappings)
    {
        var sb = new StringBuilder();

        foreach (var mapping in mappings)
        {
            var initial = mapping.SourceTypeSimpleName.Length > 0 
                ? mapping.SourceTypeSimpleName[0].ToString().ToUpperInvariant() 
                : "?";
            var facetCount = mapping.Facets.Count;

            sb.AppendLine($@"
            <div class=""source-card"">
                <div class=""source-header"">
                    <div class=""source-title"">
                        <div class=""source-icon"">{initial}</div>
                        <div>
                            <div class=""source-name"">{EscapeHtml(mapping.SourceTypeSimpleName)}</div>
                            <div class=""source-namespace"">{EscapeHtml(mapping.SourceTypeNamespace ?? "")}</div>
                        </div>
                    </div>
                    <div style=""display: flex; align-items: center; gap: 1rem;"">
                        <div class=""source-badges"">
                            <span class=""badge badge-primary"">{facetCount} facet{(facetCount != 1 ? "s" : "")}</span>
                            <span class=""badge badge-success"">{mapping.SourceMembers.Count} properties</span>
                        </div>
                        <svg class=""chevron"" width=""20"" height=""20"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"">
                            <polyline points=""6,9 12,15 18,9""/>
                        </svg>
                    </div>
                </div>
                <div class=""source-content"">
                    <h4 class=""section-title"">Source Properties</h4>
                    {GenerateMembersTable(mapping.SourceMembers)}
                    
                    <h4 class=""section-title"">Generated Facets</h4>
                    <div class=""facet-grid"">
                        {GenerateFacetCards(mapping.Facets)}
                    </div>
                </div>
            </div>");
        }

        return sb.ToString();
    }

    private static string GenerateMembersTable(IReadOnlyList<FacetMemberInfo> members)
    {
        if (members.Count == 0)
            return @"<p style=""color: var(--text-secondary); font-size: 0.875rem;"">No public members found.</p>";

        var sb = new StringBuilder();
        sb.AppendLine(@"
            <table class=""members-table"">
                <thead>
                    <tr>
                        <th>Name</th>
                        <th>Type</th>
                        <th>Modifiers</th>
                    </tr>
                </thead>
                <tbody>");

        foreach (var member in members)
        {
            var badges = new List<string>();
            if (member.IsNullable) badges.Add(@"<span class=""member-badge member-badge-nullable"">Nullable</span>");
            if (member.IsRequired) badges.Add(@"<span class=""member-badge member-badge-required"">Required</span>");
            if (member.IsInitOnly) badges.Add(@"<span class=""member-badge member-badge-init"">Init</span>");
            if (member.IsCollection) badges.Add(@"<span class=""member-badge member-badge-collection"">Collection</span>");
            if (member.IsNestedFacet) badges.Add(@"<span class=""member-badge member-badge-nested"">Nested Facet</span>");

            sb.AppendLine($@"
                    <tr>
                        <td><strong>{EscapeHtml(member.Name)}</strong></td>
                        <td><span class=""type-name"">{EscapeHtml(member.TypeName)}</span></td>
                        <td><div class=""member-badges"">{string.Join("", badges)}</div></td>
                    </tr>");
        }

        sb.AppendLine("</tbody></table>");
        return sb.ToString();
    }

    private static string GenerateFacetCards(IReadOnlyList<FacetTypeInfo> facets)
    {
        var sb = new StringBuilder();

        foreach (var facet in facets)
        {
            var checkIcon = @"<svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""3""><polyline points=""20,6 9,17 4,12""/></svg>";
            var xIcon = @"<svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2""><line x1=""18"" y1=""6"" x2=""6"" y2=""18""/><line x1=""6"" y1=""6"" x2=""18"" y2=""18""/></svg>";

            var exclusions = facet.ExcludedProperties.Count > 0
                ? $@"<div class=""facet-config"">Excludes: {EscapeHtml(string.Join(", ", facet.ExcludedProperties))}</div>"
                : "";

            var inclusions = facet.IncludedProperties?.Count > 0
                ? $@"<div class=""facet-config"">Includes: {EscapeHtml(string.Join(", ", facet.IncludedProperties))}</div>"
                : "";

            sb.AppendLine($@"
                <div class=""facet-card"">
                    <div class=""facet-header"">
                        <span class=""facet-name"">{EscapeHtml(facet.FacetTypeSimpleName)}</span>
                        <span class=""facet-type-kind"">{EscapeHtml(facet.TypeKind)}</span>
                    </div>
                    <div class=""facet-features"">
                        <span class=""feature {(facet.HasConstructor ? "feature-enabled" : "feature-disabled")}"">{(facet.HasConstructor ? checkIcon : xIcon)} Constructor</span>
                        <span class=""feature {(facet.HasProjection ? "feature-enabled" : "feature-disabled")}"">{(facet.HasProjection ? checkIcon : xIcon)} Projection</span>
                        <span class=""feature {(facet.HasToSource ? "feature-enabled" : "feature-disabled")}"">{(facet.HasToSource ? checkIcon : xIcon)} ToSource</span>
                    </div>
                    {exclusions}
                    {inclusions}
                    <div class=""facet-config"" style=""margin-top: 0.5rem;"">{facet.Members.Count} member{(facet.Members.Count != 1 ? "s" : "")}</div>
                </div>");
        }

        return sb.ToString();
    }

    private static string GenerateEmptyState()
    {
        return @"
            <div class=""empty-state"">
                <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.5"">
                    <polygon points=""12 2 22 8.5 22 15.5 12 22 2 15.5 2 8.5 12 2""/>
                    <line x1=""12"" y1=""22"" x2=""12"" y2=""15.5""/>
                    <polyline points=""22,8.5 12,15.5 2,8.5""/>
                </svg>
                <h3>No Facets Found</h3>
                <p>No types with [Facet] attribute were discovered in your assemblies.</p>
                <p style=""margin-top: 1rem;"">Make sure your facet types are public and the assemblies are loaded.</p>
            </div>";
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
