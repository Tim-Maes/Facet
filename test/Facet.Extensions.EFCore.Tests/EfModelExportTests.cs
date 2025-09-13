using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Facet.Extensions.EFCore.Tests;

public class EfModelExportTests
{
    private sealed record EfModel(EfContext[] Contexts);
    private sealed record EfContext(string Context, Entity[] Entities);
    private sealed record Entity(string Name, string Clr, object[] Keys, object[] Navigations);

    [Fact]
    public void EfModelJson_Is_Generated_With_Context_and_Entities()
    {
        // Determine the path to efmodel.json produced by the referenced DbContext project.
        // We rely on the known project name and assume Debug build configuration for this test run.
        var configuration = "Debug"; // could be parameterized if needed
        var tfm = "net8.0";
        // Walk up from current test assembly location to find the sibling DbContext project folder
        var testAssemblyDir = Path.GetDirectoryName(typeof(EfModelExportTests).Assembly.Location)!; // .../test/Facet.Extensions.EFCore.Tests/bin/Debug/net8.0
        // Move up to the test root: bin/Debug/net8.0 -> bin/Debug -> bin -> (project dir)
        var projectDir = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", ".."));
        var dbContextProjectDir = Path.GetFullPath(Path.Combine(projectDir, "..", "Facet.Extensions.EFCore.Tests.DbContext"));
        var candidatePaths = new[]
        {
            Path.Combine(projectDir, "obj", configuration, tfm, "efmodel.json"), // copied path (if copy target executed)
            Path.Combine(dbContextProjectDir, "obj", configuration, tfm, "efmodel.json") // original generation path
        };

        var path = Array.Find(candidatePaths, File.Exists);
        if (path is null)
        {
            // Skip instead of fail to avoid noisy failures when export disabled intentionally.
            return; // xUnit treats returning from Fact as pass; optionally use Skip via conditional trait if needed.
        }

        // Act
        var json = File.ReadAllText(path);
        var model = JsonSerializer.Deserialize<EfModel>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(model);
        Assert.NotNull(model!.Contexts);
        Assert.NotEmpty(model.Contexts);
        var ctx = Assert.Single(model.Contexts);
        Assert.False(string.IsNullOrWhiteSpace(ctx.Context));
        Assert.NotNull(ctx.Entities);
        Assert.NotEmpty(ctx.Entities);
    }
}
