using Facet.Mapping;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Facet.Tests.UnitTests.Extensions.EFCore;

public class DeepProjectionEfCoreQueryRegressionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DeepProjectionDbContext354 _context;

    public DeepProjectionEfCoreQueryRegressionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DeepProjectionDbContext354>()
            .UseSqlite(_connection)
            .Options;

        _context = new DeepProjectionDbContext354(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task QueryProjection_FiveLevels_Inline_ShouldMapConfiguredNestedMembers()
    {
        var root = CreateInlineRoot(1, "Inline");
        _context.Level1Entities.Add(root);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var dto = await _context.Level1Entities
            .Where(x => x.Id == 1)
            .Select(Level1Dto354.Projection)
            .SingleAsync();

        dto.Id.Should().Be(1);
        dto.Level2.Should().NotBeNull();
        dto.Level2!.Level3.Should().NotBeNull();
        dto.Level2.Level3!.Id.Should().Be(43);
        dto.Level2.Level3.Level4.Should().NotBeNull();
        dto.Level2.Level3.Level4!.Level5.Should().NotBeNull();
        dto.Level2.Level3.Level4.Level5!.Contact.Should().NotBeNull();
        dto.Level2.Level3.Level4.Level5.Contact!.Name.Should().Be("Inline (abstract)");
    }

    [Fact]
    public async Task QueryProjection_FiveLevels_LazyRoot_ShouldKeepNestedProjectionConfiguration()
    {
        var root = CreateInlineRoot(2, "Lazy");
        _context.Level1Entities.Add(root);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var dto = await _context.Level1Entities
            .Where(x => x.Id == 2)
            .Select(Level1Dto354Lazy.Projection)
            .SingleAsync();

        dto.Id.Should().Be(1002);
        dto.Level2.Should().NotBeNull();
        dto.Level2!.Level3.Should().NotBeNull();
        dto.Level2.Level3!.Level4.Should().NotBeNull();
        dto.Level2.Level3.Level4!.Level5.Should().NotBeNull();
        dto.Level2.Level3.Level4.Level5!.Contact.Should().NotBeNull();
        dto.Level2.Level3.Level4.Level5.Contact!.Name.Should().Be("Lazy (abstract)");
    }

    [Fact]
    public async Task QueryProjection_FiveLevels_InheritedRoot_ShouldProjectNestedChain()
    {
        var derived = new DerivedRootEntity354
        {
            Id = 10,
            Code = "DER-10",
            Level2 = CreateLevel2Chain(100, "Inherited")
        };

        _context.DerivedRootEntities.Add(derived);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var dto = await _context.DerivedRootEntities
            .Where(x => x.Id == 10)
            .Select(DerivedRootDto354.Projection)
            .SingleAsync();

        dto.Id.Should().Be(10);
        dto.Code.Should().Be("DER-10");
        dto.Level2.Should().NotBeNull();
        dto.Level2!.Level3.Should().NotBeNull();
        dto.Level2.Level3!.Level4.Should().NotBeNull();
        dto.Level2.Level3.Level4!.Level5.Should().NotBeNull();
        dto.Level2.Level3.Level4.Level5!.Contact.Should().NotBeNull();
        dto.Level2.Level3.Level4.Level5.Contact!.Name.Should().Be("Inherited (abstract)");
    }

    [Fact]
    public async Task QueryProjection_FiveLevels_NullIntermediateNode_ShouldRemainNull()
    {
        var root = new Level1Entity354
        {
            Id = 20,
            Level2 = new Level2Entity354
            {
                Id = 21,
                Level3 = new Level3Entity354
                {
                    Id = 22,
                    Level4 = null
                }
            }
        };

        _context.Level1Entities.Add(root);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var dto = await _context.Level1Entities
            .Where(x => x.Id == 20)
            .Select(Level1Dto354.Projection)
            .SingleAsync();

        dto.Level2.Should().NotBeNull();
        dto.Level2!.Level3.Should().NotBeNull();
        dto.Level2.Level3!.Id.Should().Be(52);
        dto.Level2.Level3.Level4.Should().BeNull();
    }

    private static Level1Entity354 CreateInlineRoot(int id, string contactName) =>
        new()
        {
            Id = id,
            Level2 = CreateLevel2Chain(id * 10, contactName)
        };

    private static Level2Entity354 CreateLevel2Chain(int baseId, string contactName) =>
        new()
        {
            Id = baseId + 2,
            Level3 = new Level3Entity354
            {
                Id = baseId + 3,
                Level4 = new Level4Entity354
                {
                    Id = baseId + 4,
                    Level5 = new Level5Entity354
                    {
                        Id = baseId + 5,
                        Contact = new ConcreteContact354
                        {
                            Id = baseId + 6,
                            Name = contactName
                        }
                    }
                }
            }
        };

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

public abstract class AbstractContact354
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ConcreteContact354 : AbstractContact354
{
}

[Facet(typeof(AbstractContact354),
    Configuration = typeof(AbstractContact354Config),
    Include = new[] { nameof(AbstractContact354.Id), nameof(AbstractContact354.Name) })]
[Facet(typeof(ConcreteContact354),
    Include = new[] { nameof(ConcreteContact354.Id), nameof(ConcreteContact354.Name) })]
public partial class ContactFacet354
{
}

public class AbstractContact354Config
    : IFacetProjectionMapConfiguration<AbstractContact354, ContactFacet354>
{
    public static void ConfigureProjection(IFacetProjectionBuilder<AbstractContact354, ContactFacet354> builder)
    {
        builder.Map(d => d.Name, s => s.Name + " (abstract)");
    }
}

public class Level5Entity354
{
    public int Id { get; set; }
    public int? ContactId { get; set; }
    public AbstractContact354? Contact { get; set; }
}

public class Level4Entity354
{
    public int Id { get; set; }
    public int? Level5Id { get; set; }
    public Level5Entity354? Level5 { get; set; }
}

public class Level3Entity354
{
    public int Id { get; set; }
    public int? Level4Id { get; set; }
    public Level4Entity354? Level4 { get; set; }
}

public class Level2Entity354
{
    public int Id { get; set; }
    public int? Level3Id { get; set; }
    public Level3Entity354? Level3 { get; set; }
}

public class Level1Entity354
{
    public int Id { get; set; }
    public int? Level2Id { get; set; }
    public Level2Entity354? Level2 { get; set; }
}

[Facet(typeof(Level5Entity354),
    Include = new[] { nameof(Level5Entity354.Id), nameof(Level5Entity354.Contact) },
    NestedFacets = new[] { typeof(ContactFacet354) })]
public partial class Level5Dto354
{
}

[Facet(typeof(Level4Entity354),
    Include = new[] { nameof(Level4Entity354.Id), nameof(Level4Entity354.Level5) },
    NestedFacets = new[] { typeof(Level5Dto354) })]
public partial class Level4Dto354
{
}

[Facet(typeof(Level3Entity354),
    Configuration = typeof(Level3Dto354Config),
    Include = new[] { nameof(Level3Entity354.Id), nameof(Level3Entity354.Level4) },
    NestedFacets = new[] { typeof(Level4Dto354) })]
public partial class Level3Dto354
{
}

public class Level3Dto354Config
    : IFacetProjectionMapConfiguration<Level3Entity354, Level3Dto354>
{
    public static void ConfigureProjection(IFacetProjectionBuilder<Level3Entity354, Level3Dto354> builder)
    {
        builder.Map(d => d.Id, s => s.Id + 30);
    }
}

[Facet(typeof(Level2Entity354),
    Include = new[] { nameof(Level2Entity354.Id), nameof(Level2Entity354.Level3) },
    NestedFacets = new[] { typeof(Level3Dto354) })]
public partial class Level2Dto354
{
}

[Facet(typeof(Level1Entity354),
    Include = new[] { nameof(Level1Entity354.Id), nameof(Level1Entity354.Level2) },
    NestedFacets = new[] { typeof(Level2Dto354) })]
public partial class Level1Dto354
{
}

[Facet(typeof(Level1Entity354),
    Configuration = typeof(Level1Dto354LazyConfig),
    Include = new[] { nameof(Level1Entity354.Id), nameof(Level1Entity354.Level2) },
    NestedFacets = new[] { typeof(Level2Dto354) })]
public partial class Level1Dto354Lazy
{
}

public class Level1Dto354LazyConfig
    : IFacetProjectionMapConfiguration<Level1Entity354, Level1Dto354Lazy>
{
    public static void ConfigureProjection(IFacetProjectionBuilder<Level1Entity354, Level1Dto354Lazy> builder)
    {
        builder.Map(d => d.Id, s => s.Id + 1000);
    }
}

public class BaseRootEntity354
{
    public int Id { get; set; }
    public int? Level2Id { get; set; }
    public Level2Entity354? Level2 { get; set; }
}

public class DerivedRootEntity354 : BaseRootEntity354
{
    public string Code { get; set; } = string.Empty;
}

[Facet(typeof(BaseRootEntity354),
    Include = new[] { nameof(BaseRootEntity354.Id), nameof(BaseRootEntity354.Level2) },
    NestedFacets = new[] { typeof(Level2Dto354) })]
public partial class BaseRootDto354
{
}

[Facet(typeof(DerivedRootEntity354),
    Include = new[] { nameof(DerivedRootEntity354.Code) })]
public partial class DerivedRootDto354 : BaseRootDto354
{
}

public class DeepProjectionDbContext354 : DbContext
{
    public DeepProjectionDbContext354(DbContextOptions<DeepProjectionDbContext354> options)
        : base(options)
    {
    }

    public DbSet<Level1Entity354> Level1Entities => Set<Level1Entity354>();
    public DbSet<DerivedRootEntity354> DerivedRootEntities => Set<DerivedRootEntity354>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AbstractContact354>()
            .HasDiscriminator<string>("ContactType")
            .HasValue<ConcreteContact354>("Concrete");

        modelBuilder.Entity<Level1Entity354>().HasKey(x => x.Id);
        modelBuilder.Entity<Level2Entity354>().HasKey(x => x.Id);
        modelBuilder.Entity<Level3Entity354>().HasKey(x => x.Id);
        modelBuilder.Entity<Level4Entity354>().HasKey(x => x.Id);
        modelBuilder.Entity<Level5Entity354>().HasKey(x => x.Id);
        modelBuilder.Entity<BaseRootEntity354>().HasKey(x => x.Id);

        modelBuilder.Entity<Level1Entity354>()
            .HasOne(x => x.Level2)
            .WithMany()
            .HasForeignKey(x => x.Level2Id);

        modelBuilder.Entity<Level2Entity354>()
            .HasOne(x => x.Level3)
            .WithMany()
            .HasForeignKey(x => x.Level3Id);

        modelBuilder.Entity<Level3Entity354>()
            .HasOne(x => x.Level4)
            .WithMany()
            .HasForeignKey(x => x.Level4Id);

        modelBuilder.Entity<Level4Entity354>()
            .HasOne(x => x.Level5)
            .WithMany()
            .HasForeignKey(x => x.Level5Id);

        modelBuilder.Entity<Level5Entity354>()
            .HasOne(x => x.Contact)
            .WithMany()
            .HasForeignKey(x => x.ContactId);

        modelBuilder.Entity<BaseRootEntity354>()
            .HasOne(x => x.Level2)
            .WithMany()
            .HasForeignKey(x => x.Level2Id);
    }
}
