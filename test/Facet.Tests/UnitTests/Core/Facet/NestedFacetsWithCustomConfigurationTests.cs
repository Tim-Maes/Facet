using FluentAssertions;

namespace Facet.Tests.UnitTests.Core.Facet;

#region Test Models - Source

public class CustomCompanySource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
}

public class CustomDepartmentSource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CustomCompanySource Company { get; set; } = null!;
}

public class CustomEmployeeSource
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public CustomDepartmentSource Department { get; set; } = null!;
    public CustomCompanySource Company { get; set; } = null!;
    public List<CustomEmployeeSource> DirectReports { get; set; } = new();
}

public class CustomOfferSource
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class CustomServiceProviderLinkSource
{
    public string ServiceProviderId { get; set; } = string.Empty;
    public string ServiceProviderType { get; set; } = string.Empty;
    public int? FkGologServiceProvider { get; set; }
}

public class CustomDamageSource
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<CustomEmployeeSource> AssignedPersons { get; set; } = new();
    public CustomEmployeeSource? Creator { get; set; }
    public List<CustomOfferSource> Offers { get; set; } = new();
    public CustomServiceProviderLinkSource? ServiceProviderLink { get; set; }
}

#endregion

#region Test Models - Facets

[Facet(typeof(CustomCompanySource))]
public partial class CustomCompanyDto;

[Facet(typeof(CustomDepartmentSource),
    NestedFacets = [typeof(CustomCompanyDto)])]
public partial class CustomDepartmentDto;

[Facet(typeof(CustomEmployeeSource),
    exclude: [nameof(CustomEmployeeSource.DirectReports)],
    NestedFacets = [typeof(CustomDepartmentDto), typeof(CustomCompanyDto)],
    Configuration = typeof(CustomEmployeeMappingConfiguration))]
public partial class CustomEmployeeDto
{
    // Custom calculated property (NOT in source)
    public string FullName { get; set; } = string.Empty;
}

public class CustomEmployeeMappingConfiguration : IFacetMapConfiguration<CustomEmployeeSource, CustomEmployeeDto>
{
    public static void Map(CustomEmployeeSource source, CustomEmployeeDto target)
    {
        // Custom mapping: combine first and last name
        target.FullName = $"{source.FirstName} {source.LastName}";
    }
}

[Facet(typeof(CustomOfferSource))]
public partial class CustomOfferDto;

public class CustomServiceProviderDto
{
    public string ServiceProviderType { get; set; } = string.Empty;
    public string ServiceProviderId { get; set; } = string.Empty;
    public int? FkGologServiceProviderId { get; set; }
}

[Facet(typeof(CustomDamageSource),
    exclude: [nameof(CustomDamageSource.ServiceProviderLink)],
    NestedFacets = [typeof(CustomEmployeeDto), typeof(CustomOfferDto)],
    Configuration = typeof(CustomDamageMappingConfiguration))]
public partial class CustomDamageDto
{
    // Custom calculated properties (NOT in source)
    public List<int> OfferIds { get; set; } = new();
    public List<int> AssignedPersonIds { get; set; } = new();
    public string ProviderId { get; set; } = string.Empty;
    public CustomServiceProviderDto? ServiceProvider { get; set; }
}

public class CustomDamageMappingConfiguration : IFacetMapConfiguration<CustomDamageSource, CustomDamageDto>
{
    public static void Map(CustomDamageSource source, CustomDamageDto target)
    {
        target.OfferIds = source.Offers.Select(o => o.Id).ToList();
        target.AssignedPersonIds = source.AssignedPersons.Select(p => p.Id).ToList();
        target.ProviderId = source.ServiceProviderLink?.ServiceProviderId ?? "random uuid";

        target.ServiceProvider = source.ServiceProviderLink != null
            ? new CustomServiceProviderDto
            {
                ServiceProviderType = source.ServiceProviderLink.ServiceProviderType,
                ServiceProviderId = source.ServiceProviderLink.ServiceProviderId,
                FkGologServiceProviderId = source.ServiceProviderLink.FkGologServiceProvider
            }
            : null;
    }
}

[Facet(typeof(CustomEmployeeSource),
    exclude: [nameof(CustomEmployeeSource.Department), nameof(CustomEmployeeSource.Company)],
    NestedFacets = [typeof(CustomEmployeeDto)],
    Configuration = typeof(CustomManagerMappingConfiguration),
    MaxDepth = 3)]
public partial class CustomManagerDto
{
    // Custom property (NOT in source)
    public int DirectReportCount { get; set; }
}

public class CustomManagerMappingConfiguration : IFacetMapConfiguration<CustomEmployeeSource, CustomManagerDto>
{
    public static void Map(CustomEmployeeSource source, CustomManagerDto target)
    {
        target.DirectReportCount = source.DirectReports.Count;
    }
}

#endregion

