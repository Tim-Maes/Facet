using Facet.Extensions.EFCore.Design;
using Microsoft.EntityFrameworkCore;

namespace Facet.Tests.UnitTests.Extensions.EFCore;

/// <summary>
/// FacetEfModelManifest.Build against a real finalized model: mapped scalars (including
/// primitive collections and FK properties) and complex properties are keep records;
/// navigations, owned references, and skip navigations are drop records; EF-ignored and
/// shadow properties never appear at all.
/// </summary>
public class FacetEfModelManifestTests
{
    public class ManifestBlog
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public string Secret { get; set; } = "";
        public ManifestAddress HomeAddress { get; set; } = new();
        public ManifestPublisher? Publisher { get; set; }
        public List<ManifestPost> Posts { get; set; } = new();
        public List<ManifestLabel> Labels { get; set; } = new();
        public ManifestSettings Settings { get; set; } = new();
    }

    public class ManifestPost
    {
        public int Id { get; set; }
        public int BlogId { get; set; }
        public ManifestBlog Blog { get; set; } = null!;
    }

    public class ManifestLabel
    {
        public int Id { get; set; }
        public List<ManifestBlog> Blogs { get; set; } = new();
    }

    public class ManifestPublisher
    {
        public int Id { get; set; }
    }

    public class ManifestSettings
    {
        public string Theme { get; set; } = "";
    }

    public class ManifestAddress
    {
        public string City { get; set; } = "";
    }

    public class ManifestContext : DbContext
    {
        public DbSet<ManifestBlog> Blogs => Set<ManifestBlog>();
        public DbSet<ManifestPost> Posts => Set<ManifestPost>();
        public DbSet<ManifestLabel> Labels => Set<ManifestLabel>();
        public DbSet<ManifestPublisher> Publishers => Set<ManifestPublisher>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("Data Source=:memory:");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ManifestBlog>(blog =>
            {
                blog.Ignore(b => b.Secret);
                blog.Property<DateTime>("ShadowStamp");
                blog.ComplexProperty(b => b.HomeAddress);
                blog.OwnsOne(b => b.Settings);
                blog.HasMany(b => b.Labels).WithMany(l => l.Blogs);
            });
        }
    }

    private static string BuildManifest()
    {
        using var context = new ManifestContext();
        return FacetEfModelManifest.Build(context.Model, nameof(ManifestContext));
    }

    private static List<string> EntitySection(string manifest, string entityName)
    {
        var lines = manifest.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var start = lines.IndexOf($"entity {entityName}");
        start.Should().BeGreaterThanOrEqualTo(0, $"the manifest should list {entityName}");

        var section = new List<string>();
        for (var i = start + 1; i < lines.Count && !lines[i].StartsWith("entity ", StringComparison.Ordinal); i++)
        {
            if (lines[i].Length > 0) section.Add(lines[i]);
        }

        return section;
    }

    private static string BlogEntityName => typeof(ManifestBlog).FullName!.Replace('+', '.');

    [Fact]
    public void MappedScalarsAndComplexProperties_AreKeepRecords()
    {
        var section = EntitySection(BuildManifest(), BlogEntityName);

        section.Should().Contain("scalar Id");
        section.Should().Contain("scalar Title");
        section.Should().Contain("scalar Tags", "primitive collections are mapped data");
        section.Should().Contain("complex HomeAddress");
    }

    [Fact]
    public void NavigationsOwnedAndSkipNavigations_AreDropRecords()
    {
        var section = EntitySection(BuildManifest(), BlogEntityName);

        section.Should().Contain("nav Publisher");
        section.Should().Contain("nav Posts");
        section.Should().Contain("owned Settings");
        section.Should().Contain("skipnav Labels");
    }

    [Fact]
    public void IgnoredAndShadowProperties_NeverAppear()
    {
        var manifest = BuildManifest();

        manifest.Should().NotContain("Secret", "EF-ignored properties are not part of the model");
        manifest.Should().NotContain("ShadowStamp", "shadow properties have no CLR member to keep or drop");
        manifest.Should().NotContain("PublisherId", "the shadow FK EF invents has no CLR member either");
    }

    [Fact]
    public void ForeignKeyAndInverseNavigation_AreRecordedOnTheDependent()
    {
        var section = EntitySection(BuildManifest(), typeof(ManifestPost).FullName!.Replace('+', '.'));

        section.Should().Contain("scalar BlogId", "CLR-backed FK properties are mapped data");
        section.Should().Contain("nav Blog");
    }

    [Fact]
    public void Write_CreatesContextNamedManifestFile()
    {
        var directory = Directory.CreateTempSubdirectory("facet-manifest-").FullName;
        try
        {
            using var context = new ManifestContext();
            var path = FacetEfModelManifest.Write(context.Model, directory, nameof(ManifestContext));

            path.Should().Be(Path.Combine(directory, "ManifestContext.facetmodel"));
            File.ReadAllText(path).Should().Contain($"entity {BlogEntityName}");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        BuildManifest().Should().Be(BuildManifest(), "stable output is what makes the committed file diff-friendly");
    }
}
