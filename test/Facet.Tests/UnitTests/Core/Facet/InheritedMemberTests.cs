namespace Facet.Tests.UnitTests.Core.Facet;

// Test entities
public class InheritedMemberEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int State { get; set; }
    public string LocalName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

// Base class for facets
public abstract class BaseMemberFacet
{
    public int Id { get; set; }
    public int State { get; set; }
}

// Facet that inherits from base class - should not generate Id and State again
[Facet(typeof(InheritedMemberEntity), exclude: new[] { "LocalName" })]
public partial class InheritedMemberFacet : BaseMemberFacet
{
}

// Another base class scenario
public abstract class BaseWithName
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[Facet(typeof(InheritedMemberEntity), Include = new[] { "Id", "Name", "Description" })]
public partial class InheritedIncludeFacet : BaseWithName
{
}

// ToSource should include inherited facet base class properties

// Source entity hierarchy
public class ModifiedByBaseEntity
{
    public int Id { get; set; }
    public string ModifiedBy { get; set; } = "";
}

public class SettingEntity : ModifiedByBaseEntity
{
    public string ApplicationType { get; set; } = "";
    public string Settings { get; set; } = "";
    public string UserId { get; set; } = "";
}

// Facet DTO hierarchy
public class ModifiedByBaseDto
{
    public int Id { get; set; }
    public string ModifiedBy { get; set; } = "";
}

// Include only lists the SettingEntity-specific properties,
// but ModifiedByBaseDto has Id and ModifiedBy which should also be mapped.
[Facet(typeof(SettingEntity),
    Include = new[] { "ApplicationType", "Settings", "UserId" },
    GenerateToSource = true)]
public partial class SettingFacetDto : ModifiedByBaseDto;

// Same scenario but with Exclude mode instead of Include mode (for comparison)
[Facet(typeof(SettingEntity), GenerateToSource = true)]
public partial class SettingFacetExcludeDto : ModifiedByBaseDto;

public class InheritedMemberTests
{
    [Fact]
    public void Constructor_ShouldNotGenerateDuplicateProperties()
    {
        // Arrange
        var entity = new InheritedMemberEntity
        {
            Id = 1,
            Name = "Test",
            State = 42,
            LocalName = "Local",
            Description = "Description"
        };

        // Act
        var facet = new InheritedMemberFacet(entity);

        // Assert
        facet.Id.Should().Be(1);
        facet.State.Should().Be(42);
        facet.Name.Should().Be("Test");
        facet.Description.Should().Be("Description");
    }

    [Fact]
    public void FacetType_ShouldNotHaveDuplicateProperties()
    {
        // Verify that the facet type doesn't have duplicate properties
        var facetType = typeof(InheritedMemberFacet);
        var declaredProperties = facetType.GetProperties(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        // Id and State should NOT be in declared properties since they're inherited
        var propertyNames = declaredProperties.Select(p => p.Name).ToList();
        propertyNames.Should().NotContain("Id");
        propertyNames.Should().NotContain("State");

        // Name and Description should be in declared properties (not in base)
        propertyNames.Should().Contain("Name");
        propertyNames.Should().Contain("Description");
    }

    [Fact]
    public void IncludeMode_ShouldNotGenerateDuplicateProperties()
    {
        // Arrange
        var entity = new InheritedMemberEntity
        {
            Id = 2,
            Name = "Test2",
            Description = "Desc2"
        };

        // Act
        var facet = new InheritedIncludeFacet(entity);

        // Assert
        facet.Id.Should().Be(2);
        facet.Name.Should().Be("Test2");
        facet.Description.Should().Be("Desc2");

        // Verify no duplicate properties
        var facetType = typeof(InheritedIncludeFacet);
        var declaredProperties = facetType.GetProperties(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var propertyNames = declaredProperties.Select(p => p.Name).ToList();

        // Id and Name should NOT be declared since they're inherited
        propertyNames.Should().NotContain("Id");
        propertyNames.Should().NotContain("Name");

        // Description should be declared
        propertyNames.Should().Contain("Description");
    }

    [Fact]
    public void Projection_ShouldWorkWithInheritedProperties()
    {
        // Arrange
        var entities = new[]
        {
            new InheritedMemberEntity { Id = 1, Name = "Test1", State = 10, Description = "Desc1" },
            new InheritedMemberEntity { Id = 2, Name = "Test2", State = 20, Description = "Desc2" }
        }.AsQueryable();

        // Act
        var facets = entities.Select(InheritedMemberFacet.Projection).ToList();

        // Assert
        facets.Should().HaveCount(2);
        facets[0].Id.Should().Be(1);
        facets[0].State.Should().Be(10);
        facets[0].Name.Should().Be("Test1");
        facets[1].Id.Should().Be(2);
        facets[1].State.Should().Be(20);
        facets[1].Name.Should().Be("Test2");
    }

    [Fact]
    public void ToSource_IncludeMode_ShouldMapInheritedBaseClassProperties()
    {
        // Arrange — facet with Include mode, base class has Id and ModifiedBy
        var facet = new SettingFacetDto
        {
            Id = 42,
            ModifiedBy = "admin",
            ApplicationType = "Web",
            Settings = "{}",
            UserId = "user-1"
        };

        // Act
        var source = facet.ToSource();

        // Assert — all properties should be mapped, including inherited ones
        source.Should().NotBeNull();
        source.ApplicationType.Should().Be("Web");
        source.Settings.Should().Be("{}");
        source.UserId.Should().Be("user-1");
        source.Id.Should().Be(42, because: "Id is inherited from ModifiedByBaseDto and should be mapped to source");
        source.ModifiedBy.Should().Be("admin", because: "ModifiedBy is inherited from ModifiedByBaseDto and should be mapped to source");
    }

    [Fact]
    public void ToSource_ExcludeMode_ShouldMapInheritedBaseClassProperties()
    {
        // Arrange — facet with default (exclude) mode, base class has Id and ModifiedBy
        var facet = new SettingFacetExcludeDto
        {
            Id = 99,
            ModifiedBy = "system",
            ApplicationType = "API",
            Settings = "{\"key\":\"value\"}",
            UserId = "user-2"
        };

        // Act
        var source = facet.ToSource();

        // Assert — all properties should be mapped
        source.Should().NotBeNull();
        source.ApplicationType.Should().Be("API");
        source.Settings.Should().Be("{\"key\":\"value\"}");
        source.UserId.Should().Be("user-2");
        source.Id.Should().Be(99);
        source.ModifiedBy.Should().Be("system");
    }

    [Fact]
    public void Constructor_IncludeMode_ShouldMapInheritedBaseClassProperties()
    {
        // Arrange
        var entity = new SettingEntity
        {
            Id = 42,
            ModifiedBy = "admin",
            ApplicationType = "Web",
            Settings = "{}",
            UserId = "user-1"
        };

        // Act
        var facet = new SettingFacetDto(entity);

        // Assert — inherited properties should be populated from source
        facet.Id.Should().Be(42);
        facet.ModifiedBy.Should().Be("admin");
        facet.ApplicationType.Should().Be("Web");
        facet.Settings.Should().Be("{}");
        facet.UserId.Should().Be("user-1");
    }
}
