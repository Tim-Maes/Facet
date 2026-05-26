using System.Reflection;
using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Core.Facet;

public class ModernRecordTests
{
    [Fact]
    public void ToFacet_ShouldMapModernRecord_WithRequiredProperties()
    {
        var modernUser = TestDataFactory.CreateModernUser("Modern", "User");

        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

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
        var modernUser = TestDataFactory.CreateModernUser();

        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        var dtoType = dto.GetType();
        dtoType.GetProperty("PasswordHash").Should().BeNull("PasswordHash should be excluded");
        dtoType.GetProperty("Bio").Should().BeNull("Bio should be excluded");
    }

    [Fact]
    public void ToFacet_ShouldHandleNullableProperties_InModernRecord()
    {
        var modernUser = new ModernUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = "Test",
            LastName = "User",
            Email = null, 
            CreatedAt = DateTime.UtcNow,
            Bio = "Should be excluded",
            PasswordHash = "Should be excluded"
        };

        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        dto.Email.Should().BeNull();
        dto.FirstName.Should().Be("Test");
        dto.LastName.Should().Be("User");
    }

    [Fact]
    public void ToFacet_ModernRecordDto_ShouldSupportRecordFeatures()
    {
        var modernUser = TestDataFactory.CreateModernUser("Record", "Features");

        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();
        var dto2 = modernUser.ToFacet<ModernUser, ModernUserDto>();

        dto.Equals(dto2).Should().BeTrue("Records should have value equality");
        dto.GetHashCode().Should().Be(dto2.GetHashCode(), "Equal records should have same hash code");
    }

    [Fact]
    public void ToFacet_ModernRecordDto_ShouldSupportWithExpressions()
    {
        var modernUser = TestDataFactory.CreateModernUser("With", "Expression");
        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        var modifiedDto = dto with { FirstName = "Modified" };

        modifiedDto.FirstName.Should().Be("Modified");
        modifiedDto.LastName.Should().Be("Expression"); 
        dto.FirstName.Should().Be("With"); 
    }

    [Fact]
    public void ToFacet_ModernRecordDto_ShouldHandleInitOnlyProperties()
    {
        var modernUser = new ModernUser
        {
            Id = "init-only-test",
            FirstName = "Init",
            LastName = "Only",
            CreatedAt = DateTime.UtcNow 
        };

        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        dto.Id.Should().Be("init-only-test");
        dto.CreatedAt.Should().Be(modernUser.CreatedAt);
    }

    [Fact]
    public void ToFacet_ShouldMapCustomPropertiesInRecord()
    {
        var modernUser = TestDataFactory.CreateModernUser("Custom", "Props");

        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        dto.FullName.Should().Be(string.Empty); 
        dto.DisplayName.Should().Be(string.Empty); 
        
        dto.FirstName.Should().Be("Custom");
        dto.LastName.Should().Be("Props");
    }

    [Fact]
    public void ToFacet_ShouldHandleGuidIds_InModernRecords()
    {
        var guidId = Guid.NewGuid().ToString();
        var modernUser = new ModernUser
        {
            Id = guidId,
            FirstName = "Guid",
            LastName = "Test",
            CreatedAt = DateTime.UtcNow
        };

        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        dto.Id.Should().Be(guidId);
        Guid.TryParse(dto.Id, out _).Should().BeTrue("ID should be a valid GUID string");
    }

    [Fact]
    public void ToFacet_ModernRecord_ShouldPreservePropertyCasing()
    {
        var modernUser = TestDataFactory.CreateModernUser("Case", "Sensitive");

        var dto = modernUser.ToFacet<ModernUser, ModernUserDto>();

        dto.FirstName.Should().Be("Case"); 
        dto.LastName.Should().Be("Sensitive"); 
        
        var dtoType = dto.GetType();
        dtoType.GetProperty("FirstName").Should().NotBeNull();
        dtoType.GetProperty("firstname").Should().BeNull(); 
    }

    [Fact]
    public void RecordFacet_ShouldPreserveRequiredModifier_WithoutWorkaround()
    {
        var dtoType = typeof(ModernUserRequiredDto);

        var idProp = dtoType.GetProperty("Id")!;
        idProp.Should().NotBeNull();
        idProp.GetCustomAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>()
            .Should().NotBeNull("Id should be marked as required");

        var firstNameProp = dtoType.GetProperty("FirstName")!;
        firstNameProp.Should().NotBeNull();
        firstNameProp.GetCustomAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>()
            .Should().NotBeNull("FirstName should be marked as required");

        var lastNameProp = dtoType.GetProperty("LastName")!;
        lastNameProp.Should().NotBeNull();
        lastNameProp.GetCustomAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>()
            .Should().NotBeNull("LastName should be marked as required");
    }

    [Fact]
    public void RecordFacet_WithRequiredProperties_ShouldMapCorrectly()
    {
        var modernUser = new ModernUser
        {
            Id = "test-id",
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            CreatedAt = DateTime.UtcNow
        };

        var dto = modernUser.ToFacet<ModernUser, ModernUserRequiredDto>();

        dto.Id.Should().Be("test-id");
        dto.FirstName.Should().Be("Jane");
        dto.LastName.Should().Be("Doe");
        dto.Email.Should().Be("jane@example.com");
    }
}
