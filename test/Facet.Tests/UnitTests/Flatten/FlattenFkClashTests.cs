using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Flatten;

public class FlattenFkClashTests
{
    [Fact]
    public void PersonWithFkFlatDto_WithIgnoreFkClashes_ShouldIncludeFkAndSkipNestedId()
    {
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

        var dto = new PersonWithFkFlatDto(person);

        Assert.Equal(1, dto.Id);
        Assert.Equal("John Doe", dto.Name);
        Assert.Equal(42, dto.AddressId); 
        Assert.Equal("123 Main St", dto.AddressLine1);
        Assert.Equal("Apt 4", dto.AddressLine2);
        Assert.Equal("Springfield", dto.AddressCity);

        var properties = typeof(PersonWithFkFlatDto).GetProperties();
        var addressIdProperties = properties.Where(p => p.Name.Contains("AddressId")).ToList();
        Assert.Single(addressIdProperties); 
        Assert.Equal("AddressId", addressIdProperties[0].Name);
    }

    [Fact]
    public void PersonWithFkFlatDto_WithIgnoreFkClashes_ShouldHandleNullNavigationProperty()
    {
        var person = new PersonWithFk
        {
            Id = 1,
            Name = "John Doe",
            AddressId = 42, 
            Address = null 
        };

        var dto = new PersonWithFkFlatDto(person);

        Assert.Equal(1, dto.Id);
        Assert.Equal("John Doe", dto.Name);
        Assert.Equal(42, dto.AddressId);
        Assert.Null(dto.AddressLine1);
        Assert.Null(dto.AddressCity);
    }

    [Fact]
    public void PersonWithFkFlatNoIgnoreDto_WithoutIgnoreFkClashes_ShouldIncludeBothIds()
    {
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

        var dto = new PersonWithFkFlatNoIgnoreDto(person);

        Assert.Equal(1, dto.Id);
        Assert.Equal("John Doe", dto.Name);
        Assert.Equal(42, dto.AddressId); 
        Assert.Equal(42, dto.AddressId2); 

        var properties = typeof(PersonWithFkFlatNoIgnoreDto).GetProperties();
        var addressIdProperties = properties.Where(p => p.Name.Contains("AddressId")).ToList();
        Assert.Equal(2, addressIdProperties.Count); 
    }

    [Fact]
    public void OrderWithFksFlatDto_WithMultipleFks_ShouldHandleAllClashes()
    {
        var order = new OrderWithFks
        {
            Id = 1,
            OrderDate = new DateTime(2025, 1, 4),
            CustomerId = 100, 
            Customer = new CustomerWithAddress
            {
                Id = 100, 
                Name = "Jane Smith",
                Email = "jane@example.com",
                HomeAddressId = 200, 
                HomeAddress = new AddressWithId
                {
                    Id = 200, 
                    Line1 = "456 Oak Ave",
                    Line2 = "Suite 10",
                    City = "Portland"
                }
            },
            ShippingAddressId = 300, 
            ShippingAddress = new AddressWithId
            {
                Id = 300, 
                Line1 = "789 Pine St",
                Line2 = "",
                City = "Seattle"
            }
        };

        var dto = new OrderWithFksFlatDto(order);

        Assert.Equal(1, dto.Id);
        Assert.Equal(new DateTime(2025, 1, 4), dto.OrderDate);

        Assert.Equal(100, dto.CustomerId);
        Assert.Equal(300, dto.ShippingAddressId);

        Assert.Equal("Jane Smith", dto.CustomerName);
        Assert.Equal("jane@example.com", dto.CustomerEmail);

        Assert.Equal("456 Oak Ave", dto.CustomerHomeAddressLine1);
        Assert.Equal("Suite 10", dto.CustomerHomeAddressLine2);
        Assert.Equal("Portland", dto.CustomerHomeAddressCity);

        Assert.Equal("789 Pine St", dto.ShippingAddressLine1);
        Assert.Equal("", dto.ShippingAddressLine2);
        Assert.Equal("Seattle", dto.ShippingAddressCity);

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
        var projectionProperty = typeof(PersonWithFkFlatDto).GetProperty("Projection");
        Assert.NotNull(projectionProperty);
        Assert.True(projectionProperty.PropertyType.IsGenericType);

        Assert.True(projectionProperty.GetMethod?.IsStatic);
    }

    [Fact]
    public void PersonWithFkFlatDto_PropertiesVerification()
    {
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
            "Projection" 
        }.OrderBy(n => n).ToList();

        Assert.Equal(expectedProperties, properties);
    }

    [Fact]
    public void PersonWithFkFlatNoIgnoreDto_PropertiesVerification()
    {
        var properties = typeof(PersonWithFkFlatNoIgnoreDto).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        var expectedProperties = new[]
        {
            "Id",
            "Name",
            "AddressId",
            "AddressId2", 
            "AddressLine1",
            "AddressLine2",
            "AddressCity",
            "Projection" 
        }.OrderBy(n => n).ToList();

        Assert.Equal(expectedProperties, properties);
    }
}
