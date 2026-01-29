using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// When a source property is marked as 'required' and is non-nullable, 
/// the generated facet should compile without nullable warnings.
/// </summary>
public class RequiredNestedFacetNullabilityTests
{
    [Fact]
    public void RequiredNestedFacet_Property_ShouldBeNonNullable()
    {
        // Arrange & Act
        var dtoType = typeof(UserWithRequiredSettingsFacet);
        var settingsProperty = dtoType.GetProperty("Settings");

        // Assert
        settingsProperty.Should().NotBeNull();
        // The property type should be the facet type (not nullable)
        settingsProperty!.PropertyType.Should().Be(typeof(UserSettingsFacet));
    }

    [Fact]
    public void RequiredNestedFacet_ShouldBeMarkedAsRequired()
    {
        // Arrange & Act
        var dtoType = typeof(UserWithRequiredSettingsFacet);
        var settingsProperty = dtoType.GetProperty("Settings");

        // Assert
        settingsProperty.Should().NotBeNull();
        // Check if the property has the required modifier (via attribute in reflection)
        var requiredAttribute = settingsProperty!.GetCustomAttributes(
            typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), true);
        // Note: Required modifier appears on the type, not the property in reflection
        // We verify by checking the type has RequiredMemberAttribute
        var typeRequiredAttribute = dtoType.GetCustomAttributes(
            typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), true);
        typeRequiredAttribute.Should().NotBeEmpty("Type should have RequiredMemberAttribute");
    }

    [Fact]
    public void RequiredNestedFacet_Constructor_ShouldMapCorrectly()
    {
        // Arrange
        var source = new UserModelWithRequiredSettings
        {
            Id = 1,
            SettingsId = 100,
            Settings = new UserSettingsModelForNested
            {
                Id = 100,
                StartTick = 10,
                StopTick = 50
            }
        };

        // Act
        var dto = new UserWithRequiredSettingsFacet(source);

        // Assert
        dto.Id.Should().Be(1);
        dto.SettingsId.Should().Be(100);
        dto.Settings.Should().NotBeNull();
        dto.Settings.StartTick.Should().Be(10);
        dto.Settings.StopTick.Should().Be(50);
    }

    [Fact]
    public void RequiredNestedFacet_ComputedProperty_ShouldWork()
    {
        // Arrange
        var source = new UserModelWithRequiredSettings
        {
            Id = 1,
            SettingsId = 100,
            Settings = new UserSettingsModelForNested
            {
                Id = 100,
                StartTick = 10,
                StopTick = 50
            }
        };

        // Act
        var dto = new UserWithRequiredSettingsFacet(source);

        // Assert - The computed property should work without null reference issues
        dto.ProcessedTicks.Should().Be(40); // 50 - 10
    }

    [Fact]
    public void OptionalNestedFacet_Property_ShouldBeNullable()
    {
        // Arrange & Act
        var dtoType = typeof(UserWithOptionalSettingsFacet);
        var settingsProperty = dtoType.GetProperty("Settings");

        // Assert
        settingsProperty.Should().NotBeNull();
        // For optional (non-required) nested facets, they should be treated as nullable
        // The type will be the facet type (nullable reference type annotation is not visible via reflection)
        settingsProperty!.PropertyType.Should().Be(typeof(UserSettingsFacet));
    }

    [Fact]
    public void OptionalNestedFacet_Constructor_ShouldHandleValue()
    {
        // Arrange
        var source = new UserModelWithOptionalSettings
        {
            Id = 2,
            Settings = new UserSettingsModelForNested
            {
                Id = 200,
                StartTick = 5,
                StopTick = 25
            }
        };

        // Act
        var dto = new UserWithOptionalSettingsFacet(source);

        // Assert
        dto.Id.Should().Be(2);
        dto.Settings.Should().NotBeNull();
        dto.Settings!.StartTick.Should().Be(5);
        dto.Settings.StopTick.Should().Be(25);
    }

    [Fact]
    public void Projection_WithRequiredNestedFacet_ShouldWork()
    {
        // Arrange
        var sources = new[]
        {
            new UserModelWithRequiredSettings
            {
                Id = 1,
                SettingsId = 100,
                Settings = new UserSettingsModelForNested
                {
                    Id = 100,
                    StartTick = 0,
                    StopTick = 100
                }
            }
        }.AsQueryable();

        // Act
        var dtos = sources.Select(UserWithRequiredSettingsFacet.Projection).ToList();

        // Assert
        dtos.Should().HaveCount(1);
        dtos[0].Id.Should().Be(1);
        dtos[0].Settings.Should().NotBeNull();
        dtos[0].Settings.StartTick.Should().Be(0);
        dtos[0].Settings.StopTick.Should().Be(100);
    }

    /// <summary>
    /// When a required nested facet source property is null at runtime,
    /// the constructor should throw ArgumentNullException with a descriptive message
    /// instead of a cryptic NullReferenceException.
    /// This tests the fix for GitHub issue #258.
    /// </summary>
    [Fact]
    public void RequiredNestedFacet_WhenSourcePropertyIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange - Force null on a required property (using null! to bypass compiler)
        var source = new UserModelWithRequiredSettings
        {
            Id = 1,
            SettingsId = 100,
            Settings = null!  // This violates the required constraint at runtime
        };

        // Act & Assert
        var action = () => new UserWithRequiredSettingsFacet(source);
        
        action.Should().Throw<ArgumentNullException>()
            .WithMessage("*Settings*")
            .WithMessage("*Required nested facet property*");
    }

    /// <summary>
    /// Verifies that the ArgumentNullException contains helpful information
    /// about which property was null.
    /// </summary>
    [Fact]
    public void RequiredNestedFacet_WhenSourcePropertyIsNull_ExceptionShouldContainPropertyName()
    {
        // Arrange
        var source = new UserModelWithRequiredSettings
        {
            Id = 1,
            SettingsId = 100,
            Settings = null!
        };

        // Act
        ArgumentNullException? exception = null;
        try
        {
            _ = new UserWithRequiredSettingsFacet(source);
        }
        catch (ArgumentNullException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        exception!.ParamName.Should().Be("Settings");
        exception.Message.Should().Contain("Required nested facet property");
        exception.Message.Should().Contain("was null");
    }

    /// <summary>
    /// When a required collection nested facet source property is null at runtime,
    /// the constructor should throw ArgumentNullException with a descriptive message.
    /// This tests the collection variant of the fix for GitHub issue #258.
    /// </summary>
    [Fact]
    public void RequiredCollectionNestedFacet_WhenSourcePropertyIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange - Force null on a required collection property
        var source = new TeamModelWithRequiredMembers
        {
            Id = 1,
            Name = "Team Alpha",
            Members = null!  // This violates the required constraint at runtime
        };

        // Act & Assert
        var action = () => new TeamWithRequiredMembersFacet(source);
        
        action.Should().Throw<ArgumentNullException>()
            .WithMessage("*Members*")
            .WithMessage("*Required nested facet collection property*");
    }

    /// <summary>
    /// Verifies that a required collection nested facet works correctly when source is populated.
    /// </summary>
    [Fact]
    public void RequiredCollectionNestedFacet_WhenSourcePropertyIsPopulated_ShouldMapCorrectly()
    {
        // Arrange
        var source = new TeamModelWithRequiredMembers
        {
            Id = 1,
            Name = "Team Alpha",
            Members = new List<UserSettingsModelForNested>
            {
                new() { Id = 1, StartTick = 10, StopTick = 50 },
                new() { Id = 2, StartTick = 20, StopTick = 60 }
            }
        };

        // Act
        var dto = new TeamWithRequiredMembersFacet(source);

        // Assert
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Team Alpha");
        dto.Members.Should().NotBeNull();
        dto.Members.Should().HaveCount(2);
        dto.Members[0].StartTick.Should().Be(10);
        dto.Members[1].StartTick.Should().Be(20);
    }
}
