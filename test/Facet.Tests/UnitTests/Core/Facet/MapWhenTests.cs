namespace Facet.Tests.UnitTests.Core.Facet;

// Test entities
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

// Basic boolean condition test
[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenBooleanFacet
{
    [MapWhen("IsActive")]
    public string? Email { get; set; }
}

// Equality comparison test
[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenEqualityFacet
{
    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? CompletedAt { get; set; }
}

// Null check test
[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenNullCheckFacet
{
    [MapWhen("Email != null")]
    public string? Email { get; set; }
}

// With default value test
[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenDefaultValueFacet
{
    [MapWhen("Price != null")]
    public decimal? Price { get; set; }
}

// Comparison operator test
[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenComparisonFacet
{
    [MapWhen("Age >= 18")]
    public string? Email { get; set; }
}

// Multiple conditions (AND logic) test
[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenMultipleConditionsFacet
{
    [MapWhen("IsActive")]
    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? CompletedAt { get; set; }
}

// Combined with other properties
[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenMixedFacet
{
    [MapWhen("IsActive")]
    public string? Email { get; set; }

    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? CompletedAt { get; set; }
}

// Exclude from projection test
[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenNoProjectionFacet
{
    [MapWhen("IsActive", IncludeInProjection = false)]
    public string? Email { get; set; }
}

// Negation test
[Facet(typeof(MapWhenTestEntity))]
public partial class MapWhenNegationFacet
{
    [MapWhen("!IsActive")]
    public string? Email { get; set; }
}

// Not equal test
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
        // Arrange
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Name = "Test",
            IsActive = true,
            Email = "test@example.com"
        };

        // Act
        var facet = new MapWhenBooleanFacet(entity);

        // Assert
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Test");
        facet.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenBooleanConditionIsFalse()
    {
        // Arrange
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Name = "Test",
            IsActive = false,
            Email = "test@example.com"
        };

        // Act
        var facet = new MapWhenBooleanFacet(entity);

        // Assert
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Test");
        facet.Email.Should().BeNull(); // Condition false, so default
    }

    [Fact]
    public void Constructor_ShouldMapWhenEqualityConditionIsTrue()
    {
        // Arrange
        var completedTime = new DateTime(2024, 1, 15, 10, 30, 0);
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Name = "Order",
            Status = OrderStatus.Completed,
            CompletedAt = completedTime
        };

        // Act
        var facet = new MapWhenEqualityFacet(entity);

        // Assert
        facet.Id.Should().Be(1);
        facet.CompletedAt.Should().Be(completedTime);
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenEqualityConditionIsFalse()
    {
        // Arrange
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Name = "Order",
            Status = OrderStatus.Pending,
            CompletedAt = DateTime.Now
        };

        // Act
        var facet = new MapWhenEqualityFacet(entity);

        // Assert
        facet.Id.Should().Be(1);
        facet.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapWhenNullCheckPasses()
    {
        // Arrange
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Email = "test@example.com"
        };

        // Act
        var facet = new MapWhenNullCheckFacet(entity);

        // Assert
        facet.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenNullCheckFails()
    {
        // Arrange
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Email = null
        };

        // Act
        var facet = new MapWhenNullCheckFacet(entity);

        // Assert
        facet.Email.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldReturnDefaultWhenConditionIsFalse()
    {
        // Arrange
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Price = null
        };

        // Act
        var facet = new MapWhenDefaultValueFacet(entity);

        // Assert
        facet.Price.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapWhenConditionIsTrue_WithNullableProperty()
    {
        // Arrange
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Price = 99.99m
        };

        // Act
        var facet = new MapWhenDefaultValueFacet(entity);

        // Assert
        facet.Price.Should().Be(99.99m);
    }

    [Fact]
    public void Constructor_ShouldMapWhenComparisonConditionIsTrue()
    {
        // Arrange
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Age = 21,
            Email = "adult@example.com"
        };

        // Act
        var facet = new MapWhenComparisonFacet(entity);

        // Assert
        facet.Email.Should().Be("adult@example.com");
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenComparisonConditionIsFalse()
    {
        // Arrange
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Age = 16,
            Email = "minor@example.com"
        };

        // Act
        var facet = new MapWhenComparisonFacet(entity);

        // Assert
        facet.Email.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapWhenAllMultipleConditionsAreTrue()
    {
        // Arrange
        var completedTime = new DateTime(2024, 1, 15, 10, 30, 0);
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            IsActive = true,
            Status = OrderStatus.Completed,
            CompletedAt = completedTime
        };

        // Act
        var facet = new MapWhenMultipleConditionsFacet(entity);

        // Assert
        facet.CompletedAt.Should().Be(completedTime);
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenAnyMultipleConditionIsFalse()
    {
        // Arrange - IsActive is false
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            IsActive = false,
            Status = OrderStatus.Completed,
            CompletedAt = DateTime.Now
        };

        // Act
        var facet = new MapWhenMultipleConditionsFacet(entity);

        // Assert
        facet.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Projection_ShouldApplyConditions()
    {
        // Arrange
        var entities = new[]
        {
            new MapWhenTestEntity { Id = 1, Name = "Active", IsActive = true, Email = "active@example.com" },
            new MapWhenTestEntity { Id = 2, Name = "Inactive", IsActive = false, Email = "inactive@example.com" }
        }.AsQueryable();

        // Act
        var facets = entities.Select(MapWhenBooleanFacet.Projection).ToList();

        // Assert
        facets.Should().HaveCount(2);
        facets[0].Email.Should().Be("active@example.com");
        facets[1].Email.Should().BeNull();
    }

    [Fact]
    public void Projection_ShouldApplyEqualityConditions()
    {
        // Arrange
        var completedTime = new DateTime(2024, 1, 15);
        var entities = new[]
        {
            new MapWhenTestEntity { Id = 1, Status = OrderStatus.Completed, CompletedAt = completedTime },
            new MapWhenTestEntity { Id = 2, Status = OrderStatus.Pending, CompletedAt = completedTime }
        }.AsQueryable();

        // Act
        var facets = entities.Select(MapWhenEqualityFacet.Projection).ToList();

        // Assert
        facets[0].CompletedAt.Should().Be(completedTime);
        facets[1].CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapMixedProperties()
    {
        // Arrange
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

        // Act
        var facet = new MapWhenMixedFacet(entity);

        // Assert
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Test Order");
        facet.Email.Should().Be("test@example.com");
        facet.CompletedAt.Should().Be(completedTime);
    }

    [Fact]
    public void Constructor_ShouldMapWhenNegationConditionIsTrue()
    {
        // Arrange - !IsActive is true when IsActive is false
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            IsActive = false,
            Email = "inactive@example.com"
        };

        // Act
        var facet = new MapWhenNegationFacet(entity);

        // Assert
        facet.Email.Should().Be("inactive@example.com");
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenNegationConditionIsFalse()
    {
        // Arrange - !IsActive is false when IsActive is true
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            IsActive = true,
            Email = "active@example.com"
        };

        // Act
        var facet = new MapWhenNegationFacet(entity);

        // Assert
        facet.Email.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapWhenNotEqualConditionIsTrue()
    {
        // Arrange
        var completedTime = new DateTime(2024, 1, 15);
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Status = OrderStatus.Completed,
            CompletedAt = completedTime
        };

        // Act
        var facet = new MapWhenNotEqualFacet(entity);

        // Assert
        facet.CompletedAt.Should().Be(completedTime);
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenNotEqualConditionIsFalse()
    {
        // Arrange
        var entity = new MapWhenTestEntity
        {
            Id = 1,
            Status = OrderStatus.Cancelled,
            CompletedAt = DateTime.Now
        };

        // Act
        var facet = new MapWhenNotEqualFacet(entity);

        // Assert
        facet.CompletedAt.Should().BeNull();
    }
}
