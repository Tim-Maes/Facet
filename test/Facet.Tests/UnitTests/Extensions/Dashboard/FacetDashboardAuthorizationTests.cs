using Facet.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Facet.Tests.UnitTests.Extensions.Dashboard;

public class FacetDashboardAuthorizationTests
{
    [Fact]
    public void MapFacetDashboard_WithAuthenticationPolicy_ShouldProtectEveryEndpoint()
    {
        var endpoints = MapDashboard(options =>
        {
            options.RequireAuthentication = true;
            options.AuthenticationPolicy = "AdminOnly";
        });

        endpoints.Should().HaveCount(2, "the dashboard maps the HTML page and the JSON API");
        endpoints.Should().AllSatisfy(endpoint =>
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().Contain(a => a.Policy == "AdminOnly", endpoint.DisplayName));
    }

    [Fact]
    public void MapFacetDashboard_WithAuthenticationWithoutPolicy_ShouldProtectEveryEndpoint()
    {
        var endpoints = MapDashboard(options => options.RequireAuthentication = true);

        endpoints.Should().HaveCount(2);
        endpoints.Should().AllSatisfy(endpoint =>
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().NotBeEmpty(endpoint.DisplayName));
    }

    [Fact]
    public void MapFacetDashboard_WithoutAuthentication_ShouldNotRequireAuthorization()
    {
        var endpoints = MapDashboard(_ => { });

        endpoints.Should().HaveCount(2);
        endpoints.Should().AllSatisfy(endpoint =>
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Should().BeEmpty(endpoint.DisplayName));
    }

    private static List<Endpoint> MapDashboard(Action<FacetDashboardOptions> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFacetDashboard(configure);
        var app = builder.Build();

        app.MapFacetDashboard();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .ToList();
    }
}
