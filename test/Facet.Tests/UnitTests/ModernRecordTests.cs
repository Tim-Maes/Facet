using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests;

public class ModernRecordTests
{
    [Fact]
    public void ToFacet_ShouldMapModernRecord_WithRequiredProperties()
    {
        // Arrange
        var modernUser = TestDataFactory.CreateModernUser("Modern", "User");

        // Act
        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(modernUser.Id);
        dto.FirstName.Should().Be("Modern");
        dto.LastName.Should().Be("User");
        dto.Email.Should().Be(modernUser.Email);
        dto.CreatedAt.Should().Be(modernUser.CreatedAt);
    }

    [Fact]
    public void ToFacet_ShouldExcludeSpecifiedProperties_FromModernRecord()
    {
        // Arrange
        var modernUser = TestDataFactory.CreateModernUser();

        // Act
        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        // Assert
        var dtoType = dto.GetType();
        dtoType.GetProperty("PasswordHash").Should().BeNull("PasswordHash should be excluded");
        dtoType.GetProperty("Bio").Should().BeNull("Bio should be excluded");
    }

    [Fact]
    public void ToFacet_ShouldHandleNullableProperties_InModernRecord()
    {
        // Arrange
        var modernUser = new ModernUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = "Test",
            LastName = "User",
            Email = null, // Nullable property
            CreatedAt = DateTime.UtcNow,
            Bio = "Should be excluded",
            PasswordHash = "Should be excluded"
        };

        // Act
        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        // Assert
        dto.Email.Should().BeNull();
        dto.FirstName.Should().Be("Test");
        dto.LastName.Should().Be("User");
    }

    [Fact]
    public void ToFacet_ModernRecordDto_ShouldSupportRecordFeatures()
    {
        // Arrange
        var modernUser = TestDataFactory.CreateModernUser("Record", "Features");

        // Act
        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();
        var dto2 = modernUser.ToFacet<ModernUser, ModernUserDto>();

        // Assert
        dto.Equals(dto2).Should().BeTrue("Records should have value equality");
        dto.GetHashCode().Should().Be(dto2.GetHashCode(), "Equal records should have same hash code");
    }

    [Fact]
    public void ToFacet_ModernRecordDto_ShouldSupportWithExpressions()
    {
        // Arrange
        var modernUser = TestDataFactory.CreateModernUser("With", "Expression");
        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        // Act
        var modifiedDto = dto with { FirstName = "Modified" };

        // Assert
        modifiedDto.FirstName.Should().Be("Modified");
        modifiedDto.LastName.Should().Be("Expression"); // Other properties unchanged
        dto.FirstName.Should().Be("With"); // Original unchanged
    }

    [Fact]
    public void ToFacet_ModernRecordDto_ShouldHandleInitOnlyProperties()
    {
        // Arrange
        var modernUser = new ModernUser
        {
            Id = "init-only-test",
            FirstName = "Init",
            LastName = "Only",
            CreatedAt = DateTime.UtcNow // This is init-only in the source
        };

        // Act
        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        // Assert
        dto.Id.Should().Be("init-only-test");
        dto.CreatedAt.Should().Be(modernUser.CreatedAt);
    }

    [Fact]
    public void ToFacet_ShouldMapCustomPropertiesInRecord()
    {
        // Arrange
        var modernUser = TestDataFactory.CreateModernUser("Custom", "Props");

        // Act
        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        // Assert
        // The DTO can have custom properties that don't exist in the source
        dto.FullName.Should().Be(string.Empty); // Default value for custom property
        dto.DisplayName.Should().Be(string.Empty); // Default value for custom property
        
        // But source properties should still be mapped
        dto.FirstName.Should().Be("Custom");
        dto.LastName.Should().Be("Props");
    }

    [Fact]
    public void ToFacet_ShouldHandleGuidIds_InModernRecords()
    {
        // Arrange
        var guidId = Guid.NewGuid().ToString();
        var modernUser = new ModernUser
        {
            Id = guidId,
            FirstName = "Guid",
            LastName = "Test",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        // Assert
        dto.Id.Should().Be(guidId);
        Guid.TryParse(dto.Id, out _).Should().BeTrue("ID should be a valid GUID string");
    }

    [Fact]
    public void ToFacet_ModernRecord_ShouldPreservePropertyCasing()
    {
        // Arrange
        var modernUser = TestDataFactory.CreateModernUser("Case", "Sensitive");

        // Act
        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        // Assert
        dto.FirstName.Should().Be("Case"); // Exact case preserved
        dto.LastName.Should().Be("Sensitive"); // Exact case preserved
        
        // Property names should match exactly (case-sensitive)
        var dtoType = dto.GetType();
        dtoType.GetProperty("FirstName").Should().NotBeNull();
        dtoType.GetProperty("firstname").Should().BeNull(); // lowercase should not exist
    }
}