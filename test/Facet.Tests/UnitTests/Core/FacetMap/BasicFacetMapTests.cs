using Facet.Tests.TestModels.FacetMap;

namespace Facet.Tests.UnitTests.Core.FacetMap;

public class BasicFacetMapTests
{
    [Fact]
    public void ToTarget_ShouldMapBasicProperties()
    {
        var customer = new Customer
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            PasswordHash = "secret_hash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Balance = 100.50m
        };

        var dto = customer.ToCustomerDto();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.FirstName.Should().Be("John");
        dto.LastName.Should().Be("Doe");
        dto.Email.Should().Be("john@example.com");
        dto.IsActive.Should().BeTrue();
        dto.Balance.Should().Be(100.50m);
    }

    [Fact]
    public void ToTarget_ShouldExcludeSpecifiedProperties()
    {
        var customer = new Customer
        {
            Id = 1,
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            PasswordHash = "hash123",
            CreatedAt = new DateTime(2024, 1, 1)
        };

        var dto = customer.ToCustomerDto();

        // PasswordHash and CreatedAt should not be on the DTO
        var dtoType = typeof(CustomerDto);
        dtoType.GetProperty("PasswordHash").Should().BeNull();
        dtoType.GetProperty("CreatedAt").Should().BeNull();
    }

    [Fact]
    public void ToSource_ShouldMapBackToEntity()
    {
        var dto = new CustomerDto
        {
            Id = 42,
            FirstName = "Bob",
            LastName = "Builder",
            Email = "bob@example.com",
            IsActive = false,
            Balance = 250.00m
        };

        var entity = dto.ToCustomer();

        entity.Should().NotBeNull();
        entity.Id.Should().Be(42);
        entity.FirstName.Should().Be("Bob");
        entity.LastName.Should().Be("Builder");
        entity.Email.Should().Be("bob@example.com");
        entity.IsActive.Should().BeFalse();
        entity.Balance.Should().Be(250.00m);
    }

    [Fact]
    public void ToTarget_WithIncludeOnly_ShouldMapOnlySpecifiedProperties()
    {
        var entity = new SimpleEntity
        {
            Id = 5,
            Name = "Test",
            Description = "A description",
            Secret = "top_secret"
        };

        var dto = entity.ToSimpleDto();

        dto.Id.Should().Be(5);
        dto.Name.Should().Be("Test");
    }

    [Fact]
    public void ToTarget_ShouldThrowOnNull()
    {
        Customer? customer = null;

        var act = () => customer!.ToCustomerDto();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToSource_ShouldThrowOnNull()
    {
        CustomerDto? dto = null;

        var act = () => dto!.ToCustomer();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyToSource_ShouldUpdateExistingEntity()
    {
        var existingCustomer = new Customer
        {
            Id = 1,
            FirstName = "Old",
            LastName = "Name",
            Email = "old@example.com",
            PasswordHash = "original_hash",
            CreatedAt = new DateTime(2020, 1, 1),
            IsActive = false,
            Balance = 0m
        };

        var dto = new CustomerDto
        {
            Id = 1,
            FirstName = "New",
            LastName = "Updated",
            Email = "new@example.com",
            IsActive = true,
            Balance = 500.00m
        };

        dto.ApplyToCustomer(existingCustomer);

        // Mapped properties should be updated
        existingCustomer.FirstName.Should().Be("New");
        existingCustomer.LastName.Should().Be("Updated");
        existingCustomer.Email.Should().Be("new@example.com");
        existingCustomer.IsActive.Should().BeTrue();
        existingCustomer.Balance.Should().Be(500.00m);

        // Excluded properties should remain unchanged
        existingCustomer.PasswordHash.Should().Be("original_hash");
        existingCustomer.CreatedAt.Should().Be(new DateTime(2020, 1, 1));
    }

    [Fact]
    public void ApplyToSource_ShouldThrowOnNullDto()
    {
        CustomerDto? dto = null;
        var customer = new Customer();

        var act = () => dto!.ApplyToCustomer(customer);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyToSource_ShouldThrowOnNullSource()
    {
        var dto = new CustomerDto { Id = 1 };

        var act = () => dto.ApplyToCustomer(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
