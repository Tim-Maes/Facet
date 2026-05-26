namespace Facet.Tests.UnitTests.Core.Facet;

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

[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromSimpleFacet
{
    [MapFrom(nameof(MapFromTestEntity.FirstName), Reversible = true)]
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromMultipleFacet
{
    [MapFrom(nameof(MapFromTestEntity.FirstName), Reversible = true)]
    public string GivenName { get; set; } = string.Empty;

    [MapFrom(nameof(MapFromTestEntity.LastName), Reversible = true)]
    public string FamilyName { get; set; } = string.Empty;
}

[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromNonReversibleFacet
{
    [MapFrom(nameof(MapFromTestEntity.FirstName), Reversible = false)]
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromNoProjectionFacet
{
    [MapFrom(nameof(MapFromTestEntity.FirstName), IncludeInProjection = false)]
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromComputedFacet
{
    [MapFrom(nameof(MapFromTestEntity.FirstName))]
    public string DisplayName { get; set; } = string.Empty;

    [MapFrom(nameof(MapFromTestEntity.LastName))]
    public string Surname { get; set; } = string.Empty;
}

[Facet(typeof(MapFromTestEntity), GenerateToSource = true)]
public partial class MapFromExpressionFacet
{
    [MapFrom(nameof(MapFromTestEntity.FirstName) + " + \" \" + " + nameof(MapFromTestEntity.LastName))]
    public string FullName { get; set; } = string.Empty;
}

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

[Facet(typeof(MapFromNestedPropertyEntity),
    exclude: [nameof(MapFromNestedPropertyEntity.Company)], 
    GenerateToSource = false)]
public partial class MapFromSingleLevelPathFacet
{
    [MapFrom("Company.CompanyName")]
    public string CompanyName { get; set; } = string.Empty;

    [MapFrom("Company.Address")]
    public string CompanyAddress { get; set; } = string.Empty;
    
    [MapFrom(nameof(@MapFromNestedPropertyEntity.Company.CompanyName))]
    public string AlternativeCompanyName { get; set; } = string.Empty;
}

[Facet(typeof(MapFromMultiLevelEntity),
    exclude: [nameof(MapFromMultiLevelEntity.Employee)], 
    GenerateToSource = false)]
public partial class MapFromMultiLevelPathFacet
{
    [MapFrom("Employee.Company.CompanyName")]
    public string EmployeeCompanyName { get; set; } = string.Empty;

    [MapFrom("Employee.Company.Address")]
    public string EmployeeCompanyAddress { get; set; } = string.Empty;

    [MapFrom("Employee.Name")]
    public string EmployeeName { get; set; } = string.Empty;
}

public class MapFromGroupMembershipEntity
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public MapFromUserEntity? User { get; set; }
}

public class MapFromUserEntity
{
    public string Username { get; set; } = string.Empty;
    public int PrimaryGroupId { get; set; }
}

[Facet(typeof(MapFromGroupMembershipEntity),
    Include = [nameof(MapFromGroupMembershipEntity.Id), nameof(MapFromGroupMembershipEntity.GroupId)])]
public partial class MapFromNavigationExpressionFacet
{
    [MapFrom("User.Username")]
    public string Username { get; set; } = string.Empty;

    [MapFrom("User.PrimaryGroupId == GroupId")]
    public bool IsPrimary { get; set; }
}

public class MapFromTests
{
    [Fact]
    public void Constructor_ShouldMapSimplePropertyRename()
    {
        var entity = new MapFromTestEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Age = 30
        };

        var facet = new MapFromSimpleFacet(entity);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("John"); 
        facet.LastName.Should().Be("Doe");
        facet.Email.Should().Be("john@example.com");
        facet.Age.Should().Be(30);
    }

    [Fact]
    public void Constructor_ShouldMapMultiplePropertyRenames()
    {
        var entity = new MapFromTestEntity
        {
            Id = 2,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            Age = 25
        };

        var facet = new MapFromMultipleFacet(entity);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(2);
        facet.GivenName.Should().Be("Jane"); 
        facet.FamilyName.Should().Be("Smith"); 
        facet.Email.Should().Be("jane@example.com");
        facet.Age.Should().Be(25);
    }

    [Fact]
    public void ToSource_ShouldReverseSimpleMapping()
    {
        var facet = new MapFromSimpleFacet
        {
            Id = 3,
            Name = "Bob", 
            LastName = "Wilson",
            Email = "bob@example.com",
            Age = 40
        };

        var entity = facet.ToSource();

        entity.Should().NotBeNull();
        entity.Id.Should().Be(3);
        entity.FirstName.Should().Be("Bob"); 
        entity.LastName.Should().Be("Wilson");
        entity.Email.Should().Be("bob@example.com");
        entity.Age.Should().Be(40);
    }

    [Fact]
    public void ToSource_ShouldReverseMultipleMappings()
    {
        var facet = new MapFromMultipleFacet
        {
            Id = 4,
            GivenName = "Alice", 
            FamilyName = "Brown", 
            Email = "alice@example.com",
            Age = 35
        };

        var entity = facet.ToSource();

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
        var facet = new MapFromNonReversibleFacet
        {
            Id = 5,
            Name = "Charlie", // Not reversible.
            LastName = "Davis",
            Email = "charlie@example.com",
            Age = 45
        };

        var entity = facet.ToSource();

        entity.Should().NotBeNull();
        entity.Id.Should().Be(5);
        
        entity.FirstName.Should().BeEmpty();
        entity.LastName.Should().Be("Davis");
        entity.Email.Should().Be("charlie@example.com");
        entity.Age.Should().Be(45);
    }

    [Fact]
    public void Projection_ShouldMapSimplePropertyRename()
    {
        var entities = new[]
        {
            new MapFromTestEntity { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", Age = 30 },
            new MapFromTestEntity { Id = 2, FirstName = "Jane", LastName = "Smith", Email = "jane@example.com", Age = 25 }
        }.AsQueryable();

        var facets = entities.Select(MapFromSimpleFacet.Projection).ToList();

        facets.Should().HaveCount(2);
        facets[0].Id.Should().Be(1);
        facets[0].Name.Should().Be("John");
        facets[1].Id.Should().Be(2);
        facets[1].Name.Should().Be("Jane");
    }

    [Fact]
    public void Projection_ShouldMapMultiplePropertyRenames()
    {
        var entities = new[]
        {
            new MapFromTestEntity { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com", Age = 30 }
        }.AsQueryable();

        var facets = entities.Select(MapFromMultipleFacet.Projection).ToList();

        facets.Should().HaveCount(1);
        facets[0].GivenName.Should().Be("John");
        facets[0].FamilyName.Should().Be("Doe");
    }

    [Fact]
    public void NestedFacet_ShouldMapPropertyRenameInNestedType()
    {
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

        var facet = new MapFromNestedFacet(entity);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Employee");
        facet.Company.Should().NotBeNull();
        facet.Company!.Id.Should().Be(100);
        facet.Company.Name.Should().Be("Acme Corp"); 
        facet.Company.Address.Should().Be("123 Main St");
    }

    [Fact]
    public void NestedFacet_ToSource_ShouldReverseNestedMapping()
    {
        var facet = new MapFromNestedFacet
        {
            Id = 2,
            Name = "Manager",
            Company = new MapFromCompanyFacet
            {
                Id = 200,
                Name = "TechCo", 
                Address = "456 Tech Ave"
            }
        };

        var entity = facet.ToSource();

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
        var facetType = typeof(MapFromSimpleFacet);
        var properties = facetType.GetProperties();

        var propertyNames = properties.Select(p => p.Name).ToList();
        propertyNames.Should().Contain("Id");
        propertyNames.Should().Contain("Name");
        propertyNames.Should().Contain("LastName");
        propertyNames.Should().Contain("Email");
        propertyNames.Should().Contain("Age");
        propertyNames.Should().Contain("Projection"); 
        
        propertyNames.Should().NotContain("FirstName");
    }

    [Fact]
    public void MultiplePropertyRenames_ShouldNotGenerateDuplicateProperties()
    {
        var facetType = typeof(MapFromMultipleFacet);
        var properties = facetType.GetProperties();

        properties.Select(p => p.Name).Should().Contain("GivenName");
        properties.Select(p => p.Name).Should().Contain("FamilyName");
        properties.Select(p => p.Name).Should().NotContain("FirstName");
        properties.Select(p => p.Name).Should().NotContain("LastName");
    }

    [Fact]
    public void Constructor_ShouldMapComputedValues()
    {
        var entity = new MapFromTestEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Age = 30
        };

        var facet = new MapFromComputedFacet(entity);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.DisplayName.Should().Be("John"); 
        facet.Surname.Should().Be("Doe"); 
        facet.Email.Should().Be("john@example.com");
        facet.Age.Should().Be(30);
    }

    [Fact]
    public void ToSource_ShouldNotIncludeComputedValues_WhenReversibleIsFalse()
    {
        var facet = new MapFromComputedFacet
        {
            Id = 2,
            DisplayName = "Jane", // Not reversible by default.
            Surname = "Smith", // Not reversible by default.
            Email = "jane@example.com",
            Age = 25
        };

        var entity = facet.ToSource();

        entity.Should().NotBeNull();
        entity.Id.Should().Be(2);
        
        entity.FirstName.Should().BeEmpty();
        entity.LastName.Should().BeEmpty();
        entity.Email.Should().Be("jane@example.com");
        entity.Age.Should().Be(25);
    }

    [Fact]
    public void Projection_ShouldMapComputedValues()
    {
        var entities = new[]
        {
            new MapFromTestEntity { Id = 1, FirstName = "Alice", LastName = "Brown", Email = "alice@example.com", Age = 28 }
        }.AsQueryable();

        var facets = entities.Select(MapFromComputedFacet.Projection).ToList();

        facets.Should().HaveCount(1);
        facets[0].Id.Should().Be(1);
        facets[0].DisplayName.Should().Be("Alice");
        facets[0].Surname.Should().Be("Brown");
    }

    [Fact]
    public void Constructor_ShouldMapExpressionToFullName()
    {
        var entity = new MapFromTestEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Age = 30
        };

        var facet = new MapFromExpressionFacet(entity);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.FullName.Should().Be("John Doe"); 
        facet.Email.Should().Be("john@example.com");
        facet.Age.Should().Be(30);
    }

    [Fact]
    public void Projection_ShouldMapExpressionToFullName()
    {
        var entities = new[]
        {
            new MapFromTestEntity { Id = 1, FirstName = "Alice", LastName = "Smith", Email = "alice@example.com", Age = 28 },
            new MapFromTestEntity { Id = 2, FirstName = "Bob", LastName = "Jones", Email = "bob@example.com", Age = 35 }
        }.AsQueryable();

        var facets = entities.Select(MapFromExpressionFacet.Projection).ToList();

        facets.Should().HaveCount(2);
        facets[0].FullName.Should().Be("Alice Smith");
        facets[1].FullName.Should().Be("Bob Jones");
    }

    [Fact]
    public void Constructor_ShouldMapSingleLevelNestedPropertyPath()
    {
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

        var facet = new MapFromSingleLevelPathFacet(entity);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("John Doe");
        facet.CompanyName.Should().Be("Acme Corporation"); 
        facet.CompanyAddress.Should().Be("123 Main Street"); 
        facet.AlternativeCompanyName.Should().Be("Acme Corporation"); 
    }

    [Fact]
    public void Projection_ShouldMapSingleLevelNestedPropertyPath()
    {
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

        var facets = entities.Select(MapFromSingleLevelPathFacet.Projection).ToList();

        facets.Should().HaveCount(2);
        facets[0].CompanyName.Should().Be("TechCorp");
        facets[0].CompanyAddress.Should().Be("456 Tech Ave");
        facets[1].CompanyName.Should().Be("StartupInc");
        facets[1].CompanyAddress.Should().Be("789 Innovation Blvd");
    }

    [Fact]
    public void Constructor_ShouldMapMultiLevelNestedPropertyPath()
    {
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

        var facet = new MapFromMultiLevelPathFacet(entity);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.EmployeeName.Should().Be("Jane Smith"); 
        facet.EmployeeCompanyName.Should().Be("Global Enterprises"); 
        facet.EmployeeCompanyAddress.Should().Be("999 Corporate Plaza"); 
    }

    [Fact]
    public void Projection_ShouldMapMultiLevelNestedPropertyPath()
    {
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

        var facets = entities.Select(MapFromMultiLevelPathFacet.Projection).ToList();

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
        var entity = new MapFromNestedPropertyEntity
        {
            Id = 1,
            Name = "John Doe",
            Company = null 
        };

        var action = () => new MapFromSingleLevelPathFacet(entity);
        action.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void Projection_ShouldHandleNullIntermediatePropertyInNestedPath()
    {
        var entities = new[]
        {
            new MapFromNestedPropertyEntity
            {
                Id = 1,
                Name = "Alice",
                Company = null 
            }
        }.AsQueryable();

        var action = () => entities.Select(MapFromSingleLevelPathFacet.Projection).ToList();
        action.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void Constructor_NestedPropertyPath_ShouldNotGenerateDuplicateProperties()
    {
        var facetType = typeof(MapFromSingleLevelPathFacet);
        var properties = facetType.GetProperties();
        var propertyNames = properties.Select(p => p.Name).ToList();

        propertyNames.Should().Contain("CompanyName");
        propertyNames.Should().Contain("CompanyAddress");

        propertyNames.Should().Contain("Id");
        propertyNames.Should().Contain("Name");

        propertyNames.Should().NotContain("Company");
    }

    [Fact]
    public void Constructor_ShouldMapNavigationPropertyExpression()
    {
        var entity = new MapFromGroupMembershipEntity
        {
            Id = 42,
            GroupId = 7,
            User = new MapFromUserEntity { Username = "alice", PrimaryGroupId = 7 }
        };

        var facet = new MapFromNavigationExpressionFacet(entity);

        facet.Username.Should().Be("alice");
        facet.IsPrimary.Should().BeTrue("User.PrimaryGroupId (7) == GroupId (7)");
    }

    [Fact]
    public void Constructor_ShouldMapNavigationPropertyExpression_WhenNotPrimary()
    {
        var entity = new MapFromGroupMembershipEntity
        {
            Id = 43,
            GroupId = 7,
            User = new MapFromUserEntity { Username = "bob", PrimaryGroupId = 99 }
        };

        var facet = new MapFromNavigationExpressionFacet(entity);

        facet.Username.Should().Be("bob");
        facet.IsPrimary.Should().BeFalse("User.PrimaryGroupId (99) != GroupId (7)");
    }

    [Fact]
    public void Projection_ShouldMapNavigationPropertyExpression()
    {
        var entities = new List<MapFromGroupMembershipEntity>
        {
            new() { Id = 1, GroupId = 5, User = new MapFromUserEntity { Username = "alice", PrimaryGroupId = 5 } },
            new() { Id = 2, GroupId = 5, User = new MapFromUserEntity { Username = "bob", PrimaryGroupId = 9 } },
        };

        var results = entities.AsQueryable().Select(MapFromNavigationExpressionFacet.Projection).ToList();

        results[0].Username.Should().Be("alice");
        results[0].IsPrimary.Should().BeTrue();
        results[1].Username.Should().Be("bob");
        results[1].IsPrimary.Should().BeFalse();
    }
}