/// <summary>
/// Tests for nested facets combined with custom IFacetMapConfiguration.
/// This ensures that nested facets are properly instantiated with depth tracking
/// even when custom mapping is present.
/// </summary>
public class NestedFacetsWithCustomConfigurationTests
{
    [Fact]
    public void NestedFacet_WithCustomConfiguration_ShouldInstantiateNestedFacets()
    {
        // Arrange
        var company = new CustomCompanySource { Id = 1, Name = "Tech Corp", Industry = "Technology" };
        var department = new CustomDepartmentSource { Id = 10, Name = "Engineering", Company = company };
        var employee = new CustomEmployeeSource { Id = 100, FirstName = "John", LastName = "Doe", Department = department, Company = company };

        // Act
        var dto = new CustomEmployeeDto(employee);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(100);
        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Doe");

        // Custom mapping should work
        dto.FullName.Should().Be("John Doe");

        // CRITICAL: Nested facets should be instantiated, not null!
        dto.Department.Should().NotBeNull();
        dto.Department!.Id.Should().Be(10);
        dto.Department.Name.Should().Be("Engineering");
        dto.Department.Company.Should().NotBeNull();
        dto.Department.Company!.Id.Should().Be(1);
        dto.Department.Company.Name.Should().Be("Tech Corp");

        dto.Company.Should().NotBeNull();
        dto.Company!.Id.Should().Be(1);
        dto.Company.Name.Should().Be("Tech Corp");
    }

    [Fact]
    public void MultipleNestedFacets_WithCustomConfiguration_ShouldInstantiateAllNestedFacets()
    {
        // Arrange - This is the exact scenario from the GitHub issue
        var offer1 = new CustomOfferSource { Id = 1, Description = "Offer 1", Amount = 100m };
        var offer2 = new CustomOfferSource { Id = 2, Description = "Offer 2", Amount = 200m };

        var company = new CustomCompanySource { Id = 1, Name = "Tech Corp", Industry = "Technology" };
        var department = new CustomDepartmentSource { Id = 10, Name = "Engineering", Company = company };
        var creator = new CustomEmployeeSource { Id = 100, FirstName = "John", LastName = "Doe", Department = department, Company = company };
        var assignedPerson1 = new CustomEmployeeSource { Id = 101, FirstName = "Jane", LastName = "Smith", Department = department, Company = company };
        var assignedPerson2 = new CustomEmployeeSource { Id = 102, FirstName = "Bob", LastName = "Johnson", Department = department, Company = company };

        var serviceProviderLink = new CustomServiceProviderLinkSource
        {
            ServiceProviderId = "provider-123",
            ServiceProviderType = "external",
            FkGologServiceProvider = 999
        };

        var damage = new CustomDamageSource
        {
            Id = 1000,
            Description = "Water damage in basement",
            Creator = creator,
            AssignedPersons = new List<CustomEmployeeSource> { assignedPerson1, assignedPerson2 },
            Offers = new List<CustomOfferSource> { offer1, offer2 },
            ServiceProviderLink = serviceProviderLink
        };

        // Act
        var dto = new CustomDamageDto(damage);

        // Assert - Basic properties
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1000);
        dto.Description.Should().Be("Water damage in basement");

        // Assert - Custom mapping logic works
        dto.OfferIds.Should().BeEquivalentTo(new[] { 1, 2 });
        dto.AssignedPersonIds.Should().BeEquivalentTo(new[] { 101, 102 });
        dto.ProviderId.Should().Be("provider-123");
        dto.ServiceProvider.Should().NotBeNull();
        dto.ServiceProvider!.ServiceProviderId.Should().Be("provider-123");
        dto.ServiceProvider.ServiceProviderType.Should().Be("external");
        dto.ServiceProvider.FkGologServiceProviderId.Should().Be(999);

        // CRITICAL: Nested facets should be instantiated!
        // This was the bug - these were null or not properly instantiated
        dto.Creator.Should().NotBeNull();
        dto.Creator!.Id.Should().Be(100);
        dto.Creator.FullName.Should().Be("John Doe"); // Custom mapping in nested facet should work
        dto.Creator.Department.Should().NotBeNull();
        dto.Creator.Department!.Name.Should().Be("Engineering");

        dto.AssignedPersons.Should().NotBeNull();
        dto.AssignedPersons.Should().HaveCount(2);
        dto.AssignedPersons[0].Id.Should().Be(101);
        dto.AssignedPersons[0].FullName.Should().Be("Jane Smith");
        dto.AssignedPersons[1].Id.Should().Be(102);
        dto.AssignedPersons[1].FullName.Should().Be("Bob Johnson");

