using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Flatten;

public class FlattenAdvancedTests
{
    [Fact]
    public void CompanyFlatDto_ShouldIgnoreCollections()
    {
        var company = new Company
        {
            Id = 1,
            Name = "Acme Corp",
            HeadquartersAddress = new Address
            {
                Street = "456 Business Rd",
                City = "New York",
                ZipCode = "10001",
                Country = new Country { Name = "USA", Code = "US" }
            },
            Workers = new List<Worker>
            {
                new Worker { Id = 1, Name = "Alice", Title = "Engineer", CompanyId = 1 },
                new Worker { Id = 2, Name = "Bob", Title = "Manager", CompanyId = 1 }
            }
        };

        var dto = new CompanyFlatDto(company);

        Assert.Equal(1, dto.Id);
        Assert.Equal("Acme Corp", dto.Name);
        Assert.Equal("456 Business Rd", dto.HeadquartersAddressStreet);
        Assert.Equal("New York", dto.HeadquartersAddressCity);

        var type = typeof(CompanyFlatDto);
        Assert.Null(type.GetProperty("WorkersCount"));
        Assert.Null(type.GetProperty("WorkersIsReadOnly"));
        Assert.Null(type.GetProperty("Workers"));
    }

    [Fact]
    public void OrderFlatDto_WithIgnoreNestedIds_ShouldOnlyIncludeTopLevelId()
    {
        var order = new Order
        {
            Id = 100,
            OrderDate = new DateTime(2024, 1, 15),
            CustomerId = 50,
            Customer = new Customer
            {
                Id = 50,
                Name = "Jane Smith",
                Email = "jane@example.com",
                PreferredAddressId = 10
            },
            Total = 250.00m
        };

        var dto = new OrderFlatDto(order);

        Assert.Equal(100, dto.Id);
        Assert.Equal(new DateTime(2024, 1, 15), dto.OrderDate);
        Assert.Equal(250.00m, dto.Total);

        Assert.Equal("Jane Smith", dto.CustomerName);
        Assert.Equal("jane@example.com", dto.CustomerEmail);

        var type = typeof(OrderFlatDto);
        Assert.Null(type.GetProperty("CustomerId")); 
        Assert.Null(type.GetProperty("CustomerId")); 
        Assert.Null(type.GetProperty("CustomerPreferredAddressId")); 
    }

    [Fact]
    public void OrderFlatWithAllIdsDto_WithoutIgnoreNestedIds_ShouldIncludeAllIds()
    {
        var order = new Order
        {
            Id = 100,
            OrderDate = new DateTime(2024, 1, 15),
            CustomerId = 50,
            Customer = new Customer
            {
                Id = 50,
                Name = "Jane Smith",
                Email = "jane@example.com",
                PreferredAddressId = 10
            },
            Total = 250.00m
        };

        var dto = new OrderFlatWithAllIdsDto(order);

        Assert.Equal(100, dto.Id);
        Assert.Equal(50, dto.CustomerId);
        Assert.Equal(50, dto.CustomerId);
        Assert.Equal(10, dto.CustomerPreferredAddressId);
        Assert.Equal("Jane Smith", dto.CustomerName);
        Assert.Equal("jane@example.com", dto.CustomerEmail);
    }

    [Fact]
    public void OrderFlatDto_Projection_ShouldWorkWithIgnoreNestedIds()
    {
        var orders = new[]
        {
            new Order
            {
                Id = 100,
                OrderDate = new DateTime(2024, 1, 15),
                CustomerId = 50,
                Customer = new Customer
                {
                    Id = 50,
                    Name = "Jane Smith",
                    Email = "jane@example.com",
                    PreferredAddressId = 10
                },
                Total = 250.00m
            }
        };

        var projection = OrderFlatDto.Projection.Compile();
        var dto = projection(orders[0]);

        Assert.Equal(100, dto.Id);
        Assert.Equal("Jane Smith", dto.CustomerName);
        Assert.Equal("jane@example.com", dto.CustomerEmail);

        var type = typeof(OrderFlatDto);
        Assert.Null(type.GetProperty("CustomerId"));
        Assert.Null(type.GetProperty("CustomerPreferredAddressId"));
    }
}
