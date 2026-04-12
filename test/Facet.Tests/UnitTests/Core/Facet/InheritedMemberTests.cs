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

// Facet inheriting from another Facet, verifies 'new' keyword on
// FromSource / ToSource / Projection / BackTo to suppress CS0108.
public class BaseValidationEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
}

public class DerivedValidationEntity : BaseValidationEntity
{
    public string Description { get; set; } = "";
}

// Base facet — generates FromSource, ToSource, Projection
[Facet(typeof(BaseValidationEntity), GenerateToSource = true)]
public partial class BaseValidationFacet;

// Derived facet inherits the base facet — without 'new' this would be CS0108
[Facet(typeof(DerivedValidationEntity), GenerateToSource = true)]
public partial class DerivedValidationFacet : BaseValidationFacet;

// -----------------------------------------------------------------------
// GitHub issue #325: two facets on the same domain model, derived facet
// inheriting from base facet. Without 'new', CS0108 is raised.
// -----------------------------------------------------------------------
public class ApplicationUser325
{
    public virtual string Id { get; set; } = default!;
    public virtual string? UserName { get; set; }
}

[Facet(typeof(ApplicationUser325), Include = [], GenerateToSource = true, PreserveRequiredProperties = true)]
public partial class UserCreateModel325
{
    [MapFrom(nameof(ApplicationUser325.UserName), Reversible = true)]
    public string? UserName { get; set; }
}

[Facet(typeof(ApplicationUser325), Include = [], GenerateToSource = true, PreserveRequiredProperties = true)]
public partial class UserEditModel325 : UserCreateModel325
{
    [MapFrom(nameof(ApplicationUser325.Id), Reversible = true)]
    public required string Id { get; set; }
}

// Source entity used for the MapFrom-on-derived-class tests
public class UserEntityForMapFromInheritance
{
    public int Id { get; set; }
    public bool Enabled { get; set; }
    public string Email { get; set; } = string.Empty;
}

// Base DTO renames Enabled -> Active via [MapFrom]
[Facet(typeof(UserEntityForMapFromInheritance), Include = [])]
public partial class UserCreateModelMapFromBase
{
    [MapFrom(nameof(UserEntityForMapFromInheritance.Enabled), Reversible = true)]
    public bool Active { get; set; }
}

// Derived DTO should inherit the Enabled->Active mapping; must NOT generate a new "Enabled" property
[Facet(typeof(UserEntityForMapFromInheritance), Include = [nameof(UserEntityForMapFromInheritance.Email)])]
public partial class UserDetailsModelMapFromDerived : UserCreateModelMapFromBase
{
}

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

    // -----------------------------------------------------------------------
    // new keyword tests — if the 'new' modifier is missing the project would
    // not compile (CS0108 is an error in strict pipelines), so a successful
    // build already proves the fix. These tests also verify runtime behaviour.
    // -----------------------------------------------------------------------

    [Fact]
    public void DerivedFacet_Constructor_ShouldMapAllProperties()
    {
        // Arrange
        var entity = new DerivedValidationEntity { Id = 5, Code = "XYZ", Description = "desc" };

        // Act — uses the generated constructor on the derived facet
        var facet = new DerivedValidationFacet(entity);

        // Assert
        facet.Id.Should().Be(5);
        facet.Code.Should().Be("XYZ");
        facet.Description.Should().Be("desc");
    }

    [Fact]
    public void DerivedFacet_ToSource_ShouldRoundTrip()
    {
        // Arrange
        var entity = new DerivedValidationEntity { Id = 7, Code = "ABC", Description = "hello" };
        var facet = new DerivedValidationFacet(entity);

        // Act — calls the 'new' ToSource on the derived facet
        var result = facet.ToSource();

        // Assert
        result.Id.Should().Be(7);
        result.Code.Should().Be("ABC");
        result.Description.Should().Be("hello");
    }

    [Fact]
    public void DerivedFacet_FromSource_ShouldReturnDerivedInstance()
    {
        // Arrange
        var entity = new DerivedValidationEntity { Id = 3, Code = "DEF", Description = "world" };

        // Act — calls the 'new' static FromSource on the derived facet
        var facet = DerivedValidationFacet.FromSource(entity);

        // Assert
        facet.Should().BeOfType<DerivedValidationFacet>();
        facet.Id.Should().Be(3);
        facet.Description.Should().Be("world");
    }

    // -----------------------------------------------------------------------
    // GitHub issue #322: derived DTO inheriting MapFrom from base DTO
    // -----------------------------------------------------------------------

    [Fact]
    public void DerivedDto_ShouldNotDeclarePropertyWithOriginalSourceName()
    {
        // "Enabled" must NOT appear as a declared property on the derived DTO;
        // only the renamed "Active" (declared on the base) should exist.
        var declaredProps = typeof(UserDetailsModelMapFromDerived)
            .GetProperties(System.Reflection.BindingFlags.DeclaredOnly |
                           System.Reflection.BindingFlags.Public |
                           System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        declaredProps.Should().NotContain("Enabled",
            because: "Enabled is mapped to Active in the base DTO and should not be re-generated");
    }

    [Fact]
    public void DerivedDto_Constructor_ShouldMapRenamedPropertyFromBase()
    {
        var entity = new UserEntityForMapFromInheritance { Id = 1, Enabled = true, Email = "a@b.com" };

        var facet = new UserDetailsModelMapFromDerived(entity);

        facet.Active.Should().BeTrue(because: "Active is the renamed property mapped from Enabled");
        facet.Email.Should().Be("a@b.com");
    }

    [Fact]
    public void DerivedDto_Projection_ShouldUseRenamedProperty()
    {
        var entities = new[]
        {
            new UserEntityForMapFromInheritance { Id = 1, Enabled = true,  Email = "x@y.com" },
            new UserEntityForMapFromInheritance { Id = 2, Enabled = false, Email = "y@z.com" },
        }.AsQueryable();

        var results = entities.Select(UserDetailsModelMapFromDerived.Projection).ToList();

        results[0].Active.Should().BeTrue();
        results[0].Email.Should().Be("x@y.com");
        results[1].Active.Should().BeFalse();
        results[1].Email.Should().Be("y@z.com");
    }

    // -----------------------------------------------------------------------
    // GitHub issue #325: two facets for the SAME domain model, derived facet
    // inheriting from base facet — 'new' keyword must be emitted.
    // -----------------------------------------------------------------------

    [Fact]
    public void Issue325_DerivedFacet_SameDomainModel_FromSource_ShouldReturnDerivedInstance()
    {
        var user = new ApplicationUser325 { Id = "u-1", UserName = "alice" };

        var facet = UserEditModel325.FromSource(user);

        facet.Should().BeOfType<UserEditModel325>();
        facet.Id.Should().Be("u-1");
        facet.UserName.Should().Be("alice");
    }

    [Fact]
    public void Issue325_DerivedFacet_SameDomainModel_ToSource_ShouldRoundTrip()
    {
        var facet = new UserEditModel325 { Id = "u-2", UserName = "bob" };

        var result = facet.ToSource();

        result.Id.Should().Be("u-2");
        result.UserName.Should().Be("bob");
    }

    [Fact]
    public void Issue325_DerivedFacet_SameDomainModel_Projection_ShouldMapAllProperties()
    {
        var users = new[]
        {
            new ApplicationUser325 { Id = "u-3", UserName = "carol" },
        }.AsQueryable();

        var results = users.Select(UserEditModel325.Projection).ToList();

        results[0].Id.Should().Be("u-3");
        results[0].UserName.Should().Be("carol");
    }
}
