using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for issue #188: Nested [Facet] classes with duplicate names support
/// </summary>
public class NestedClassDuplicateNamesTests
{
    [Fact]
    public void NestedClasses_WithSameName_InDifferentParents_ShouldGenerateSuccessfully()
    {
        var user = new User
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Password = "secret",
            IsActive = true,
            DateOfBirth = new DateTime(1990, 1, 1),
            LastLoginAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var listItem = new UserListResponse.UserItem(user);
        var detailItem = new UserDetailResponse.UserItem(user);

        listItem.Should().NotBeNull();
        listItem.Id.Should().Be(1);
        listItem.FirstName.Should().Be("John");

        var listItemType = listItem.GetType();
        listItemType.GetProperty("LastName").Should().BeNull("LastName should not be included in UserListResponse.UserItem");
        listItemType.GetProperty("Email").Should().BeNull("Email should not be included in UserListResponse.UserItem");

        detailItem.Should().NotBeNull();
        detailItem.Id.Should().Be(1);
        detailItem.FirstName.Should().Be("John");
        detailItem.LastName.Should().Be("Doe");
        detailItem.Email.Should().Be("john@example.com");

        var detailItemType = detailItem.GetType();
        detailItemType.GetProperty("Password").Should().BeNull("Password should not be included in UserDetailResponse.UserItem");
    }

    [Fact]
    public void NestedClasses_WithSameName_ShouldHaveDifferentFullNames()
    {
        var listItemType = typeof(UserListResponse.UserItem);
        var detailItemType = typeof(UserDetailResponse.UserItem);

        listItemType.Should().NotBe(detailItemType, "Types should be different even though they have the same simple name");
        listItemType.Name.Should().Be("UserItem");
        detailItemType.Name.Should().Be("UserItem");
        listItemType.FullName.Should().Contain("UserListResponse");
        detailItemType.FullName.Should().Contain("UserDetailResponse");
    }

    [Fact]
    public void NestedClasses_Projection_ShouldWorkCorrectly()
    {
        var users = new List<User>
        {
            new User { Id = 1, FirstName = "Alice", LastName = "Smith", Email = "alice@test.com" },
            new User { Id = 2, FirstName = "Bob", LastName = "Jones", Email = "bob@test.com" }
        };

        var listItems = users.Select(UserListResponse.UserItem.Projection.Compile()).ToList();
        var detailItems = users.Select(UserDetailResponse.UserItem.Projection.Compile()).ToList();

        listItems.Should().HaveCount(2);
        listItems[0].FirstName.Should().Be("Alice");
        listItems[1].FirstName.Should().Be("Bob");

        detailItems.Should().HaveCount(2);
        detailItems[0].FirstName.Should().Be("Alice");
        detailItems[0].Email.Should().Be("alice@test.com");
        detailItems[1].FirstName.Should().Be("Bob");
        detailItems[1].Email.Should().Be("bob@test.com");
    }

    [Fact]
    public void DeeplyNestedClasses_WithSameName_ShouldGenerateSuccessfully()
    {
        var user = new User
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        };

        var outerInner = new OuterClass1.InnerClass.Item(user);
        var anotherOuterInner = new OuterClass2.InnerClass.Item(user);

        outerInner.Should().NotBeNull();
        outerInner.Id.Should().Be(1);

        anotherOuterInner.Should().NotBeNull();
        anotherOuterInner.Id.Should().Be(1);

        typeof(OuterClass1.InnerClass.Item).Should().NotBe(typeof(OuterClass2.InnerClass.Item));
    }
}

public partial class UserListResponse
{
    [Facet(typeof(User), Include = ["Id", "FirstName"])]
    public partial class UserItem;
}

public partial class UserDetailResponse
{
    [Facet(typeof(User), Include = ["Id", "FirstName", "LastName", "Email"])]
    public partial class UserItem;
}

public partial class OuterClass1
{
    public partial class InnerClass
    {
        [Facet(typeof(User), Include = ["Id", "FirstName"])]
        public partial class Item;
    }
}

public partial class OuterClass2
{
    public partial class InnerClass
    {
        [Facet(typeof(User), Include = ["Id", "FirstName", "Email"])]
        public partial class Item;
    }
}
