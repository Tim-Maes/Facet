namespace Facet.Tests.UnitTests.Core.Facet.MapFromMapWhenCombined;

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

[Facet(typeof(CombinedTestEntity))]
public partial class CombinedMapFromMapWhenFacet
{
    [MapFrom(nameof(CombinedTestEntity.FirstName))]
    [MapWhen("IsActive")]
    public string? DisplayName { get; set; }
}

[Facet(typeof(CombinedTestEntity))]
public partial class CombinedMultipleConditionsFacet
{
    [MapFrom(nameof(CombinedTestEntity.Email))]
    [MapWhen("IsActive")]
    [MapWhen("IsEmailVerified")]
    public string? VerifiedEmail { get; set; }
}

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
        var entity = new CombinedTestEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };

        var facet = new CombinedMapFromMapWhenFacet(entity);

        facet.Id.Should().Be(1);
        facet.DisplayName.Should().Be("John"); 
    }

    [Fact]
    public void Constructor_ShouldNotMap_WhenMapWhenConditionIsFalse()
    {
        var entity = new CombinedTestEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            IsActive = false 
        };

        var facet = new CombinedMapFromMapWhenFacet(entity);

        facet.Id.Should().Be(1);
        facet.DisplayName.Should().BeNull(); 
    }

    [Fact]
    public void Constructor_ShouldApplyMultipleConditions_AllTrue()
    {
        var entity = new CombinedTestEntity
        {
            Id = 1,
            Email = "john@example.com",
            IsActive = true,
            IsEmailVerified = true
        };

        var facet = new CombinedMultipleConditionsFacet(entity);

        facet.VerifiedEmail.Should().Be("john@example.com");
    }

    [Fact]
    public void Constructor_ShouldNotMap_WhenAnyConditionIsFalse()
    {
        var entity = new CombinedTestEntity
        {
            Id = 1,
            Email = "john@example.com",
            IsActive = true,
            IsEmailVerified = false 
        };

        var facet = new CombinedMultipleConditionsFacet(entity);

        facet.VerifiedEmail.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldApplyMapFromRenameWithStatusCondition()
    {
        var completedTime = new DateTime(2024, 1, 15, 10, 30, 0);
        var entity = new CombinedTestEntity
        {
            Id = 1,
            Status = OrderStatus.Completed,
            CompletedAt = completedTime
        };

        var facet = new CombinedStatusCheckFacet(entity);

        facet.FinishedAt.Should().Be(completedTime); 
    }

    [Fact]
    public void Constructor_ShouldNotMapRenamedProperty_WhenStatusConditionFails()
    {
        var entity = new CombinedTestEntity
        {
            Id = 1,
            Status = OrderStatus.Pending,
            CompletedAt = DateTime.Now
        };

        var facet = new CombinedStatusCheckFacet(entity);

        facet.FinishedAt.Should().BeNull(); 
    }

    [Fact]
    public void Projection_ShouldApplyBothMapFromAndMapWhen()
    {
        var entities = new[]
        {
            new CombinedTestEntity { Id = 1, FirstName = "Active", IsActive = true },
            new CombinedTestEntity { Id = 2, FirstName = "Inactive", IsActive = false }
        }.AsQueryable();

        var facets = entities.Select(CombinedMapFromMapWhenFacet.Projection).ToList();

        facets.Should().HaveCount(2);
        facets[0].DisplayName.Should().Be("Active"); 
        facets[1].DisplayName.Should().BeNull(); 
    }

    [Fact]
    public void Projection_ShouldApplyMultipleConditionsWithMapFrom()
    {
        var entities = new[]
        {
            new CombinedTestEntity { Id = 1, Email = "verified@example.com", IsActive = true, IsEmailVerified = true },
            new CombinedTestEntity { Id = 2, Email = "unverified@example.com", IsActive = true, IsEmailVerified = false },
            new CombinedTestEntity { Id = 3, Email = "inactive@example.com", IsActive = false, IsEmailVerified = true }
        }.AsQueryable();

        var facets = entities.Select(CombinedMultipleConditionsFacet.Projection).ToList();

        facets.Should().HaveCount(3);
        facets[0].VerifiedEmail.Should().Be("verified@example.com"); 
        facets[1].VerifiedEmail.Should().BeNull(); 
        facets[2].VerifiedEmail.Should().BeNull(); 
    }

    [Fact]
    public void FacetType_ShouldHaveCorrectPropertyNames()
    {
        var facetType = typeof(CombinedMapFromMapWhenFacet);
        var propertyNames = facetType.GetProperties().Select(p => p.Name).ToList();

        propertyNames.Should().Contain("DisplayName"); 
        propertyNames.Should().NotContain("FirstName"); 
    }

    #region Edge Cases

    [Fact]
    public void Constructor_ShouldHandleEmptyStrings_WithMapFromAndMapWhen()
    {
        var entity = new CombinedTestEntity
        {
            Id = 1,
            FirstName = "",
            IsActive = true
        };

        var facet = new CombinedMapFromMapWhenFacet(entity);

        facet.DisplayName.Should().Be("");
    }

    [Fact]
    public void Constructor_ShouldHandleNullEmail_WithMultipleConditions()
    {
        var entity = new CombinedTestEntity
        {
            Id = 1,
            Email = null,
            IsActive = true,
            IsEmailVerified = true
        };

        var facet = new CombinedMultipleConditionsFacet(entity);

        facet.VerifiedEmail.Should().BeNull();
    }

    [Fact]
    public void Projection_ShouldHandleAllStatusValues()
    {
        var entities = new[]
        {
            new CombinedTestEntity { Id = 1, Status = OrderStatus.Pending, CompletedAt = DateTime.Now },
            new CombinedTestEntity { Id = 2, Status = OrderStatus.Processing, CompletedAt = DateTime.Now },
            new CombinedTestEntity { Id = 3, Status = OrderStatus.Completed, CompletedAt = new DateTime(2024, 6, 15) },
            new CombinedTestEntity { Id = 4, Status = OrderStatus.Cancelled, CompletedAt = DateTime.Now }
        }.AsQueryable();

        var facets = entities.Select(CombinedStatusCheckFacet.Projection).ToList();

        facets[0].FinishedAt.Should().BeNull();
        facets[1].FinishedAt.Should().BeNull();
        facets[2].FinishedAt.Should().Be(new DateTime(2024, 6, 15));
        facets[3].FinishedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldNotMapWhenBothConditionsFalse()
    {
        var entity = new CombinedTestEntity
        {
            Id = 1,
            Email = "test@example.com",
            IsActive = false,
            IsEmailVerified = false
        };

        var facet = new CombinedMultipleConditionsFacet(entity);

        facet.VerifiedEmail.Should().BeNull();
    }

    #endregion
}
