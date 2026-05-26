using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class EnumConversionTests
{
    [Fact]
    public void ConvertEnumsTo_String_ShouldGenerateStringProperty()
    {
        var entity = new UserWithEnum
        {
            Id = 1,
            Name = "John",
            Status = UserStatus.Active,
            Email = "john@example.com"
        };

        var dto = new UserWithEnumToStringDto(entity);

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("John");
        dto.Status.Should().Be("Active");
        dto.Email.Should().Be("john@example.com");
    }

    [Theory]
    [InlineData(UserStatus.Active, "Active")]
    [InlineData(UserStatus.Inactive, "Inactive")]
    [InlineData(UserStatus.Pending, "Pending")]
    [InlineData(UserStatus.Suspended, "Suspended")]
    public void ConvertEnumsTo_String_ShouldMapAllEnumValues(UserStatus status, string expected)
    {
        var entity = new UserWithEnum { Status = status };

        var dto = new UserWithEnumToStringDto(entity);

        dto.Status.Should().Be(expected);
    }

    [Fact]
    public void ConvertEnumsTo_String_PropertyType_ShouldBeString()
    {
        var statusProperty = typeof(UserWithEnumToStringDto).GetProperty("Status");
        statusProperty.Should().NotBeNull();
        statusProperty!.PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void ConvertEnumsTo_String_NonEnumProperties_ShouldBeUnchanged()
    {
        var idProperty = typeof(UserWithEnumToStringDto).GetProperty("Id");
        idProperty!.PropertyType.Should().Be(typeof(int));

        var nameProperty = typeof(UserWithEnumToStringDto).GetProperty("Name");
        nameProperty!.PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void ConvertEnumsTo_String_FromSource_ShouldWork()
    {
        var entity = new UserWithEnum { Status = UserStatus.Pending };

        var dto = UserWithEnumToStringDto.FromSource(entity);

        dto.Status.Should().Be("Pending");
    }

    [Fact]
    public void ConvertEnumsTo_String_ToSource_ShouldConvertBack()
    {
        var dto = new UserWithEnumToStringDto
        {
            Id = 1,
            Name = "John",
            Status = "Active",
            Email = "john@example.com"
        };

        var entity = dto.ToSource();

        entity.Status.Should().Be(UserStatus.Active);
        entity.Id.Should().Be(1);
        entity.Name.Should().Be("John");
    }

    [Fact]
    public void ConvertEnumsTo_Int_ShouldGenerateIntProperty()
    {
        var entity = new UserWithEnum
        {
            Id = 1,
            Name = "John",
            Status = UserStatus.Active,
            Email = "john@example.com"
        };

        var dto = new UserWithEnumToIntDto(entity);

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("John");
        dto.Status.Should().Be(0); 
        dto.Email.Should().Be("john@example.com");
    }

    [Theory]
    [InlineData(UserStatus.Active, 0)]
    [InlineData(UserStatus.Inactive, 1)]
    [InlineData(UserStatus.Pending, 2)]
    [InlineData(UserStatus.Suspended, 3)]
    public void ConvertEnumsTo_Int_ShouldMapAllEnumValues(UserStatus status, int expected)
    {
        var entity = new UserWithEnum { Status = status };

        var dto = new UserWithEnumToIntDto(entity);

        dto.Status.Should().Be(expected);
    }

    [Fact]
    public void ConvertEnumsTo_Int_PropertyType_ShouldBeInt()
    {
        var statusProperty = typeof(UserWithEnumToIntDto).GetProperty("Status");
        statusProperty.Should().NotBeNull();
        statusProperty!.PropertyType.Should().Be(typeof(int));
    }

    [Fact]
    public void ConvertEnumsTo_Int_ToSource_ShouldConvertBack()
    {
        var dto = new UserWithEnumToIntDto
        {
            Id = 1,
            Name = "John",
            Status = 2, 
            Email = "john@example.com"
        };

        var entity = dto.ToSource();

        entity.Status.Should().Be(UserStatus.Pending);
    }

    [Fact]
    public void ConvertEnumsTo_String_NullableEnum_ShouldHandleNonNullValue()
    {
        var entity = new EntityWithNullableEnum
        {
            Id = 1,
            Name = "Test",
            Status = UserStatus.Active,
            NonNullableStatus = UserStatus.Pending
        };

        var dto = new NullableEnumToStringDto(entity);

        dto.Status.Should().Be("Active");
        dto.NonNullableStatus.Should().Be("Pending");
    }

    [Fact]
    public void ConvertEnumsTo_String_NullableEnum_ShouldHandleNullValue()
    {
        var entity = new EntityWithNullableEnum
        {
            Id = 1,
            Name = "Test",
            Status = null,
            NonNullableStatus = UserStatus.Active
        };

        var dto = new NullableEnumToStringDto(entity);

        dto.Status.Should().BeNull();
        dto.NonNullableStatus.Should().Be("Active");
    }

    [Fact]
    public void ConvertEnumsTo_String_NullableEnum_PropertyType_ShouldBeNullableString()
    {
        var statusProperty = typeof(NullableEnumToStringDto).GetProperty("Status");
        statusProperty.Should().NotBeNull();
        statusProperty!.PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void ConvertEnumsTo_Int_NullableEnum_ShouldHandleNonNullValue()
    {
        var entity = new EntityWithNullableEnum
        {
            Id = 1,
            Name = "Test",
            Status = UserStatus.Suspended,
            NonNullableStatus = UserStatus.Active
        };

        var dto = new NullableEnumToIntDto(entity);

        dto.Status.Should().Be(3); 
        dto.NonNullableStatus.Should().Be(0); 
    }

    [Fact]
    public void ConvertEnumsTo_Int_NullableEnum_ShouldHandleNullValue()
    {
        var entity = new EntityWithNullableEnum
        {
            Id = 1,
            Name = "Test",
            Status = null,
            NonNullableStatus = UserStatus.Active
        };

        var dto = new NullableEnumToIntDto(entity);

        dto.Status.Should().BeNull();
        dto.NonNullableStatus.Should().Be(0);
    }

    [Fact]
    public void ConvertEnumsTo_Int_NullableEnum_PropertyType_ShouldBeNullableInt()
    {
        var statusProperty = typeof(NullableEnumToIntDto).GetProperty("Status");
        statusProperty.Should().NotBeNull();
        statusProperty!.PropertyType.Should().Be(typeof(int?));
    }

    [Fact]
    public void ConvertEnumsTo_String_WithNullableProperties_ShouldMakeAllNullable()
    {
        var entity = new UserWithEnum
        {
            Id = 1,
            Name = "John",
            Status = UserStatus.Active,
            Email = "john@example.com"
        };

        var dto = new UserWithEnumToStringNullableDto(entity);

        dto.Status.Should().Be("Active");

        var statusProperty = typeof(UserWithEnumToStringNullableDto).GetProperty("Status");
        statusProperty.Should().NotBeNull();
        statusProperty!.PropertyType.Should().Be(typeof(string)); 
    }

    [Fact]
    public void ConvertEnumsTo_String_Projection_ShouldExist()
    {
        var projection = UserWithEnumToStringDto.Projection;
        projection.Should().NotBeNull();
    }

    [Fact]
    public void ConvertEnumsTo_String_Projection_ShouldMapCorrectly()
    {
        var entities = new List<UserWithEnum>
        {
            new() { Id = 1, Name = "Alice", Status = UserStatus.Active, Email = "alice@test.com" },
            new() { Id = 2, Name = "Bob", Status = UserStatus.Pending, Email = "bob@test.com" }
        };

        var dtos = entities.AsQueryable()
            .Select(UserWithEnumToStringDto.Projection)
            .ToList();

        dtos.Should().HaveCount(2);
        dtos[0].Status.Should().Be("Active");
        dtos[1].Status.Should().Be("Pending");
    }

    [Fact]
    public void ConvertEnumsTo_Int_Projection_ShouldMapCorrectly()
    {
        var entities = new List<UserWithEnum>
        {
            new() { Id = 1, Name = "Alice", Status = UserStatus.Active, Email = "alice@test.com" },
            new() { Id = 2, Name = "Bob", Status = UserStatus.Suspended, Email = "bob@test.com" }
        };

        var dtos = entities.AsQueryable()
            .Select(UserWithEnumToIntDto.Projection)
            .ToList();

        dtos.Should().HaveCount(2);
        dtos[0].Status.Should().Be(0); 
        dtos[1].Status.Should().Be(3); 
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.Inactive)]
    [InlineData(UserStatus.Pending)]
    [InlineData(UserStatus.Suspended)]
    public void ConvertEnumsTo_String_RoundTrip_ShouldPreserveValue(UserStatus status)
    {
        var original = new UserWithEnum
        {
            Id = 42,
            Name = "Test",
            Status = status,
            Email = "test@test.com"
        };

        var dto = new UserWithEnumToStringDto(original);

        var result = dto.ToSource();

        result.Status.Should().Be(status);
        result.Id.Should().Be(42);
        result.Name.Should().Be("Test");
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.Inactive)]
    [InlineData(UserStatus.Pending)]
    [InlineData(UserStatus.Suspended)]
    public void ConvertEnumsTo_Int_RoundTrip_ShouldPreserveValue(UserStatus status)
    {
        var original = new UserWithEnum
        {
            Id = 42,
            Name = "Test",
            Status = status,
            Email = "test@test.com"
        };

        var dto = new UserWithEnumToIntDto(original);

        var result = dto.ToSource();

        result.Status.Should().Be(status);
    }

    [Fact]
    public void ConvertEnumsTo_String_EnumCollection_ShouldConvertToStringCollection()
    {
        var entity = new EntityWithEnumCollection
        {
            Id = 1,
            Name = "Test",
            Statuses = new List<UserStatus> { UserStatus.Active, UserStatus.Pending, UserStatus.Inactive }
        };

        var dto = new EntityWithEnumCollectionToStringDto(entity);

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Test");
        dto.Statuses.Should().NotBeNull();
        dto.Statuses.Should().HaveCount(3);
        dto.Statuses.Should().Equal("Active", "Pending", "Inactive");
    }

    [Fact]
    public void ConvertEnumsTo_String_EnumCollection_PropertyType_ShouldBeStringCollection()
    {
        var statusesProperty = typeof(EntityWithEnumCollectionToStringDto).GetProperty("Statuses");
        statusesProperty.Should().NotBeNull();
        statusesProperty!.PropertyType.Should().Be(typeof(List<string>));
    }

    [Fact]
    public void ConvertEnumsTo_Int_EnumCollection_ShouldConvertToIntCollection()
    {
        var entity = new EntityWithEnumCollection
        {
            Id = 1,
            Name = "Test",
            Statuses = new List<UserStatus> { UserStatus.Active, UserStatus.Suspended, UserStatus.Pending }
        };

        var dto = new EntityWithEnumCollectionToIntDto(entity);

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Test");
        dto.Statuses.Should().NotBeNull();
        dto.Statuses.Should().HaveCount(3);
        dto.Statuses.Should().Equal(0, 3, 2); 
    }

    [Fact]
    public void ConvertEnumsTo_Int_EnumCollection_PropertyType_ShouldBeIntCollection()
    {
        var statusesProperty = typeof(EntityWithEnumCollectionToIntDto).GetProperty("Statuses");
        statusesProperty.Should().NotBeNull();
        statusesProperty!.PropertyType.Should().Be(typeof(List<int>));
    }

    [Fact]
    public void ConvertEnumsTo_String_EnumCollection_ToSource_ShouldConvertBack()
    {
        var dto = new EntityWithEnumCollectionToStringDto
        {
            Id = 1,
            Name = "Test",
            Statuses = new List<string> { "Active", "Pending", "Inactive" }
        };

        var entity = dto.ToSource();

        entity.Statuses.Should().NotBeNull();
        entity.Statuses.Should().HaveCount(3);
        entity.Statuses.Should().Equal(UserStatus.Active, UserStatus.Pending, UserStatus.Inactive);
    }

    [Fact]
    public void ConvertEnumsTo_Int_EnumCollection_ToSource_ShouldConvertBack()
    {
        var dto = new EntityWithEnumCollectionToIntDto
        {
            Id = 1,
            Name = "Test",
            Statuses = new List<int> { 0, 3, 2 } 
        };

        var entity = dto.ToSource();

        entity.Statuses.Should().NotBeNull();
        entity.Statuses.Should().HaveCount(3);
        entity.Statuses.Should().Equal(UserStatus.Active, UserStatus.Suspended, UserStatus.Pending);
    }

    [Fact]
    public void ConvertEnumsTo_String_EnumCollection_Projection_ShouldMapCorrectly()
    {
        var entities = new List<EntityWithEnumCollection>
        {
            new() { Id = 1, Name = "Test1", Statuses = new List<UserStatus> { UserStatus.Active, UserStatus.Pending } },
            new() { Id = 2, Name = "Test2", Statuses = new List<UserStatus> { UserStatus.Inactive } }
        };

        var dtos = entities.AsQueryable()
            .Select(EntityWithEnumCollectionToStringDto.Projection)
            .ToList();

        dtos.Should().HaveCount(2);
        dtos[0].Statuses.Should().Equal("Active", "Pending");
        dtos[1].Statuses.Should().Equal("Inactive");
    }
}
