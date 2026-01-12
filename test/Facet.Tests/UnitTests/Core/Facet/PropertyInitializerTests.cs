using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class PropertyInitializerTests
{
    [Fact]
    public void Facet_ShouldPreserveInitializer_ForNonNullableReferenceTypeProperty()
    {
        // Arrange & Act
        var dto = new UserModelDto();

        // Assert
        dto.Settings.Should().NotBeNull("Settings should be initialized with default value from source");
    }

    [Fact]
    public void Facet_ShouldMapFromSource_WithInitializedProperties()
    {
        // Arrange
        var source = new UserModel
        {
            Id = 1,
            Name = "Test User",
            Settings = new UserSettings
            {
                NotificationsEnabled = false,
                Theme = "dark",
                Language = "de"
            }
        };

        // Act
        var dto = new UserModelDto(source);

        // Assert
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Test User");
        dto.Settings.Should().NotBeNull();
        dto.Settings.NotificationsEnabled.Should().BeFalse();
        dto.Settings.Theme.Should().Be("dark");
        dto.Settings.Language.Should().Be("de");
    }

    [Fact]
    public void Facet_ShouldPreserveStringEmptyInitializer()
    {
        // Arrange & Act
        var dto = new UserModelDto();

        // Assert - Name should be initialized to string.Empty from the source type
        dto.Name.Should().BeEmpty("Name should be initialized to string.Empty");
    }

    [Fact]
    public void Facet_ShouldPreserveListInitializer_ForInitOnlyProperties()
    {
        // Arrange & Act
        var dto = new InitOnlyWithInitializersDto();

        // Assert - Tags should be initialized to new List<string>()
        dto.Tags.Should().NotBeNull("Tags should be initialized from source");
        dto.Tags.Should().BeEmpty();
    }

    [Fact]
    public void Facet_ShouldPreserveGuidInitializer_ForIdProperty()
    {
        // Arrange & Act
        var dto = new InitOnlyWithInitializersDto();

        // Assert
        dto.Id.Should().NotBeNullOrEmpty("Id should be initialized with a GUID");
        Guid.TryParse(dto.Id, out _).Should().BeTrue("Id should be a valid GUID format");
    }

    [Fact]
    public void Facet_ShouldPreserveDateTimeUtcNowInitializer()
    {
        // Arrange & Act
        var beforeCreation = DateTime.UtcNow;
        var dto = new InitOnlyWithInitializersDto();
        var afterCreation = DateTime.UtcNow;

        // Assert
        dto.CreatedAt.Should().BeOnOrAfter(beforeCreation.AddSeconds(-1));
        dto.CreatedAt.Should().BeOnOrBefore(afterCreation.AddSeconds(1));
    }

    [Fact]
    public void Facet_ShouldCopyFromSource_OverridingDefaultInitializer()
    {
        // Arrange
        var customId = "custom-id-123";
        var customDate = new DateTime(2020, 1, 1);
        var source = new InitOnlyWithInitializers
        {
            Id = customId,
            Name = "Custom Name",
            Tags = new List<string> { "tag1", "tag2" },
            CreatedAt = customDate
        };

        // Act
        var dto = new InitOnlyWithInitializersDto(source);

        // Assert
        dto.Id.Should().Be(customId);
        dto.Name.Should().Be("Custom Name");
        dto.Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2" });
        dto.CreatedAt.Should().Be(customDate);
    }
}
