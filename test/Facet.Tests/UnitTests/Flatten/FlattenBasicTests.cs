using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Flatten;

public class FlattenBasicTests
{
    [Fact]
    public void PersonFlatDto_ShouldHaveAllFlattenedProperties()
    {
        var person = new Person
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Address = new Address
            {
                Street = "123 Main St",
                City = "Springfield",
                ZipCode = "12345",
                Country = new Country
                {
                    Name = "USA",
                    Code = "US"
                }
            },
            ContactInfo = new ContactInfo
            {
                Email = "john@example.com",
                Phone = "555-1234"
            }
        };

        var dto = new PersonFlatDto(person);

        Assert.Equal(1, dto.Id);
        Assert.Equal("John", dto.FirstName);
        Assert.Equal("Doe", dto.LastName);
        Assert.Equal(new DateTime(1990, 1, 1), dto.DateOfBirth);
        Assert.Equal("123 Main St", dto.AddressStreet);
        Assert.Equal("Springfield", dto.AddressCity);
        Assert.Equal("12345", dto.AddressZipCode);
        Assert.Equal("USA", dto.AddressCountryName);
        Assert.Equal("US", dto.AddressCountryCode);
        Assert.Equal("john@example.com", dto.ContactInfoEmail);
        Assert.Equal("555-1234", dto.ContactInfoPhone);
    }

    [Fact]
    public void PersonFlatDto_ShouldHandleNullNestedObjects()
    {
        var person = new Person
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Address = null!,
            ContactInfo = null!
        };

        var dto = new PersonFlatDto(person);

        Assert.Equal(1, dto.Id);
        Assert.Equal("John", dto.FirstName);
        Assert.Null(dto.AddressStreet);
        Assert.Null(dto.AddressCity);
        Assert.Null(dto.ContactInfoEmail);
    }

    [Fact]
    public void PersonFlatDto_ShouldHandlePartiallyNullNestedObjects()
    {
        var person = new Person
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Address = new Address
            {
                Street = "123 Main St",
                City = "Springfield",
                ZipCode = "12345",
                Country = null! 
            },
            ContactInfo = new ContactInfo
            {
                Email = "john@example.com",
                Phone = "555-1234"
            }
        };

        var dto = new PersonFlatDto(person);

        Assert.Equal("123 Main St", dto.AddressStreet);
        Assert.Equal("Springfield", dto.AddressCity);
        Assert.Null(dto.AddressCountryName);
        Assert.Null(dto.AddressCountryCode);
    }

    [Fact]
    public void PersonFlatDto_Projection_ShouldWork()
    {
        var people = new[]
        {
            new Person
            {
                Id = 1,
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = new DateTime(1990, 1, 1),
                Address = new Address
                {
                    Street = "123 Main St",
                    City = "Springfield",
                    ZipCode = "12345",
                    Country = new Country { Name = "USA", Code = "US" }
                },
                ContactInfo = new ContactInfo
                {
                    Email = "john@example.com",
                    Phone = "555-1234"
                }
            }
        };

        var projection = PersonFlatDto.Projection.Compile();
        var dto = projection(people[0]);

        Assert.Equal(1, dto.Id);
        Assert.Equal("John", dto.FirstName);
        Assert.Equal("123 Main St", dto.AddressStreet);
        Assert.Equal("USA", dto.AddressCountryName);
    }

    [Fact]
    public void PersonFlatDepth2Dto_ShouldOnlyFlattenTwoLevelsDeep()
    {
        var person = new Person
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Address = new Address
            {
                Street = "123 Main St",
                City = "Springfield",
                ZipCode = "12345",
                Country = new Country { Name = "USA", Code = "US" }
            },
            ContactInfo = new ContactInfo
            {
                Email = "john@example.com",
                Phone = "555-1234"
            }
        };

        var dto = new PersonFlatDepth2Dto(person);

        Assert.Equal(1, dto.Id);
        Assert.Equal("John", dto.FirstName);
        Assert.Equal("123 Main St", dto.AddressStreet);

        var type = typeof(PersonFlatDepth2Dto);
        Assert.Null(type.GetProperty("AddressCountryName"));
        Assert.Null(type.GetProperty("AddressCountryCode"));
    }

    [Fact]
    public void PersonFlatWithoutContactDto_ShouldExcludeContactInfo()
    {
        var person = new Person
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Address = new Address
            {
                Street = "123 Main St",
                City = "Springfield",
                ZipCode = "12345",
                Country = new Country { Name = "USA", Code = "US" }
            },
            ContactInfo = new ContactInfo
            {
                Email = "john@example.com",
                Phone = "555-1234"
            }
        };

        var dto = new PersonFlatWithoutContactDto(person);

        Assert.Equal(1, dto.Id);
        Assert.Equal("John", dto.FirstName);
        Assert.Equal("123 Main St", dto.AddressStreet);

        var type = typeof(PersonFlatWithoutContactDto);
        Assert.Null(type.GetProperty("ContactInfoEmail"));
        Assert.Null(type.GetProperty("ContactInfoPhone"));
    }

    [Fact]
    public void PersonFlatWithoutCountryDto_ShouldExcludeCountry()
    {
        var person = new Person
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Address = new Address
            {
                Street = "123 Main St",
                City = "Springfield",
                ZipCode = "12345",
                Country = new Country { Name = "USA", Code = "US" }
            },
            ContactInfo = new ContactInfo
            {
                Email = "john@example.com",
                Phone = "555-1234"
            }
        };

        var dto = new PersonFlatWithoutCountryDto(person);

        Assert.Equal("123 Main St", dto.AddressStreet);
        Assert.Equal("Springfield", dto.AddressCity);

        var type = typeof(PersonFlatWithoutCountryDto);
        Assert.Null(type.GetProperty("AddressCountryName"));
        Assert.Null(type.GetProperty("AddressCountryCode"));
    }

    [Fact]
    public void PersonFlatDto_ParameterlessConstructor_ShouldWork()
    {
        var dto = new PersonFlatDto();

        Assert.NotNull(dto);
        Assert.Equal(0, dto.Id);
        Assert.Null(dto.FirstName);
    }

    [Fact]
    public void PersonFlatLeafOnlyDto_ShouldUseLeafOnlyPropertyNames()
    {
        var person = new Person
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Address = new Address
            {
                Street = "123 Main St",
                City = "Springfield",
                ZipCode = "12345",
                Country = new Country
                {
                    Name = "USA",
                    Code = "US"
                }
            },
            ContactInfo = new ContactInfo
            {
                Email = "john@example.com",
                Phone = "555-1234"
            }
        };

        var dto = new PersonFlatLeafOnlyDto(person);

        var type = typeof(PersonFlatLeafOnlyDto);

        Assert.Equal(1, dto.Id);
        Assert.Equal("John", dto.FirstName);
        Assert.Equal("Doe", dto.LastName);

        Assert.NotNull(type.GetProperty("Street"));
        Assert.NotNull(type.GetProperty("City"));
        Assert.NotNull(type.GetProperty("ZipCode"));
        Assert.NotNull(type.GetProperty("Name"));
        Assert.NotNull(type.GetProperty("Code"));
        Assert.NotNull(type.GetProperty("Email"));
        Assert.NotNull(type.GetProperty("Phone"));

        Assert.Null(type.GetProperty("AddressStreet"));
        Assert.Null(type.GetProperty("AddressCity"));
        Assert.Null(type.GetProperty("AddressCountryName"));
        Assert.Null(type.GetProperty("ContactInfoEmail"));

        Assert.Equal("123 Main St", type.GetProperty("Street")!.GetValue(dto));
        Assert.Equal("Springfield", type.GetProperty("City")!.GetValue(dto));
        Assert.Equal("USA", type.GetProperty("Name")!.GetValue(dto));
        Assert.Equal("john@example.com", type.GetProperty("Email")!.GetValue(dto));
    }
}
