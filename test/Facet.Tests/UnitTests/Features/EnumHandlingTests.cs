using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Features;

public class EnumHandlingTests
{
    [Fact]
    public void ToFacet_ShouldMapEnumProperties_Correctly()
    {
        var user = TestDataFactory.CreateUserWithEnum("Active User", UserStatus.Active);

        var dto = user.ToFacet<UserWithEnum, UserWithEnumDto>();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(user.Id);
        dto.Name.Should().Be("Active User");
        dto.Status.Should().Be(UserStatus.Active);
        dto.Email.Should().Be(user.Email);
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.Inactive)]
    [InlineData(UserStatus.Pending)]
    [InlineData(UserStatus.Suspended)]
    public void ToFacet_ShouldHandleAllEnumValues_Correctly(UserStatus status)
    {
        var user = TestDataFactory.CreateUserWithEnum($"User {status}", status);

        var dto = user.ToFacet<UserWithEnum, UserWithEnumDto>();

        dto.Status.Should().Be(status);
        dto.Name.Should().Be($"User {status}");
    }

    [Fact]
    public void ToFacet_ShouldPreserveEnumTypeInformation()
    {
        var user = TestDataFactory.CreateUserWithEnum(status: UserStatus.Pending);

        var dto = user.ToFacet<UserWithEnum, UserWithEnumDto>();

        dto.Status.GetType().Should().Be<UserStatus>();
        dto.Status.GetType().Should().Be(typeof(UserStatus));
    }

    [Fact]
    public void ToFacet_ShouldAllowEnumComparison_AfterMapping()
    {
        var activeUser = TestDataFactory.CreateUserWithEnum("Active", UserStatus.Active);
        var inactiveUser = TestDataFactory.CreateUserWithEnum("Inactive", UserStatus.Inactive);

        var activeDto = activeUser.ToFacet<UserWithEnum, UserWithEnumDto>();
        var inactiveDto = inactiveUser.ToFacet<UserWithEnum, UserWithEnumDto>();

        activeDto.Status.Should().NotBe(inactiveDto.Status);
        (activeDto.Status == UserStatus.Active).Should().BeTrue();
        (inactiveDto.Status == UserStatus.Inactive).Should().BeTrue();
    }

    [Fact]
    public void ToFacet_ShouldHandleEnumToStringConversion_IfNeeded()
    {
        var user = TestDataFactory.CreateUserWithEnum("String Test", UserStatus.Suspended);

        var dto = user.ToFacet<UserWithEnum, UserWithEnumDto>();

        dto.Status.ToString().Should().Be("Suspended");
        Enum.GetName(typeof(UserStatus), dto.Status).Should().Be("Suspended");
    }

    [Fact]
    public void ToFacet_ShouldMaintainEnumOrdinalValues()
    {
        var user = TestDataFactory.CreateUserWithEnum(status: UserStatus.Pending);

        var dto = user.ToFacet<UserWithEnum, UserWithEnumDto>();

        ((int)dto.Status).Should().Be((int)UserStatus.Pending);
        ((int)dto.Status).Should().Be(2);
    }

    [Fact]
    public void ToFacet_ShouldHandleMultipleUsersWithDifferentEnumValues()
    {
        var users = new List<UserWithEnum>
        {
            TestDataFactory.CreateUserWithEnum("User 1", UserStatus.Active),
            TestDataFactory.CreateUserWithEnum("User 2", UserStatus.Inactive),
            TestDataFactory.CreateUserWithEnum("User 3", UserStatus.Pending),
            TestDataFactory.CreateUserWithEnum("User 4", UserStatus.Suspended)
        };

        var dtos = users.Select(u => u.ToFacet<UserWithEnum, UserWithEnumDto>()).ToList();

        dtos.Should().HaveCount(4);
        dtos[0].Status.Should().Be(UserStatus.Active);
        dtos[1].Status.Should().Be(UserStatus.Inactive);
        dtos[2].Status.Should().Be(UserStatus.Pending);
        dtos[3].Status.Should().Be(UserStatus.Suspended);
    }

    [Fact]
    public void UserStatusEnum_ShouldHaveExpectedValues()
    {
        ((int)UserStatus.Active).Should().Be(0);
        ((int)UserStatus.Inactive).Should().Be(1);
        ((int)UserStatus.Pending).Should().Be(2);
        ((int)UserStatus.Suspended).Should().Be(3);
    }

    [Fact]
    public void ToFacet_ShouldAllowEnumBasedFiltering_AfterMapping()
    {
        var users = new List<UserWithEnum>
        {
            TestDataFactory.CreateUserWithEnum("Active 1", UserStatus.Active),
            TestDataFactory.CreateUserWithEnum("Inactive 1", UserStatus.Inactive),
            TestDataFactory.CreateUserWithEnum("Active 2", UserStatus.Active),
            TestDataFactory.CreateUserWithEnum("Pending 1", UserStatus.Pending)
        };

        var dtos = users.Select(u => u.ToFacet<UserWithEnum, UserWithEnumDto>()).ToList();
        var activeUsers = dtos.Where(dto => dto.Status == UserStatus.Active).ToList();

        activeUsers.Should().HaveCount(2);
        activeUsers.All(u => u.Status == UserStatus.Active).Should().BeTrue();
        activeUsers.Select(u => u.Name).Should().BeEquivalentTo(new[] { "Active 1", "Active 2" });
    }
}