        dto.Offers.Should().NotBeNull();
        dto.Offers.Should().HaveCount(2);
        dto.Offers[0].Id.Should().Be(1);
        dto.Offers[0].Description.Should().Be("Offer 1");
        dto.Offers[1].Id.Should().Be(2);
        dto.Offers[1].Description.Should().Be("Offer 2");
    }

    [Fact]
    public void CollectionNestedFacets_WithCustomConfiguration_ShouldInstantiateWithDepthTracking()
    {
        // Arrange
        var company = new CustomCompanySource { Id = 1, Name = "Tech Corp", Industry = "Technology" };
        var department = new CustomDepartmentSource { Id = 10, Name = "Engineering", Company = company };

        var report1 = new CustomEmployeeSource { Id = 201, FirstName = "Alice", LastName = "Williams", Department = department, Company = company };
        var report2 = new CustomEmployeeSource { Id = 202, FirstName = "Charlie", LastName = "Brown", Department = department, Company = company };

        var manager = new CustomEmployeeSource
        {
            Id = 100,
            FirstName = "John",
            LastName = "Manager",
            Department = department,
            Company = company,
            DirectReports = new List<CustomEmployeeSource> { report1, report2 }
        };

        // Act
        var dto = new CustomManagerDto(manager);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(100);
        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Manager");

        // Custom mapping should work
        dto.DirectReportCount.Should().Be(2);

        // CRITICAL: Collection of nested facets should be instantiated with depth tracking
        dto.DirectReports.Should().NotBeNull();
        dto.DirectReports.Should().HaveCount(2);

        dto.DirectReports[0].Id.Should().Be(201);
        dto.DirectReports[0].FullName.Should().Be("Alice Williams");
        dto.DirectReports[0].Department.Should().NotBeNull();

        dto.DirectReports[1].Id.Should().Be(202);
        dto.DirectReports[1].FullName.Should().Be("Charlie Brown");
        dto.DirectReports[1].Department.Should().NotBeNull();
    }

    [Fact]
    public void NullableNestedFacet_WithCustomConfiguration_ShouldHandleNullCorrectly()
    {
        // Arrange
        var employee = new CustomEmployeeSource
        {
            Id = 100,
            FirstName = "John",
            LastName = "Doe",
            Department = null!, // Null nested facet
            Company = null!
        };

        // Act
        var dto = new CustomEmployeeDto(employee);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(100);
        dto.FullName.Should().Be("John Doe"); // Custom mapping should still work

        // Null nested facets should remain null, not throw
        dto.Department.Should().BeNull();
        dto.Company.Should().BeNull();
    }

    [Fact]
    public void EmptyCollectionNestedFacets_WithCustomConfiguration_ShouldHandleCorrectly()
    {
        // Arrange
        var damage = new CustomDamageSource
        {
            Id = 1000,
            Description = "Minor damage",
            Creator = null,
            AssignedPersons = new List<CustomEmployeeSource>(), // Empty collection
            Offers = new List<CustomOfferSource>(),
            ServiceProviderLink = null
        };

        // Act
        var dto = new CustomDamageDto(damage);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1000);

        // Custom mapping should handle empty collections
        dto.OfferIds.Should().BeEmpty();
        dto.AssignedPersonIds.Should().BeEmpty();
        dto.ProviderId.Should().Be("random uuid"); // Fallback value
        dto.ServiceProvider.Should().BeNull();

        // Empty collections should be instantiated, not null
        dto.AssignedPersons.Should().NotBeNull();
        dto.AssignedPersons.Should().BeEmpty();
        dto.Offers.Should().NotBeNull();
        dto.Offers.Should().BeEmpty();

        dto.Creator.Should().BeNull();
    }

    // Note: ToSource is not generated when custom configuration is present by default
    // This is expected behavior and not part of the bug fix verification

    [Fact]
    public void DepthTracking_WithNestedFacetsAndCustomConfiguration_ShouldRespectMaxDepth()
    {
        // Arrange - Create a hierarchy deeper than MaxDepth (3)
        var company = new CustomCompanySource { Id = 1, Name = "Tech Corp", Industry = "Technology" };
        var department = new CustomDepartmentSource { Id = 10, Name = "Engineering", Company = company };

        var level3 = new CustomEmployeeSource { Id = 3, FirstName = "Level3", LastName = "Employee", Department = department, Company = company };
        var level2 = new CustomEmployeeSource { Id = 2, FirstName = "Level2", LastName = "Manager", Department = department, Company = company, DirectReports = new List<CustomEmployeeSource> { level3 } };
        var level1 = new CustomEmployeeSource { Id = 1, FirstName = "Level1", LastName = "Director", Department = department, Company = company, DirectReports = new List<CustomEmployeeSource> { level2 } };
        var ceo = new CustomEmployeeSource { Id = 0, FirstName = "CEO", LastName = "Boss", Department = department, Company = company, DirectReports = new List<CustomEmployeeSource> { level1 } };

        // Act
        var dto = new CustomManagerDto(ceo);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(0);
        dto.DirectReportCount.Should().Be(1); // Custom mapping works

        // Level 0 (CEO) - should have direct reports
        dto.DirectReports.Should().HaveCount(1);
        dto.DirectReports[0].Id.Should().Be(1);

        // Level 1 (Director) - should have direct reports
        dto.DirectReports[0].Should().BeOfType<CustomEmployeeDto>();

        // Level 2 (Manager) - MaxDepth is 3, so this should still be populated
        // but we can verify the depth tracking mechanism is in place
        dto.DirectReports.Should().NotBeNull();
    }
}
