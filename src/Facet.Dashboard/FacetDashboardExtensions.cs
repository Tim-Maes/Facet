using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Facet.Dashboard;

/// <summary>
/// Extension methods for configuring the Facet Dashboard in an ASP.NET Core application.
/// </summary>
public static class FacetDashboardExtensions
{
    /// <summary>
    /// Adds Facet Dashboard services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional action to configure dashboard options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFacetDashboard(
        this IServiceCollection services,
        Action<FacetDashboardOptions>? configure = null)
    {
        var options = new FacetDashboardOptions();
        configure?.Invoke(options);

        services.AddSingleton(Options.Create(options));
        services.AddSingleton<FacetDashboardService>();

        return services;
    }

    /// <summary>
    /// Maps the Facet Dashboard endpoints to the application.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapFacetDashboard(this IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetService<IOptions<FacetDashboardOptions>>()?.Value
            ?? new FacetDashboardOptions();

        var routePrefix = options.RoutePrefix.TrimEnd('/');

        // Main dashboard HTML page
        var dashboardEndpoint = app.MapGet(routePrefix, async context =>
        {
            var dashboardService = context.RequestServices.GetRequiredService<FacetDashboardService>();
            var html = dashboardService.GetDashboardHtml();
            
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html);
        });

        // JSON API endpoint
        if (options.EnableJsonApi)
        {
            app.MapGet($"{routePrefix}/api/facets", async context =>
            {
                var dashboardService = context.RequestServices.GetRequiredService<FacetDashboardService>();
                var facets = dashboardService.GetFacetMappings();
                
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                // Convert to serializable format (remove Type references)
                var result = facets.Select(m => new
                {
                    sourceTypeName = m.SourceTypeName,
                    sourceTypeSimpleName = m.SourceTypeSimpleName,
                    sourceTypeNamespace = m.SourceTypeNamespace,
                    sourceMembers = m.SourceMembers.Select(sm => new
                    {
                        sm.Name,
                        sm.TypeName,
                        sm.IsProperty,
                        sm.IsNullable,
                        sm.IsRequired,
                        sm.IsInitOnly,
                        sm.IsReadOnly,
                        sm.IsCollection,
                        sm.Attributes
                    }),
                    facets = m.Facets.Select(f => new
                    {
                        facetTypeName = f.FacetTypeName,
                        facetTypeSimpleName = f.FacetTypeSimpleName,
                        facetTypeNamespace = f.FacetTypeNamespace,
                        f.TypeKind,
                        f.HasConstructor,
                        f.HasProjection,
                        f.HasToSource,
                        f.NullableProperties,
                        f.CopyAttributes,
                        f.ConfigurationTypeName,
                        excludedProperties = f.ExcludedProperties,
                        includedProperties = f.IncludedProperties,
                        nestedFacets = f.NestedFacets.Select(n => n.FullName),
                        members = f.Members.Select(fm => new
                        {
                            fm.Name,
                            fm.TypeName,
                            fm.IsProperty,
                            fm.IsNullable,
                            fm.IsRequired,
                            fm.IsInitOnly,
                            fm.IsReadOnly,
                            fm.IsNestedFacet,
                            fm.IsCollection,
                            fm.MappedFromProperty,
                            fm.Attributes
                        })
                    })
                });

                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(result, jsonOptions));
            });
        }

        // Apply authentication if configured
        if (options.RequireAuthentication && !string.IsNullOrEmpty(options.AuthenticationPolicy))
        {
            dashboardEndpoint.RequireAuthorization(options.AuthenticationPolicy);
        }
        else if (options.RequireAuthentication)
        {
            dashboardEndpoint.RequireAuthorization();
        }

        return app;
    }
}
