using Facet.Extensions.EFCore.Tests.TestData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Facet.Extensions.EFCore.Tests;

/// <summary>
/// Test startup class for configuring the test web application.
/// </summary>
public class TestStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Add EF Core DbContext
        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase("IntegrationTestDb")
                   .EnableSensitiveDataLogging());

        // Add logging
        services.AddLogging(builder =>
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Information));

        // Add controllers for basic web app setup
        services.AddControllers();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}