namespace Facet.Tests.UnitTests.Core.Facet.MapFromMapWhenCombined;

// Test entity - moved to separate namespace to avoid nesting issues
public class CombinedTestEntity
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum OrderStatus
{
    Pending,
    Processing,
    Completed,
    Cancelled
}

// Test: MapFrom + MapWhen on same property
[Facet(typeof(CombinedTestEntity))]
public partial class CombinedMapFromMapWhenFacet
{
    // Rename property AND apply conditional mapping
    [MapFrom(nameof(CombinedTestEntity.FirstName))]
    [MapWhen("IsActive")]
    public string? DisplayName { get; set; }
}

// Test: MapFrom rename with multiple MapWhen conditions
[Facet(typeof(CombinedTestEntity))]
public partial class CombinedMultipleConditionsFacet
{
    [MapFrom(nameof(CombinedTestEntity.Email))]
    [MapWhen("IsActive")]
    [MapWhen("IsEmailVerified")]
    public string? VerifiedEmail { get; set; }
}

// Test: MapFrom + MapWhen with status check
[Facet(typeof(CombinedTestEntity))]
public partial class CombinedStatusCheckFacet
{
    [MapFrom(nameof(CombinedTestEntity.CompletedAt))]
    [MapWhen("Status == OrderStatus.Completed")]
    public DateTime? FinishedAt { get; set; }
}

/// <summary>
/// Tests for combining [MapFrom] and [MapWhen] attributes on the same property.
/// </summary>
public class MapFromMapWhenCombinedTests
{
    [Fact]
    public void Constructor_ShouldApplyBothMapFromAndMapWhen_WhenConditionIsTrue()
    {
        // Arrange
        var entity = new CombinedTestEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };

        // Act
        var facet = new CombinedMapFromMapWhenFacet(entity);

