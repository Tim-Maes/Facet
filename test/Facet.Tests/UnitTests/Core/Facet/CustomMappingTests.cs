using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Core.Facet;

public class CustomMappingTests
{
    [Fact]
    public void ToFacet_ShouldApplyCustomMapping_WhenConfigurationIsProvided()
    {
        var user = TestDataFactory.CreateUser("John", "Doe", dateOfBirth: new DateTime(1990, 5, 15));

        var dto = user.ToFacet<User, UserDtoWithMapping>();

        dto.Should().NotBeNull();
        dto.FullName.Should().Be("John Doe", "Custom mapping should combine first and last name");
        dto.Age.Should().BeGreaterThan(30, "Custom mapping should calculate age from birth date");
    }

    [Fact]
    public void ToFacet_ShouldCalculateAge_BasedOnCurrentDate()
    {
        var birthDate = DateTime.Today.AddYears(-25);
        var user = TestDataFactory.CreateUser("Jane", "Smith", dateOfBirth: birthDate);

        var dto = user.ToFacet<User, UserDtoWithMapping>();

        dto.Age.Should().Be(25, "Age should be calculated from birth date");
    }

    [Fact]
    public void ToFacet_ShouldHandleBirthdayNotYetPassed_InCurrentYear()
    {
        var today = DateTime.Today;
        var birthDate = today.AddMonths(6).AddYears(-30);
        var user = TestDataFactory.CreateUser("Future", "Birthday", dateOfBirth: birthDate);

        var dto = user.ToFacet<User, UserDtoWithMapping>();

        dto.Age.Should().Be(29, "Age should be 29 if 30th birthday hasn't occurred this year yet");
    }

    [Fact]
    public void ToFacet_ShouldHandleBirthdayAlreadyPassed_InCurrentYear()
    {
        var today = DateTime.Today;
        var birthDate = today.AddMonths(-6).AddYears(-30);
        var user = TestDataFactory.CreateUser("Past", "Birthday", dateOfBirth: birthDate);

        var dto = user.ToFacet<User, UserDtoWithMapping>();

        dto.Age.Should().Be(30, "Age should be 30 if birthday has already occurred this year");
    }

    [Fact]
    public void ToFacet_ShouldCombineNamesCorrectly_WithDifferentInputs()
    {
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
            var user = TestDataFactory.CreateUser(firstName, lastName);
            var dto = user.ToFacet<User, UserDtoWithMapping>();

            dto.FullName.Should().Be(expectedFullName,
                $"FullName should be '{expectedFullName}' for '{firstName}' + '{lastName}'");
        }
    }

    [Fact]
    public void ToFacet_ShouldStillExcludeSpecifiedProperties_WhenUsingCustomMapping()
    {
        var user = TestDataFactory.CreateUser();

        var dto = user.ToFacet<User, UserDtoWithMapping>();

        var dtoType = dto.GetType();
        dtoType.GetProperty("Password").Should().BeNull("Password should still be excluded");
        dtoType.GetProperty("CreatedAt").Should().BeNull("CreatedAt should still be excluded");
    }

    [Fact]
    public void ToFacet_ShouldIncludeStandardProperties_EvenWithCustomMapping()
    {
        var user = TestDataFactory.CreateUser("Custom", "Mapping", "custom@example.com");

        var dto = user.ToFacet<User, UserDtoWithMapping>();

        dto.Id.Should().Be(user.Id);
        dto.FirstName.Should().Be("Custom");
        dto.LastName.Should().Be("Mapping");
        dto.Email.Should().Be("custom@example.com");
        dto.IsActive.Should().Be(user.IsActive);

        dto.FullName.Should().Be("Custom Mapping");
        dto.Age.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ToFacet_CustomMapping_ShouldWorkWithBoundaryAges()
    {
        var today = DateTime.Today;
        var veryYoung = TestDataFactory.CreateUser("Young", "Person", dateOfBirth: today.AddYears(-1));
        var veryOld = TestDataFactory.CreateUser("Old", "Person", dateOfBirth: today.AddYears(-100));

        var youngDto = veryYoung.ToFacet<User, UserDtoWithMapping>();
        var oldDto = veryOld.ToFacet<User, UserDtoWithMapping>();

        youngDto.Age.Should().Be(1);
        oldDto.Age.Should().Be(100);
    }
}
