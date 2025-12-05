namespace Facet.Tests.UnitTests.Core.Facet;

// Test entities
public class MapFromTestEntity
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class MapFromNestedEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public MapFromCompanyEntity? Company { get; set; }
}

public class MapFromCompanyEntity
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

// Simple property rename test
[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromSimpleFacet
{
    [MapFrom(nameof(MapFromTestEntity.FirstName), Reversible = true)]
    public string Name { get; set; } = string.Empty;
}

// Multiple property renames
[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromMultipleFacet
{
    [MapFrom(nameof(MapFromTestEntity.FirstName), Reversible = true)]
    public string GivenName { get; set; } = string.Empty;

    [MapFrom(nameof(MapFromTestEntity.LastName), Reversible = true)]
    public string FamilyName { get; set; } = string.Empty;
}

// Non-reversible mapping
[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromNonReversibleFacet
{
    [MapFrom(nameof(MapFromTestEntity.FirstName), Reversible = false)]
    public string Name { get; set; } = string.Empty;
}

// Exclude from projection
[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromNoProjectionFacet
{
    [MapFrom(nameof(MapFromTestEntity.FirstName), IncludeInProjection = false)]
    public string Name { get; set; } = string.Empty;
}

// Computed value - one-way mapping (default Reversible = false)
[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromComputedFacet
{
    // Computed from FirstName - cannot be reversed
    [MapFrom(nameof(MapFromTestEntity.FirstName))]
    public string DisplayName { get; set; } = string.Empty;

    // Computed from LastName - cannot be reversed
    [MapFrom(nameof(MapFromTestEntity.LastName))]
    public string Surname { get; set; } = string.Empty;
}

// Computed expression - FirstName + LastName = FullName
[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromExpressionFacet
{
    // Computed expression - cannot be reversed
    [MapFrom(nameof(MapFromTestEntity.FirstName) + " + \" \" + " + nameof(MapFromTestEntity.LastName))]
    public string FullName { get; set; } = string.Empty;
}

// Nested facet with company
[Facet(typeof(MapFromCompanyEntity), GenerateToSource = true)]
public partial class MapFromCompanyFacet
{
    [MapFrom(nameof(MapFromCompanyEntity.CompanyName), Reversible = true)]
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(MapFromNestedEntity),
    NestedFacets = [typeof(MapFromCompanyFacet)],
    GenerateToSource = true)]
public partial class MapFromNestedFacet;

// ========================================
// Nested Property Path Tests (not nested facets!)
// These test [MapFrom("Property.Nested.Path")] using string literals
// ========================================

public class MapFromNestedPropertyEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public MapFromCompanyEntity? Company { get; set; }
}

public class MapFromMultiLevelEntity
{
    public int Id { get; set; }
    public MapFromNestedPropertyEntity? Employee { get; set; }
}

// Test single-level nested property path: Company.CompanyName
[Facet(typeof(MapFromNestedPropertyEntity),
    exclude: [nameof(MapFromNestedPropertyEntity.Company)], // Exclude navigation property
    GenerateToSource = false)]
public partial class MapFromSingleLevelPathFacet
{
    // Use string literal to access nested property
    [MapFrom("Company.CompanyName")]
    public string CompanyName { get; set; } = string.Empty;

    [MapFrom("Company.Address")]
    public string CompanyAddress { get; set; } = string.Empty;
}

// Test multi-level nested property path: Employee.Company.CompanyName
[Facet(typeof(MapFromMultiLevelEntity),
    exclude: [nameof(MapFromMultiLevelEntity.Employee)], // Exclude navigation property
    GenerateToSource = false)]
public partial class MapFromMultiLevelPathFacet
{
    // Two levels deep
    [MapFrom("Employee.Company.CompanyName")]
    public string EmployeeCompanyName { get; set; } = string.Empty;

    // Two levels deep
    [MapFrom("Employee.Company.Address")]
    public string EmployeeCompanyAddress { get; set; } = string.Empty;

    // Single level
    [MapFrom("Employee.Name")]
    public string EmployeeName { get; set; } = string.Empty;
}

public class MapFromTests
{
    [Fact]
    public void Constructor_ShouldMapSimplePropertyRename()
    {
        // Arrange
        var entity = new MapFromTestEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Age = 30
        };

        // Act
        var facet = new MapFromSimpleFacet(entity);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("John"); // Mapped from FirstName
        facet.LastName.Should().Be("Doe");
        facet.Email.Should().Be("john@example.com");
        facet.Age.Should().Be(30);
    }

    [Fact]
    public void Constructor_ShouldMapMultiplePropertyRenames()
    {
        // Arrange
        var entity = new MapFromTestEntity
        {
            Id = 2,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            Age = 25
        };

        // Act
        var facet = new MapFromMultipleFacet(entity);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(2);
        facet.GivenName.Should().Be("Jane"); // Mapped from FirstName
        facet.FamilyName.Should().Be("Smith"); // Mapped from LastName
        facet.Email.Should().Be("jane@example.com");
        facet.Age.Should().Be(25);
    }

    [Fact]
    public void ToSource_ShouldReverseSimpleMapping()
    {
        // Arrange
        var facet = new MapFromSimpleFacet
        {
            Id = 3,
            Name = "Bob", // Should map back to FirstName
            LastName = "Wilson",
            Email = "bob@example.com",
            Age = 40
        };

        // Act
        var entity = facet.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(3);
        entity.FirstName.Should().Be("Bob"); // Mapped from Name
        entity.LastName.Should().Be("Wilson");
        entity.Email.Should().Be("bob@example.com");
        entity.Age.Should().Be(40);
    }

    [Fact]
    public void ToSource_ShouldReverseMultipleMappings()
    {
        // Arrange
        var facet = new MapFromMultipleFacet
        {
            Id = 4,
            GivenName = "Alice", // Should map back to FirstName
            FamilyName = "Brown", // Should map back to LastName
            Email = "alice@example.com",
            Age = 35
        };

        // Act
        var entity = facet.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(4);
        entity.FirstName.Should().Be("Alice");
        entity.LastName.Should().Be("Brown");
        entity.Email.Should().Be("alice@example.com");
        entity.Age.Should().Be(35);
    }

    [Fact]
    public void ToSource_ShouldNotIncludeNonReversibleMapping()
    {
        // Arrange
        var facet = new MapFromNonReversibleFacet
        {
            Id = 5,
            Name = "Charlie", // Should NOT map back (Reversible = false)
            LastName = "Davis",
            Email = "charlie@example.com",
            Age = 45
        };

        // Act
        var entity = facet.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(5);
        // FirstName should be default since Name is not reversible
        entity.FirstName.Should().BeEmpty();
        entity.LastName.Should().Be("Davis");
        entity.Email.Should().Be("charlie@example.com");
        entity.Age.Should().Be(45);
    }

    [Fact]
    public void Projection_ShouldMapSimplePropertyRename()
    {
        // Arrange
        var entities = new[]
        {
            new MapFromTestEntity { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", Age = 30 },
            new MapFromTestEntity { Id = 2, FirstName = "Jane", LastName = "Smith", Email = "jane@example.com", Age = 25 }
        }.AsQueryable();

        // Act
        var facets = entities.Select(MapFromSimpleFacet.Projection).ToList();

        // Assert
        facets.Should().HaveCount(2);
        facets[0].Id.Should().Be(1);
        facets[0].Name.Should().Be("John");
        facets[1].Id.Should().Be(2);
        facets[1].Name.Should().Be("Jane");
    }

    [Fact]
    public void Projection_ShouldMapMultiplePropertyRenames()
    {
        // Arrange
        var entities = new[]
        {
            new MapFromTestEntity { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", Age = 30 }
        }.AsQueryable();

        // Act
        var facets = entities.Select(MapFromMultipleFacet.Projection).ToList();

        // Assert
        facets.Should().HaveCount(1);
        facets[0].GivenName.Should().Be("John");
        facets[0].FamilyName.Should().Be("Doe");
    }

    [Fact]
    public void NestedFacet_ShouldMapPropertyRenameInNestedType()
    {
        // Arrange
        var entity = new MapFromNestedEntity
        {
            Id = 1,
            Name = "Employee",
            Company = new MapFromCompanyEntity
            {
                Id = 100,
                CompanyName = "Acme Corp",
                Address = "123 Main St"
            }
        };

        // Act
        var facet = new MapFromNestedFacet(entity);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Employee");
        facet.Company.Should().NotBeNull();
        facet.Company!.Id.Should().Be(100);
        facet.Company.Name.Should().Be("Acme Corp"); // Mapped from CompanyName
        facet.Company.Address.Should().Be("123 Main St");
    }

    [Fact]
    public void NestedFacet_ToSource_ShouldReverseNestedMapping()
    {
        // Arrange
        var facet = new MapFromNestedFacet
        {
            Id = 2,
            Name = "Manager",
            Company = new MapFromCompanyFacet
            {
                Id = 200,
                Name = "TechCo", // Should map back to CompanyName
                Address = "456 Tech Ave"
            }
        };

        // Act
        var entity = facet.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(2);
        entity.Name.Should().Be("Manager");
        entity.Company.Should().NotBeNull();
        entity.Company!.Id.Should().Be(200);
        entity.Company.CompanyName.Should().Be("TechCo");
        entity.Company.Address.Should().Be("456 Tech Ave");
    }

    [Fact]
    public void SimplePropertyRename_ShouldNotGenerateDuplicateProperty()
    {
        // This test verifies that the facet type has the correct properties
        // and doesn't have duplicate properties from both user-declared and generated

        // Arrange & Act
        var facetType = typeof(MapFromSimpleFacet);
        var properties = facetType.GetProperties();

        // Assert - should have Id, Name (not FirstName), LastName, Email, Age, and Projection
        var propertyNames = properties.Select(p => p.Name).ToList();
        propertyNames.Should().Contain("Id");
        propertyNames.Should().Contain("Name");
        propertyNames.Should().Contain("LastName");
        propertyNames.Should().Contain("Email");
        propertyNames.Should().Contain("Age");
        propertyNames.Should().Contain("Projection"); // Static property
        // Should NOT contain FirstName since it was mapped to Name
        propertyNames.Should().NotContain("FirstName");
    }

    [Fact]
    public void MultiplePropertyRenames_ShouldNotGenerateDuplicateProperties()
    {
        // Arrange & Act
        var facetType = typeof(MapFromMultipleFacet);
        var properties = facetType.GetProperties();

        // Assert
        properties.Select(p => p.Name).Should().Contain("GivenName");
        properties.Select(p => p.Name).Should().Contain("FamilyName");
        properties.Select(p => p.Name).Should().NotContain("FirstName");
        properties.Select(p => p.Name).Should().NotContain("LastName");
    }

    [Fact]
    public void Constructor_ShouldMapComputedValues()
    {
        // Arrange
        var entity = new MapFromTestEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Age = 30
        };

        // Act
        var facet = new MapFromComputedFacet(entity);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.DisplayName.Should().Be("John"); // Mapped from FirstName
        facet.Surname.Should().Be("Doe"); // Mapped from LastName
        facet.Email.Should().Be("john@example.com");
        facet.Age.Should().Be(30);
    }

    [Fact]
    public void ToSource_ShouldNotIncludeComputedValues_WhenReversibleIsFalse()
    {
        // Arrange
        var facet = new MapFromComputedFacet
        {
            Id = 2,
            DisplayName = "Jane", // Should NOT map back (Reversible = false by default)
            Surname = "Smith", // Should NOT map back (Reversible = false by default)
            Email = "jane@example.com",
            Age = 25
        };

        // Act
        var entity = facet.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(2);
        // FirstName and LastName should be default because mappings are not reversible
        entity.FirstName.Should().BeEmpty();
        entity.LastName.Should().BeEmpty();
        entity.Email.Should().Be("jane@example.com");
        entity.Age.Should().Be(25);
    }

    [Fact]
    public void Projection_ShouldMapComputedValues()
    {
        // Arrange
        var entities = new[]
        {
            new MapFromTestEntity { Id = 1, FirstName = "Alice", LastName = "Brown", Email = "alice@example.com", Age = 28 }
        }.AsQueryable();

        // Act
        var facets = entities.Select(MapFromComputedFacet.Projection).ToList();

        // Assert
        facets.Should().HaveCount(1);
        facets[0].Id.Should().Be(1);
        facets[0].DisplayName.Should().Be("Alice");
        facets[0].Surname.Should().Be("Brown");
    }

    [Fact]
    public void Constructor_ShouldMapExpressionToFullName()
    {
        // Arrange
        var entity = new MapFromTestEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Age = 30
        };

        // Act
        var facet = new MapFromExpressionFacet(entity);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.FullName.Should().Be("John Doe"); // Computed from FirstName + " " + LastName
        facet.Email.Should().Be("john@example.com");
        facet.Age.Should().Be(30);
    }

    [Fact]
    public void Projection_ShouldMapExpressionToFullName()
    {
        // Arrange
        var entities = new[]
        {
            new MapFromTestEntity { Id = 1, FirstName = "Alice", LastName = "Smith", Email = "alice@example.com", Age = 28 },
            new MapFromTestEntity { Id = 2, FirstName = "Bob", LastName = "Jones", Email = "bob@example.com", Age = 35 }
        }.AsQueryable();

        // Act
        var facets = entities.Select(MapFromExpressionFacet.Projection).ToList();

        // Assert
        facets.Should().HaveCount(2);
        facets[0].FullName.Should().Be("Alice Smith");
        facets[1].FullName.Should().Be("Bob Jones");
    }

    // ========================================
    // Nested Property Path Tests
    // Testing [MapFrom("Property.Nested.Path")] with string literals
    // ========================================

    [Fact]
    public void Constructor_ShouldMapSingleLevelNestedPropertyPath()
    {
        // Arrange
        var entity = new MapFromNestedPropertyEntity
        {
            Id = 1,
            Name = "John Doe",
            Company = new MapFromCompanyEntity
            {
                Id = 100,
                CompanyName = "Acme Corporation",
                Address = "123 Main Street"
            }
        };

        // Act
        var facet = new MapFromSingleLevelPathFacet(entity);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("John Doe");
        facet.CompanyName.Should().Be("Acme Corporation"); // From Company.CompanyName
        facet.CompanyAddress.Should().Be("123 Main Street"); // From Company.Address
    }

    [Fact]
    public void Projection_ShouldMapSingleLevelNestedPropertyPath()
    {
        // Arrange
        var entities = new[]
        {
            new MapFromNestedPropertyEntity
            {
                Id = 1,
                Name = "Alice",
                Company = new MapFromCompanyEntity
                {
                    Id = 100,
                    CompanyName = "TechCorp",
                    Address = "456 Tech Ave"
                }
            },
            new MapFromNestedPropertyEntity
            {
                Id = 2,
                Name = "Bob",
                Company = new MapFromCompanyEntity
                {
                    Id = 200,
                    CompanyName = "StartupInc",
                    Address = "789 Innovation Blvd"
                }
            }
        }.AsQueryable();

        // Act
        var facets = entities.Select(MapFromSingleLevelPathFacet.Projection).ToList();

        // Assert
        facets.Should().HaveCount(2);
        facets[0].CompanyName.Should().Be("TechCorp");
        facets[0].CompanyAddress.Should().Be("456 Tech Ave");
        facets[1].CompanyName.Should().Be("StartupInc");
        facets[1].CompanyAddress.Should().Be("789 Innovation Blvd");
    }

    [Fact]
    public void Constructor_ShouldMapMultiLevelNestedPropertyPath()
    {
        // Arrange
        var entity = new MapFromMultiLevelEntity
        {
            Id = 1,
            Employee = new MapFromNestedPropertyEntity
            {
                Id = 50,
                Name = "Jane Smith",
                Company = new MapFromCompanyEntity
                {
                    Id = 100,
                    CompanyName = "Global Enterprises",
                    Address = "999 Corporate Plaza"
                }
            }
        };

        // Act
        var facet = new MapFromMultiLevelPathFacet(entity);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.EmployeeName.Should().Be("Jane Smith"); // From Employee.Name
        facet.EmployeeCompanyName.Should().Be("Global Enterprises"); // From Employee.Company.CompanyName
        facet.EmployeeCompanyAddress.Should().Be("999 Corporate Plaza"); // From Employee.Company.Address
    }

    [Fact]
    public void Projection_ShouldMapMultiLevelNestedPropertyPath()
    {
        // Arrange
        var entities = new[]
        {
            new MapFromMultiLevelEntity
            {
                Id = 1,
                Employee = new MapFromNestedPropertyEntity
                {
                    Id = 10,
                    Name = "Alice Johnson",
                    Company = new MapFromCompanyEntity
                    {
                        Id = 100,
                        CompanyName = "MegaCorp",
                        Address = "100 Business Park"
                    }
                }
            },
            new MapFromMultiLevelEntity
            {
                Id = 2,
                Employee = new MapFromNestedPropertyEntity
                {
                    Id = 20,
                    Name = "Bob Williams",
                    Company = new MapFromCompanyEntity
                    {
                        Id = 200,
                        CompanyName = "SmallBiz LLC",
                        Address = "200 Small St"
                    }
                }
            }
        }.AsQueryable();

        // Act
        var facets = entities.Select(MapFromMultiLevelPathFacet.Projection).ToList();

        // Assert
        facets.Should().HaveCount(2);
        facets[0].EmployeeName.Should().Be("Alice Johnson");
        facets[0].EmployeeCompanyName.Should().Be("MegaCorp");
        facets[0].EmployeeCompanyAddress.Should().Be("100 Business Park");
        facets[1].EmployeeName.Should().Be("Bob Williams");
        facets[1].EmployeeCompanyName.Should().Be("SmallBiz LLC");
        facets[1].EmployeeCompanyAddress.Should().Be("200 Small St");
    }

    [Fact]
    public void Constructor_ShouldHandleNullIntermediatePropertyInNestedPath()
    {
        // Arrange
        var entity = new MapFromNestedPropertyEntity
        {
            Id = 1,
            Name = "John Doe",
            Company = null // Null company
        };

        // Act & Assert
        // This should throw NullReferenceException because Company is null
        // and the generated code doesn't include null checks for nested paths
        var action = () => new MapFromSingleLevelPathFacet(entity);
        action.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void Projection_ShouldHandleNullIntermediatePropertyInNestedPath()
    {
        // Arrange
        var entities = new[]
        {
            new MapFromNestedPropertyEntity
            {
                Id = 1,
                Name = "Alice",
                Company = null // Null company
            }
        }.AsQueryable();

        // Act & Assert
        // EF Core projection with null nested path should also fail
        var action = () => entities.Select(MapFromSingleLevelPathFacet.Projection).ToList();
        action.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void Constructor_NestedPropertyPath_ShouldNotGenerateDuplicateProperties()
    {
        // Verify that MapFrom with nested paths doesn't create duplicate properties
        var facetType = typeof(MapFromSingleLevelPathFacet);
        var properties = facetType.GetProperties();
        var propertyNames = properties.Select(p => p.Name).ToList();

        // Should have our custom properties
        propertyNames.Should().Contain("CompanyName");
        propertyNames.Should().Contain("CompanyAddress");

        // Should have auto-generated properties from MapFromNestedPropertyEntity
        propertyNames.Should().Contain("Id");
        propertyNames.Should().Contain("Name");

        // Should NOT have the "Company" navigation property (it's mapped to nested paths)
        propertyNames.Should().NotContain("Company");
    }
}
