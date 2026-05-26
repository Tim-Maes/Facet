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
        var dtoType = typeof(UserWithRequiredSettingsFacet);
        var settingsProperty = dtoType.GetProperty("Settings");

        settingsProperty.Should().NotBeNull();
        
        settingsProperty!.PropertyType.Should().Be(typeof(UserSettingsFacet));
    }

    [Fact]
    public void RequiredNestedFacet_ShouldBeMarkedAsRequired()
    {
        var dtoType = typeof(UserWithRequiredSettingsFacet);
        var settingsProperty = dtoType.GetProperty("Settings");

        settingsProperty.Should().NotBeNull();
        
        var requiredAttribute = settingsProperty!.GetCustomAttributes(
            typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), true);
        // Note: Required modifier appears on the type, not the property in reflection
        
        var typeRequiredAttribute = dtoType.GetCustomAttributes(
            typeof(System.Runtime.CompilerServices.RequiredMemberAttribute), true);
        typeRequiredAttribute.Should().NotBeEmpty("Type should have RequiredMemberAttribute");
    }

    [Fact]
    public void RequiredNestedFacet_Constructor_ShouldMapCorrectly()
    {
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

        var dto = new UserWithRequiredSettingsFacet(source);

        dto.Id.Should().Be(1);
        dto.SettingsId.Should().Be(100);
        dto.Settings.Should().NotBeNull();
        dto.Settings.StartTick.Should().Be(10);
        dto.Settings.StopTick.Should().Be(50);
    }

    [Fact]
    public void RequiredNestedFacet_ComputedProperty_ShouldWork()
    {
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

        var dto = new UserWithRequiredSettingsFacet(source);

        dto.ProcessedTicks.Should().Be(40); 
    }

    [Fact]
    public void OptionalNestedFacet_Property_ShouldBeNullable()
    {
        var dtoType = typeof(UserWithOptionalSettingsFacet);
        var settingsProperty = dtoType.GetProperty("Settings");

        settingsProperty.Should().NotBeNull();
        
        settingsProperty!.PropertyType.Should().Be(typeof(UserSettingsFacet));
    }

    [Fact]
    public void OptionalNestedFacet_Constructor_ShouldHandleValue()
    {
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

        var dto = new UserWithOptionalSettingsFacet(source);

        dto.Id.Should().Be(2);
        dto.Settings.Should().NotBeNull();
        dto.Settings!.StartTick.Should().Be(5);
        dto.Settings.StopTick.Should().Be(25);
    }

    [Fact]
    public void Projection_WithRequiredNestedFacet_ShouldWork()
    {
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

        var dtos = sources.Select(UserWithRequiredSettingsFacet.Projection).ToList();

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
        var source = new UserModelWithRequiredSettings
        {
            Id = 1,
            SettingsId = 100,
            Settings = null!  // This violates the required constraint at runtime
        };

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
        var source = new UserModelWithRequiredSettings
        {
            Id = 1,
            SettingsId = 100,
            Settings = null!
        };

        ArgumentNullException? exception = null;
        try
        {
            _ = new UserWithRequiredSettingsFacet(source);
        }
        catch (ArgumentNullException ex)
        {
            exception = ex;
        }

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
        var source = new TeamModelWithRequiredMembers
        {
            Id = 1,
            Name = "Team Alpha",
            Members = null!  // This violates the required constraint at runtime
        };

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

        var dto = new TeamWithRequiredMembersFacet(source);

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Team Alpha");
        dto.Members.Should().NotBeNull();
        dto.Members.Should().HaveCount(2);
        dto.Members[0].StartTick.Should().Be(10);
        dto.Members[1].StartTick.Should().Be(20);
    }
}
