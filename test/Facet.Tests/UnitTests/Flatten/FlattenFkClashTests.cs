using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Flatten;

public class FlattenFkClashTests
{
    [Fact]
    public void PersonWithFkFlatDto_WithIgnoreFkClashes_ShouldIncludeFkAndSkipNestedId()
    {
        // Arrange
        var person = new PersonWithFk
        {
            Id = 1,
            Name = "John Doe",
            AddressId = 42, // Foreign key
            Address = new AddressWithId
            {
                Id = 42, // This should be skipped when IgnoreForeignKeyClashes = true
                Line1 = "123 Main St",
                Line2 = "Apt 4",
                City = "Springfield"
            }
        };

        // Act
        var dto = new PersonWithFkFlatDto(person);

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("John Doe", dto.Name);
        Assert.Equal(42, dto.AddressId); // FK included
        Assert.Equal("123 Main St", dto.AddressLine1);
        Assert.Equal("Apt 4", dto.AddressLine2);
        Assert.Equal("Springfield", dto.AddressCity);

        // Verify Address.Id was skipped (no AddressId2 or duplicate)
        var properties = typeof(PersonWithFkFlatDto).GetProperties();
        var addressIdProperties = properties.Where(p => p.Name.Contains("AddressId")).ToList();
        Assert.Single(addressIdProperties); // Only one AddressId property
        Assert.Equal("AddressId", addressIdProperties[0].Name);
    }

    [Fact]
    public void PersonWithFkFlatDto_WithIgnoreFkClashes_ShouldHandleNullNavigationProperty()
    {
        // Arrange
        var person = new PersonWithFk
        {
            Id = 1,
            Name = "John Doe",
            AddressId = 42, // FK present
            Address = null // Navigation property is null
        };

        // Act
        var dto = new PersonWithFkFlatDto(person);

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("John Doe", dto.Name);
        Assert.Equal(42, dto.AddressId);
        Assert.Null(dto.AddressLine1);
        Assert.Null(dto.AddressCity);
    }

    [Fact]
    public void PersonWithFkFlatNoIgnoreDto_WithoutIgnoreFkClashes_ShouldIncludeBothIds()
    {
        // Arrange
        var person = new PersonWithFk
        {
            Id = 1,
            Name = "John Doe",
            AddressId = 42,
            Address = new AddressWithId
            {
                Id = 42,
                Line1 = "123 Main St",
                Line2 = "Apt 4",
                City = "Springfield"
            }
        };

        // Act
        var dto = new PersonWithFkFlatNoIgnoreDto(person);

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("John Doe", dto.Name);
        Assert.Equal(42, dto.AddressId); // FK included
        Assert.Equal(42, dto.AddressId2); // Address.Id included as AddressId2 (name collision)

        // Verify both AddressId properties exist
        var properties = typeof(PersonWithFkFlatNoIgnoreDto).GetProperties();
        var addressIdProperties = properties.Where(p => p.Name.Contains("AddressId")).ToList();
        Assert.Equal(2, addressIdProperties.Count); // Two properties: AddressId and AddressId2
    }

    [Fact]
    public void OrderWithFksFlatDto_WithMultipleFks_ShouldHandleAllClashes()
    {
        // Arrange
        var order = new OrderWithFks
        {
            Id = 1,
            OrderDate = new DateTime(2025, 1, 4),
            CustomerId = 100, // FK
            Customer = new CustomerWithAddress
            {
                Id = 100, // Should be skipped
                Name = "Jane Smith",
                Email = "jane@example.com",
                HomeAddressId = 200, // Nested FK - should be skipped
                HomeAddress = new AddressWithId
                {
                    Id = 200, // Should be skipped
                    Line1 = "456 Oak Ave",
                    Line2 = "Suite 10",
                    City = "Portland"
                }
            },
            ShippingAddressId = 300, // FK
            ShippingAddress = new AddressWithId
            {
                Id = 300, // Should be skipped
                Line1 = "789 Pine St",
                Line2 = "",
                City = "Seattle"
            }
        };

        // Act
        var dto = new OrderWithFksFlatDto(order);

        // Assert
        // Root properties
        Assert.Equal(1, dto.Id);
        Assert.Equal(new DateTime(2025, 1, 4), dto.OrderDate);

        // Foreign keys included
        Assert.Equal(100, dto.CustomerId);
        Assert.Equal(300, dto.ShippingAddressId);

        // Customer properties (non-Id)
        Assert.Equal("Jane Smith", dto.CustomerName);
        Assert.Equal("jane@example.com", dto.CustomerEmail);

        // Customer.HomeAddress properties (non-Id)
        Assert.Equal("456 Oak Ave", dto.CustomerHomeAddressLine1);
        Assert.Equal("Suite 10", dto.CustomerHomeAddressLine2);
        Assert.Equal("Portland", dto.CustomerHomeAddressCity);

        // ShippingAddress properties (non-Id)
        Assert.Equal("789 Pine St", dto.ShippingAddressLine1);
        Assert.Equal("", dto.ShippingAddressLine2);
        Assert.Equal("Seattle", dto.ShippingAddressCity);

        // Verify no Id clashes - should not have CustomerId2, ShippingAddressId2, or CustomerHomeAddressId/CustomerHomeAddressId2
        var properties = typeof(OrderWithFksFlatDto).GetProperties();
        var propertyNames = properties.Select(p => p.Name).ToList();

        Assert.DoesNotContain("CustomerId2", propertyNames);
        Assert.DoesNotContain("ShippingAddressId2", propertyNames);
        Assert.DoesNotContain("CustomerHomeAddressId", propertyNames);
        Assert.DoesNotContain("CustomerHomeAddressId2", propertyNames);
    }

    [Fact]
    public void PersonWithFkFlatDto_WithProjection_ShouldHaveProjectionProperty()
    {
        // Verify the Projection property exists and is of the correct type
        var projectionProperty = typeof(PersonWithFkFlatDto).GetProperty("Projection");
        Assert.NotNull(projectionProperty);
        Assert.True(projectionProperty.PropertyType.IsGenericType);

        // Verify it's a static property
        Assert.True(projectionProperty.GetMethod?.IsStatic);
    }

    [Fact]
    public void PersonWithFkFlatDto_PropertiesVerification()
    {
        // Verify the generated DTO has the expected properties
        var properties = typeof(PersonWithFkFlatDto).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        var expectedProperties = new[]
        {
            "Id",
            "Name",
            "AddressId",
            "AddressLine1",
            "AddressLine2",
            "AddressCity",
            "Projection" // Static projection property
        }.OrderBy(n => n).ToList();

        Assert.Equal(expectedProperties, properties);
    }

    [Fact]
    public void PersonWithFkFlatNoIgnoreDto_PropertiesVerification()
    {
        // Verify the generated DTO has the expected properties including AddressId2
        var properties = typeof(PersonWithFkFlatNoIgnoreDto).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        var expectedProperties = new[]
        {
            "Id",
            "Name",
            "AddressId",
            "AddressId2", // Address.Id creates a collision, becomes AddressId2
            "AddressLine1",
            "AddressLine2",
            "AddressCity",
            "Projection" // Static projection property
        }.OrderBy(n => n).ToList();

        Assert.Equal(expectedProperties, properties);
    }
}
