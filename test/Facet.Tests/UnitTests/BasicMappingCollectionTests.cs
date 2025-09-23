using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests;

public class BasicMappingCollectionTests
{
    [Fact]
    public void SelectFacets_ShouldMapBasicProperties_WhenMappingUserToDto()
    {
        // Arrange
        var users = TestDataFactory.CreateUsers();

        // Act
        var dtos = users.SelectFacets<User, UserDto>().ToList();

        // Assert
        dtos.Should().NotBeNull();
        dtos.Should().HaveCount(users.Count);
        dtos[0].FirstName.Should().Be(users[0].FirstName);
        dtos[1].FirstName.Should().Be(users[1].FirstName);
        dtos[2].FirstName.Should().Be(users[2].FirstName);
        dtos[2].IsActive.Should().Be(users[2].IsActive);
    }
    
    [Fact]
    public void SelectFacetsShorthand_ShouldMapBasicProperties_WhenMappingUserToDto()
    {
        // Arrange
        var users = TestDataFactory.CreateUsers();

        // Act
        var dtos = users.SelectFacets<UserDto>().ToList();

        // Assert
        dtos.Should().NotBeNull();
        dtos.Should().HaveCount(users.Count);
        dtos[0].FirstName.Should().Be(users[0].FirstName);
        dtos[1].FirstName.Should().Be(users[1].FirstName);
        dtos[2].FirstName.Should().Be(users[2].FirstName);
        dtos[2].IsActive.Should().Be(users[2].IsActive);
    }
}