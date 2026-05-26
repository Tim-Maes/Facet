namespace Facet.Tests.UnitTests.Core.Facet;

public class MapWhenTestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal? Price { get; set; }
    public int Age { get; set; }
    public string? Email { get; set; }
}

public enum OrderStatus
{
    Pending,
    Processing,
    Completed,
    Cancelled
}

[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenBooleanFacet
{
    [MapWhen("IsActive")]
    public string? Email { get; set; }
}

[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenEqualityFacet
{
    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? CompletedAt { get; set; }
}

[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenNullCheckFacet
{
    [MapWhen("Email != null")]
    public string? Email { get; set; }
}

[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenDefaultValueFacet
{
    [MapWhen("Price != null")]
    public decimal? Price { get; set; }
}

[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenComparisonFacet
{
    [MapWhen("Age >= 18")]
    public string? Email { get; set; }
}

[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenMultipleConditionsFacet
{
    [MapWhen("IsActive")]
    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? CompletedAt { get; set; }
}

[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenMixedFacet
{
    [MapWhen("IsActive")]
    public string? Email { get; set; }

    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? CompletedAt { get; set; }
}

[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenNoProjectionFacet
{
    [MapWhen("IsActive", IncludeInProjection = false)]
    public string? Email { get; set; }
}

[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenNegationFacet
{
    [MapWhen("!IsActive")]
    public string? Email { get; set; }
}

[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenNotEqualFacet
{
    [MapWhen("Status != OrderStatus.Cancelled")]
    public DateTime? CompletedAt { get; set; }
}

public class MapWhenTests
{
    [Fact]
    public void Constructor_ShouldMapWhenBooleanConditionIsTrue()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Name = "Test",
            IsActive = true,
            Email = "test@example.com"
        };

        var facet = new MapWhenBooleanFacet(entity);

        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Test");
        facet.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenBooleanConditionIsFalse()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Name = "Test",
            IsActive = false,
            Email = "test@example.com"
        };

        var facet = new MapWhenBooleanFacet(entity);

        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Test");
        facet.Email.Should().BeNull(); 
    }

    [Fact]
    public void Constructor_ShouldMapWhenEqualityConditionIsTrue()
    {
        var completedTime = new DateTime(2024, 1, 15, 10, 30, 0);
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Name = "Order",
            Status = OrderStatus.Completed,
            CompletedAt = completedTime
        };

        var facet = new MapWhenEqualityFacet(entity);

        facet.Id.Should().Be(1);
        facet.CompletedAt.Should().Be(completedTime);
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenEqualityConditionIsFalse()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Name = "Order",
            Status = OrderStatus.Pending,
            CompletedAt = DateTime.Now
        };

        var facet = new MapWhenEqualityFacet(entity);

        facet.Id.Should().Be(1);
        facet.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapWhenNullCheckPasses()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Email = "test@example.com"
        };

        var facet = new MapWhenNullCheckFacet(entity);

        facet.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenNullCheckFails()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Email = null
        };

        var facet = new MapWhenNullCheckFacet(entity);

        facet.Email.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldReturnDefaultWhenConditionIsFalse()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Price = null
        };

        var facet = new MapWhenDefaultValueFacet(entity);

        facet.Price.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapWhenConditionIsTrue_WithNullableProperty()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Price = 99.99m
        };

        var facet = new MapWhenDefaultValueFacet(entity);

        facet.Price.Should().Be(99.99m);
    }

    [Fact]
    public void Constructor_ShouldMapWhenComparisonConditionIsTrue()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Age = 21,
            Email = "adult@example.com"
        };

        var facet = new MapWhenComparisonFacet(entity);

        facet.Email.Should().Be("adult@example.com");
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenComparisonConditionIsFalse()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Age = 16,
            Email = "minor@example.com"
        };

        var facet = new MapWhenComparisonFacet(entity);

        facet.Email.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapWhenAllMultipleConditionsAreTrue()
    {
        var completedTime = new DateTime(2024, 1, 15, 10, 30, 0);
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            IsActive = true,
            Status = OrderStatus.Completed,
            CompletedAt = completedTime
        };

        var facet = new MapWhenMultipleConditionsFacet(entity);

        facet.CompletedAt.Should().Be(completedTime);
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenAnyMultipleConditionIsFalse()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            IsActive = false,
            Status = OrderStatus.Completed,
            CompletedAt = DateTime.Now
        };

        var facet = new MapWhenMultipleConditionsFacet(entity);

        facet.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Projection_ShouldApplyConditions()
    {
        var entities = new[]
        {
            new MapWhenTestEntity { Id = 1, Name = "Active", IsActive = true, Email = "active@example.com" },
            new MapWhenTestEntity { Id = 2, Name = "Inactive", IsActive = false, Email = "inactive@example.com" }
        }.AsQueryable();

        var facets = entities.Select(MapWhenBooleanFacet.Projection).ToList();

        facets.Should().HaveCount(2);
        facets[0].Email.Should().Be("active@example.com");
        facets[1].Email.Should().BeNull();
    }

    [Fact]
    public void Projection_ShouldApplyEqualityConditions()
    {
        var completedTime = new DateTime(2024, 1, 15);
        var entities = new[]
        {
            new MapWhenTestEntity { Id = 1, Status = OrderStatus.Completed, CompletedAt = completedTime },
            new MapWhenTestEntity { Id = 2, Status = OrderStatus.Pending, CompletedAt = completedTime }
        }.AsQueryable();

        var facets = entities.Select(MapWhenEqualityFacet.Projection).ToList();

        facets[0].CompletedAt.Should().Be(completedTime);
        facets[1].CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapMixedProperties()
    {
        var completedTime = new DateTime(2024, 1, 15);
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Name = "Test Order",
            IsActive = true,
            Status = OrderStatus.Completed,
            Email = "test@example.com",
            CompletedAt = completedTime
        };

        var facet = new MapWhenMixedFacet(entity);

        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Test Order");
        facet.Email.Should().Be("test@example.com");
        facet.CompletedAt.Should().Be(completedTime);
    }

    [Fact]
    public void Constructor_ShouldMapWhenNegationConditionIsTrue()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            IsActive = false,
            Email = "inactive@example.com"
        };

        var facet = new MapWhenNegationFacet(entity);

        facet.Email.Should().Be("inactive@example.com");
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenNegationConditionIsFalse()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            IsActive = true,
            Email = "active@example.com"
        };

        var facet = new MapWhenNegationFacet(entity);

        facet.Email.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapWhenNotEqualConditionIsTrue()
    {
        var completedTime = new DateTime(2024, 1, 15);
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Status = OrderStatus.Completed,
            CompletedAt = completedTime
        };

        var facet = new MapWhenNotEqualFacet(entity);

        facet.CompletedAt.Should().Be(completedTime);
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenNotEqualConditionIsFalse()
    {
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Status = OrderStatus.Cancelled,
            CompletedAt = DateTime.Now
        };

        var facet = new MapWhenNotEqualFacet(entity);

        facet.CompletedAt.Should().BeNull();
    }
}
