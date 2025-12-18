namespace Facet.Dashboard;

/// <summary>
/// Configuration options for the Facet Dashboard.
/// </summary>
public sealed class FacetDashboardOptions
{
    /// <summary>
    /// Gets or sets the route prefix for the dashboard. Default is "/facets".
    /// </summary>
    public string RoutePrefix { get; set; } = "/facets";

    /// <summary>
    /// Gets or sets the title displayed in the dashboard. Default is "Facet Dashboard".
    /// </summary>
    public string Title { get; set; } = "Facet Dashboard";

    /// <summary>
    /// Gets or sets whether to include system assemblies when discovering facets.
    /// Default is false.
    /// </summary>
    public bool IncludeSystemAssemblies { get; set; } = false;

    /// <summary>
    /// Gets or sets additional assemblies to scan for facets.
    /// </summary>
    public ICollection<System.Reflection.Assembly> AdditionalAssemblies { get; } = new List<System.Reflection.Assembly>();

    /// <summary>
    /// Gets or sets whether the dashboard requires authentication.
    /// Default is false.
    /// </summary>
    public bool RequireAuthentication { get; set; } = false;

    /// <summary>
    /// Gets or sets the authentication policy name to use when RequireAuthentication is true.
    /// </summary>
    public string? AuthenticationPolicy { get; set; }

    /// <summary>
    /// Gets or sets whether to expose the JSON API endpoint.
    /// Default is true.
    /// </summary>
    public bool EnableJsonApi { get; set; } = true;

    /// <summary>
    /// Gets or sets the accent color for the dashboard theme. 
    /// Default is "#6366f1" (Indigo).
    /// </summary>
    public string AccentColor { get; set; } = "#6366f1";

    /// <summary>
    /// Gets or sets whether to enable dark mode by default.
    /// Default is false (uses system preference).
    /// </summary>
    public bool DefaultDarkMode { get; set; } = false;
}
