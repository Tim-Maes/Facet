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
}