        // Assert
        facet.Id.Should().Be(1);
        facet.DisplayName.Should().Be("John"); // Mapped from FirstName, condition IsActive = true
    }

    [Fact]
    public void Constructor_ShouldNotMap_WhenMapWhenConditionIsFalse()
    {
        // Arrange
        var entity = new CombinedTestEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            IsActive = false // Condition is false
        };

        // Act
        var facet = new CombinedMapFromMapWhenFacet(entity);

        // Assert
        facet.Id.Should().Be(1);
        facet.DisplayName.Should().BeNull(); // Not mapped because IsActive = false
    }

    [Fact]
    public void Constructor_ShouldApplyMultipleConditions_AllTrue()
    {
        // Arrange
        var entity = new CombinedTestEntity
        {
            Id = 1,
            Email = "john@example.com",
            IsActive = true,
            IsEmailVerified = true
        };

        // Act
        var facet = new CombinedMultipleConditionsFacet(entity);

        // Assert
        facet.VerifiedEmail.Should().Be("john@example.com");
    }

    [Fact]
    public void Constructor_ShouldNotMap_WhenAnyConditionIsFalse()
    {
        // Arrange
        var entity = new CombinedTestEntity
        {
            Id = 1,
            Email = "john@example.com",
            IsActive = true,
            IsEmailVerified = false // One condition is false
        };

        // Act
        var facet = new CombinedMultipleConditionsFacet(entity);

        // Assert
        facet.VerifiedEmail.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldApplyMapFromRenameWithStatusCondition()
    {
        // Arrange
        var completedTime = new DateTime(2024, 1, 15, 10, 30, 0);
        var entity = new CombinedTestEntity
        {
            Id = 1,
            Status = OrderStatus.Completed,
            CompletedAt = completedTime
        };

        // Act
        var facet = new CombinedStatusCheckFacet(entity);

        // Assert
        facet.FinishedAt.Should().Be(completedTime); // Renamed from CompletedAt
    }

    [Fact]
    public void Constructor_ShouldNotMapRenamedProperty_WhenStatusConditionFails()
    {
        // Arrange
        var entity = new CombinedTestEntity
        {
            Id = 1,
            Status = OrderStatus.Pending,
            CompletedAt = DateTime.Now
        };

        // Act
        var facet = new CombinedStatusCheckFacet(entity);

        // Assert
        facet.FinishedAt.Should().BeNull(); // Not mapped because Status != Completed
    }

    [Fact]
    public void Projection_ShouldApplyBothMapFromAndMapWhen()
    {
        // Arrange
        var entities = new[]
        {
            new CombinedTestEntity { Id = 1, FirstName = "Active", IsActive = true },
            new CombinedTestEntity { Id = 2, FirstName = "Inactive", IsActive = false }
        }.AsQueryable();

        // Act
        var facets = entities.Select(CombinedMapFromMapWhenFacet.Projection).ToList();

        // Assert
        facets.Should().HaveCount(2);
        facets[0].DisplayName.Should().Be("Active"); // Condition true
        facets[1].DisplayName.Should().BeNull(); // Condition false
    }

    [Fact]
    public void Projection_ShouldApplyMultipleConditionsWithMapFrom()
    {
        // Arrange
        var entities = new[]
        {
            new CombinedTestEntity { Id = 1, Email = "verified@example.com", IsActive = true, IsEmailVerified = true },
            new CombinedTestEntity { Id = 2, Email = "unverified@example.com", IsActive = true, IsEmailVerified = false },
            new CombinedTestEntity { Id = 3, Email = "inactive@example.com", IsActive = false, IsEmailVerified = true }
        }.AsQueryable();

        // Act
        var facets = entities.Select(CombinedMultipleConditionsFacet.Projection).ToList();

        // Assert
        facets.Should().HaveCount(3);
        facets[0].VerifiedEmail.Should().Be("verified@example.com"); // Both conditions true
        facets[1].VerifiedEmail.Should().BeNull(); // IsEmailVerified = false
        facets[2].VerifiedEmail.Should().BeNull(); // IsActive = false
    }

    [Fact]
    public void FacetType_ShouldHaveCorrectPropertyNames()
    {
        // Arrange
        var facetType = typeof(CombinedMapFromMapWhenFacet);
        var propertyNames = facetType.GetProperties().Select(p => p.Name).ToList();

        // Assert
        propertyNames.Should().Contain("DisplayName"); // Renamed property
        propertyNames.Should().NotContain("FirstName"); // Original name should not exist
    }

    #region Edge Cases

    [Fact]
    public void Constructor_ShouldHandleEmptyStrings_WithMapFromAndMapWhen()
    {
        // Arrange
        var entity = new CombinedTestEntity
        {
            Id = 1,
            FirstName = "",
            IsActive = true
        };

        // Act
        var facet = new CombinedMapFromMapWhenFacet(entity);

        // Assert
        facet.DisplayName.Should().Be("");
    }

    [Fact]
    public void Constructor_ShouldHandleNullEmail_WithMultipleConditions()
    {
        // Arrange
        var entity = new CombinedTestEntity
        {
            Id = 1,
            Email = null,
            IsActive = true,
            IsEmailVerified = true
        };

        // Act
        var facet = new CombinedMultipleConditionsFacet(entity);

        // Assert
        facet.VerifiedEmail.Should().BeNull();
    }

    [Fact]
    public void Projection_ShouldHandleAllStatusValues()
    {
        // Arrange
        var entities = new[]
        {
            new CombinedTestEntity { Id = 1, Status = OrderStatus.Pending, CompletedAt = DateTime.Now },
            new CombinedTestEntity { Id = 2, Status = OrderStatus.Processing, CompletedAt = DateTime.Now },
            new CombinedTestEntity { Id = 3, Status = OrderStatus.Completed, CompletedAt = new DateTime(2024, 6, 15) },
            new CombinedTestEntity { Id = 4, Status = OrderStatus.Cancelled, CompletedAt = DateTime.Now }
        }.AsQueryable();

        // Act
        var facets = entities.Select(CombinedStatusCheckFacet.Projection).ToList();

        // Assert
        facets[0].FinishedAt.Should().BeNull();
        facets[1].FinishedAt.Should().BeNull();
        facets[2].FinishedAt.Should().Be(new DateTime(2024, 6, 15));
        facets[3].FinishedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenBothConditionsFalse()
    {
        // Arrange
        var entity = new CombinedTestEntity
        {
            Id = 1,
            Email = "test@example.com",
            IsActive = false,
            IsEmailVerified = false
        };

        // Act
        var facet = new CombinedMultipleConditionsFacet(entity);

        // Assert
        facet.VerifiedEmail.Should().BeNull();
    }

    #endregion
}
