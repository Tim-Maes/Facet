using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Core.Facet;

public class CustomMappingTests
{
    [Fact]
    public void ToFacet_ShouldApplyCustomMapping_WhenConfigurationIsProvided()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("John", "Doe", dateOfBirth: new DateTime(1990, 5, 15));

        // Act
        var dto = user.ToFacet<User, UserDtoWithMapping>();

        // Assert
        dto.Should().NotBeNull();
        dto.FullName.Should().Be("John Doe", "Custom mapping should combine first and last name");
        dto.Age.Should().BeGreaterThan(30, "Custom mapping should calculate age from birth date");
    }

    [Fact]
    public void ToFacet_ShouldCalculateAge_BasedOnCurrentDate()
    {
        // Arrange
        var birthDate = DateTime.Today.AddYears(-25);
        var user = TestDataFactory.CreateUser("Jane", "Smith", dateOfBirth: birthDate);

        // Act
        var dto = user.ToFacet<User, UserDtoWithMapping>();

        // Assert
        dto.Age.Should().Be(25, "Age should be calculated from birth date");
    }

    [Fact]
    public void ToFacet_ShouldHandleBirthdayNotYetPassed_InCurrentYear()
    {
        // Arrange
        var nextMonth = DateTime.Today.AddMonths(1);
        var birthDateNextMonth = new DateTime(DateTime.Today.Year - 30, nextMonth.Month, nextMonth.Day);
        var user = TestDataFactory.CreateUser("Future", "Birthday", dateOfBirth: birthDateNextMonth);

        // Act
        var dto = user.ToFacet<User, UserDtoWithMapping>();

       // Assert
        dto.Age.Should().Be(29, "Age should be 29 if birthday hasn't occurred this year yet");
    }

    [Fact]
    public void ToFacet_ShouldHandleBirthdayAlreadyPassed_InCurrentYear()
    {
        // Arrange
        var lastMonth = DateTime.Today.AddMonths(-1);
        var birthDateLastMonth = new DateTime(DateTime.Today.Year - 30, lastMonth.Month, lastMonth.Day);
        var user = TestDataFactory.CreateUser("Past", "Birthday", dateOfBirth: birthDateLastMonth);

        // Act
        var dto = user.ToFacet<User, UserDtoWithMapping>();

        // Assert
        dto.Age.Should().Be(30, "Age should be 30 if birthday has already occurred this year");
    }

    [Fact]
    public void ToFacet_ShouldCombineNamesCorrectly_WithDifferentInputs()
    {
        // Arrange
        var testCases = new[]
        {
            ("John", "Doe", "John Doe"),
            ("Mary", "Smith-Johnson", "Mary Smith-Johnson"),
            ("", "SingleName", " SingleName"),
            ("OnlyFirst", "", "OnlyFirst "),
            ("", "", " ")
        };

        foreach (var (firstName, lastName, expectedFullName) in testCases)
        {
            // Act
            var user = TestDataFactory.CreateUser(firstName, lastName);
            var dto = user.ToFacet<User, UserDtoWithMapping>();

            // Assert
            dto.FullName.Should().Be(expectedFullName,
                $"FullName should be '{expectedFullName}' for '{firstName}' + '{lastName}'");
        }
    }

    [Fact]
    public void ToFacet_ShouldStillExcludeSpecifiedProperties_WhenUsingCustomMapping()
    {
        // Arrange
        var user = TestDataFactory.CreateUser();

        // Act
        var dto = user.ToFacet<User, UserDtoWithMapping>();

        // Assert
        var dtoType = dto.GetType();
        dtoType.GetProperty("Password").Should().BeNull("Password should still be excluded");
        dtoType.GetProperty("CreatedAt").Should().BeNull("CreatedAt should still be excluded");
    }

    [Fact]
    public void ToFacet_ShouldIncludeStandardProperties_EvenWithCustomMapping()
    {
        // Arrange
        var user = TestDataFactory.CreateUser("Custom", "Mapping", "custom@example.com");

        // Act
        var dto = user.ToFacet<User, UserDtoWithMapping>();

        // Assert
        dto.Id.Should().Be(user.Id);
        dto.FirstName.Should().Be("Custom");
        dto.LastName.Should().Be("Mapping");
        dto.Email.Should().Be("custom@example.com");
        dto.IsActive.Should().Be(user.IsActive);

        // Plus the custom mapped properties
        dto.FullName.Should().Be("Custom Mapping");
        dto.Age.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ToFacet_CustomMapping_ShouldWorkWithBoundaryAges()
    {
        // Arrange - Test with very young and very old ages
        var today = DateTime.Today;
        var veryYoung = TestDataFactory.CreateUser("Young", "Person", dateOfBirth: today.AddYears(-1));
        var veryOld = TestDataFactory.CreateUser("Old", "Person", dateOfBirth: today.AddYears(-100));

        // Act
        var youngDto = veryYoung.ToFacet<User, UserDtoWithMapping>();
        var oldDto = veryOld.ToFacet<User, UserDtoWithMapping>();

        // Assert
        youngDto.Age.Should().Be(1);
        oldDto.Age.Should().Be(100);
    }
}