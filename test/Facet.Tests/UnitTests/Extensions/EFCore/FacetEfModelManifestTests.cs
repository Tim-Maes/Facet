using System.Text.Json;
using Facet.Extensions.EFCore.Design;
using Microsoft.EntityFrameworkCore;

namespace Facet.Tests.UnitTests.Extensions.EFCore;

/// <summary>
/// FacetEfModelManifest.Build against a real finalized model: mapped scalars (including
/// primitive collections and FK properties) and complex properties land in keep categories;
/// navigations, owned references, and skip navigations land in drop categories; EF-ignored
/// and shadow properties never appear at all.
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
        // The design-time model — what the migrations scaffolder hook receives. Unlike the
        // runtime model it still implements the convention metadata surface, which is where
        // explicitly ignored members live.
        var model = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
            .GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>(context).Model;
        return FacetEfModelManifest.Build(model, nameof(ManifestContext));
    }

    private static JsonElement Root(string manifest)
    {
        using var document = JsonDocument.Parse(manifest);
        return document.RootElement.Clone();
    }

    private static JsonElement Entity(string manifest, string clrType)
    {
        var entities = Root(manifest).GetProperty("entities").EnumerateArray()
            .Where(e => e.GetProperty("clrType").GetString() == clrType)
            .ToList();
        entities.Should().HaveCount(1, $"the manifest should list {clrType} exactly once");
        return entities[0];
    }

    private static string[] Category(JsonElement entity, string name)
        => entity.TryGetProperty(name, out var members)
            ? members.EnumerateArray().Select(m => m.GetString()!).ToArray()
            : Array.Empty<string>();

    private static string ClrName(Type type) => type.FullName!.Replace('+', '.');

    [Fact]
    public void Manifest_IsVersionedJson()
    {
        var root = Root(BuildManifest());

        root.GetProperty("version").GetInt32().Should().Be(1);
        root.GetProperty("context").GetString().Should().Be(nameof(ManifestContext));
        root.GetProperty("$comment").GetString().Should().Contain("do not edit");
    }

    [Fact]
    public void MappedScalarsAndComplexProperties_AreKeepCategories()
    {
        var blog = Entity(BuildManifest(), ClrName(typeof(ManifestBlog)));

        Category(blog, "scalar").Should().Contain(["Id", "Title", "Tags"],
            "scalar columns and primitive collections are mapped data");
        Category(blog, "complex").Should().Contain("HomeAddress");
    }

    [Fact]
    public void NavigationsOwnedAndSkipNavigations_AreDropCategories()
    {
        var blog = Entity(BuildManifest(), ClrName(typeof(ManifestBlog)));

        Category(blog, "nav").Should().Contain(["Publisher", "Posts"]);
        Category(blog, "owned").Should().Contain("Settings");
        Category(blog, "skipnav").Should().Contain("Labels");
    }

    [Fact]
    public void ExplicitlyIgnoredMembers_AreRecordedAsIgnored()
    {
        var blog = Entity(BuildManifest(), ClrName(typeof(ManifestBlog)));

        Category(blog, "ignored").Should().Contain("Secret",
            "the model's opinion on ignored members is what lets the generator tell 'ignored' apart from 'unknown' (FAC106)");
        Category(blog, "scalar").Should().NotContain("Secret");
    }

    [Fact]
    public void ShadowProperties_NeverAppear()
    {
        var manifest = BuildManifest();

        manifest.Should().NotContain("ShadowStamp", "shadow properties have no CLR member to keep or drop");
        manifest.Should().NotContain("PublisherId", "the shadow FK EF invents has no CLR member either");
    }

    [Fact]
    public void ForeignKeyAndInverseNavigation_AreRecordedOnTheDependent()
    {
        var post = Entity(BuildManifest(), ClrName(typeof(ManifestPost)));

        Category(post, "scalar").Should().Contain("BlogId", "CLR-backed FK properties are mapped data");
        Category(post, "nav").Should().Contain("Blog");
    }

    [Fact]
    public void Write_CreatesContextNamedManifestFile()
    {
        var directory = Directory.CreateTempSubdirectory("facet-manifest-").FullName;
        try
        {
            using var context = new ManifestContext();
            var path = FacetEfModelManifest.Write(context.Model, directory, nameof(ManifestContext));

            path.Should().Be(Path.Combine(directory, "ManifestContext.facetmodel.json"));
            Entity(File.ReadAllText(path), ClrName(typeof(ManifestBlog)));
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
