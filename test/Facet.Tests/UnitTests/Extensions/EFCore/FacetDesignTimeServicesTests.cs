using Facet.Extensions.EFCore.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Facet.Tests.UnitTests.Extensions.EFCore;

/// <summary>
/// End-to-end through EF's real design-time pipeline: FacetDesignTimeServices swaps in the
/// manifest-writing scaffolder, and scaffolding + saving a migration writes
/// {ContextName}.facetmodel beside the model snapshot.
/// </summary>
public class FacetDesignTimeServicesTests
{
    public class DesignWidget
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<DesignPart> Parts { get; set; } = new();
    }

    public class DesignPart
    {
        public int Id { get; set; }
        public int DesignWidgetId { get; set; }
        public DesignWidget Widget { get; set; } = null!;
    }

    public class DesignManifestContext : DbContext
    {
        public DbSet<DesignWidget> Widgets => Set<DesignWidget>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("Data Source=:memory:");
    }

    private static IMigrationsScaffolder CreateScaffolder(DbContext context)
    {
        var services = new ServiceCollection()
            .AddEntityFrameworkDesignTimeServices()
            .AddDbContextDesignTimeServices(context);
#pragma warning disable EF1001 // dotnet ef adds the provider's design-time services the same way
        new Microsoft.EntityFrameworkCore.Sqlite.Design.Internal.SqliteDesignTimeServices()
            .ConfigureDesignTimeServices(services);
#pragma warning restore EF1001
        new FacetDesignTimeServices().ConfigureDesignTimeServices(services);
        return services.BuildServiceProvider().GetRequiredService<IMigrationsScaffolder>();
    }

    [Fact]
    public void FacetDesignTimeServices_ReplacesTheDefaultScaffolder()
    {
        using var context = new DesignManifestContext();

        CreateScaffolder(context).Should().BeOfType<FacetManifestMigrationsScaffolder>();
    }

    [Fact]
    public void SavingAMigration_WritesTheManifestBesideTheSnapshot()
    {
        using var context = new DesignManifestContext();
        var scaffolder = CreateScaffolder(context);
        var projectDir = Directory.CreateTempSubdirectory("facet-design-").FullName;

        try
        {
            var migration = scaffolder.ScaffoldMigration("InitialFacetManifest", "Facet.Tests.DesignSandbox");
            var files = scaffolder.Save(projectDir, migration, outputDir: null);

            var manifestPath = Path.Combine(
                Path.GetDirectoryName(files.SnapshotFile)!,
                nameof(DesignManifestContext) + FacetEfModelManifest.FileExtension);
            File.Exists(manifestPath).Should().BeTrue("the manifest belongs beside the snapshot it mirrors");

            var manifest = File.ReadAllText(manifestPath);
            manifest.Should().Contain($"entity {typeof(DesignWidget).FullName!.Replace('+', '.')}");
            manifest.Should().Contain("scalar Name");
            manifest.Should().Contain("nav Parts");
            manifest.Should().Contain("nav Widget");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }
}
