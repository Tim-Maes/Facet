using Facet.Extensions;
using Facet.Extensions.EFCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Facet.Tests.UnitTests.Extensions.EFCore;

/// <summary>
/// Querying a type derived from the facet's declared source (e.g. an EF table-per-hierarchy
/// subtype) must project through the declared source's projection.
/// </summary>
public class DerivedSourceSelectFacetTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DerivedSourceDbContext _context;

    public DerivedSourceSelectFacetTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DerivedSourceDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new DerivedSourceDbContext(options);
        _context.Database.EnsureCreated();

        _context.People.AddRange(
            new TphPerson { Id = 1, Name = "Regular" },
            new TphAdmin { Id = 2, Name = "Ada", AdminLevel = 3 });
        _context.Customers.AddRange(
            new TphCustomer { Id = 1, Name = "Customer" },
            new TphVipCustomer { Id = 2, Name = "Vip", Discount = 0.2m });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task ToFacetsAsync_WithDerivedSourceType_ShouldProjectThroughDeclaredSource()
    {
        var dtos = await _context.Set<TphAdmin>().ToFacetsAsync<TphAdmin, TphPersonDto>();

        dtos.Should().ContainSingle();
        dtos[0].Id.Should().Be(2);
        dtos[0].Name.Should().Be("Ada");
    }

    [Fact]
    public async Task FirstFacetAsync_WithDerivedSourceType_ShouldProjectThroughDeclaredSource()
    {
        var dto = await _context.Set<TphAdmin>().FirstFacetAsync<TphAdmin, TphPersonDto>();

        dto.Should().NotBeNull();
        dto!.Name.Should().Be("Ada");
    }

    [Fact]
    public async Task ToFacetsAsync_WithDeclaredSourceType_ShouldStillReturnAllRows()
    {
        var dtos = await _context.People.OrderBy(p => p.Id).ToFacetsAsync<TphPerson, TphPersonDto>();

        dtos.Select(d => d.Name).Should().Equal("Regular", "Ada");
    }

    [Fact]
    public void SelectFacet_WithDerivedSourceType_InMemory_ShouldProject()
    {
        var admins = new[] { new TphAdmin { Id = 5, Name = "Grace", AdminLevel = 1 } }.AsQueryable();

        var result = admins.SelectFacet<TphAdmin, TphPersonDto>().ToList();

        result.Should().ContainSingle().Which.Name.Should().Be("Grace");
    }

    [Fact]
    public async Task ToFacetsAsync_MultiSourceFacet_WithDerivedSourceType_ShouldUseMatchingSourceProjection()
    {
        var dtos = await _context.Set<TphVipCustomer>().ToFacetsAsync<TphVipCustomer, TphContactDto>();

        dtos.Should().ContainSingle().Which.Name.Should().Be("Vip");
    }

    [Fact]
    public void SelectFacet_MultiSourceFacet_InferredSource_WithDerivedType_ShouldUseMatchingSourceProjection()
    {
        IQueryable vips = new[] { new TphVipCustomer { Id = 7, Name = "Inferred", Discount = 0.1m } }.AsQueryable();

        var result = vips.SelectFacet<TphContactDto>().ToList();

        result.Should().ContainSingle().Which.Name.Should().Be("Inferred");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

public class TphPerson
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class TphAdmin : TphPerson
{
    public int AdminLevel { get; set; }
}

public class TphCustomer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class TphVipCustomer : TphCustomer
{
    public decimal Discount { get; set; }
}

[Facet(typeof(TphPerson))]
public partial class TphPersonDto;

[Facet(typeof(TphPerson), Include = new[] { nameof(TphPerson.Id), nameof(TphPerson.Name) })]
[Facet(typeof(TphCustomer), Include = new[] { nameof(TphCustomer.Id), nameof(TphCustomer.Name) })]
public partial class TphContactDto;

public class DerivedSourceDbContext(DbContextOptions<DerivedSourceDbContext> options) : DbContext(options)
{
    public DbSet<TphPerson> People => Set<TphPerson>();
    public DbSet<TphCustomer> Customers => Set<TphCustomer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TphAdmin>();
        modelBuilder.Entity<TphVipCustomer>();
    }
}
