using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Flatten;

public class FlattenBasicTests
{
    [Fact]
    public void PersonFlatDto_ShouldHaveAllFlattenedProperties()
    {
        // Arrange
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

        // Act
        var dto = new PersonFlatDto(person);

        // Assert
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
        // Arrange
        var person = new Person
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Address = null!,
            ContactInfo = null!
        };

        // Act
        var dto = new PersonFlatDto(person);

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("John", dto.FirstName);
        Assert.Null(dto.AddressStreet);
        Assert.Null(dto.AddressCity);
        Assert.Null(dto.ContactInfoEmail);
    }

    [Fact]
    public void PersonFlatDto_ShouldHandlePartiallyNullNestedObjects()
    {
        // Arrange
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
                Country = null! // Null nested object
            },
            ContactInfo = new ContactInfo
            {
                Email = "john@example.com",
                Phone = "555-1234"
            }
        };

        // Act
        var dto = new PersonFlatDto(person);

        // Assert
        Assert.Equal("123 Main St", dto.AddressStreet);
        Assert.Equal("Springfield", dto.AddressCity);
        Assert.Null(dto.AddressCountryName);
        Assert.Null(dto.AddressCountryCode);
    }

    [Fact]
    public void PersonFlatDto_Projection_ShouldWork()
    {
        // Arrange
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

        // Act
        var projection = PersonFlatDto.Projection.Compile();
        var dto = projection(people[0]);

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("John", dto.FirstName);
        Assert.Equal("123 Main St", dto.AddressStreet);
        Assert.Equal("USA", dto.AddressCountryName);
    }

    [Fact]
    public void PersonFlatDepth2Dto_ShouldOnlyFlattenTwoLevelsDeep()
    {
        // Arrange
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

        // Act
        var dto = new PersonFlatDepth2Dto(person);

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("John", dto.FirstName);
        Assert.Equal("123 Main St", dto.AddressStreet);

        // Country should NOT be flattened (it's at depth 3)
        // Check that Country properties don't exist by checking the type
        var type = typeof(PersonFlatDepth2Dto);
        Assert.Null(type.GetProperty("AddressCountryName"));
        Assert.Null(type.GetProperty("AddressCountryCode"));
    }

    [Fact]
    public void PersonFlatWithoutContactDto_ShouldExcludeContactInfo()
    {
        // Arrange
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

        // Act
        var dto = new PersonFlatWithoutContactDto(person);

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("John", dto.FirstName);
        Assert.Equal("123 Main St", dto.AddressStreet);

        // ContactInfo properties should NOT exist
        var type = typeof(PersonFlatWithoutContactDto);
        Assert.Null(type.GetProperty("ContactInfoEmail"));
        Assert.Null(type.GetProperty("ContactInfoPhone"));
    }

    [Fact]
    public void PersonFlatWithoutCountryDto_ShouldExcludeCountry()
    {
        // Arrange
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

        // Act
        var dto = new PersonFlatWithoutCountryDto(person);

        // Assert
        Assert.Equal("123 Main St", dto.AddressStreet);
        Assert.Equal("Springfield", dto.AddressCity);

        // Country properties should NOT exist
        var type = typeof(PersonFlatWithoutCountryDto);
        Assert.Null(type.GetProperty("AddressCountryName"));
        Assert.Null(type.GetProperty("AddressCountryCode"));
    }

    [Fact]
    public void PersonFlatDto_ParameterlessConstructor_ShouldWork()
    {
        // Act
        var dto = new PersonFlatDto();

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(0, dto.Id);
        Assert.Null(dto.FirstName);
    }

    [Fact]
    public void PersonFlatLeafOnlyDto_ShouldUseLeafOnlyPropertyNames()
    {
        // Arrange
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

        // Act
        var dto = new PersonFlatLeafOnlyDto(person);

        // Assert - verify leaf-only naming (not prefixed with parent names)
        var type = typeof(PersonFlatLeafOnlyDto);

        // Root properties should exist as-is
        Assert.Equal(1, dto.Id);
        Assert.Equal("John", dto.FirstName);
        Assert.Equal("Doe", dto.LastName);

        // Nested properties should use leaf names only
        Assert.NotNull(type.GetProperty("Street"));
        Assert.NotNull(type.GetProperty("City"));
        Assert.NotNull(type.GetProperty("ZipCode"));
        Assert.NotNull(type.GetProperty("Name"));
        Assert.NotNull(type.GetProperty("Code"));
        Assert.NotNull(type.GetProperty("Email"));
        Assert.NotNull(type.GetProperty("Phone"));

        // Prefixed names should NOT exist
        Assert.Null(type.GetProperty("AddressStreet"));
        Assert.Null(type.GetProperty("AddressCity"));
        Assert.Null(type.GetProperty("AddressCountryName"));
        Assert.Null(type.GetProperty("ContactInfoEmail"));

        // Verify values
        Assert.Equal("123 Main St", type.GetProperty("Street")!.GetValue(dto));
        Assert.Equal("Springfield", type.GetProperty("City")!.GetValue(dto));
        Assert.Equal("USA", type.GetProperty("Name")!.GetValue(dto));
        Assert.Equal("john@example.com", type.GetProperty("Email")!.GetValue(dto));
    }
}
