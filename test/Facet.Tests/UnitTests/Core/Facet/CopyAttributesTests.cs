using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.Facet;

public class CopyAttributesTests
{
    [Fact]
    public void Facet_ShouldCopyAttributes_WhenCopyAttributesIsTrue()
    {
        // Arrange
        var userWithAnnotations = new UserWithDataAnnotations
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Age = 30,
            PhoneNumber = "555-1234"
        };

        // Act
        var dto = new UserWithDataAnnotationsDto
        {
            FirstName = userWithAnnotations.FirstName,
            LastName = userWithAnnotations.LastName,
            Email = userWithAnnotations.Email,
            Age = userWithAnnotations.Age
        };

        // Assert
        var firstNameProperty = typeof(UserWithDataAnnotationsDto).GetProperty("FirstName");
        var lastNameProperty = typeof(UserWithDataAnnotationsDto).GetProperty("LastName");
        var emailProperty = typeof(UserWithDataAnnotationsDto).GetProperty("Email");
        var ageProperty = typeof(UserWithDataAnnotationsDto).GetProperty("Age");

        firstNameProperty.Should().NotBeNull();
        firstNameProperty!.GetCustomAttribute<RequiredAttribute>().Should().NotBeNull();
        firstNameProperty!.GetCustomAttribute<StringLengthAttribute>().Should().NotBeNull();

        lastNameProperty.Should().NotBeNull();
        lastNameProperty!.GetCustomAttribute<RequiredAttribute>().Should().NotBeNull();

        emailProperty.Should().NotBeNull();
        emailProperty!.GetCustomAttribute<RequiredAttribute>().Should().NotBeNull();
        emailProperty!.GetCustomAttribute<EmailAddressAttribute>().Should().NotBeNull();

        ageProperty.Should().NotBeNull();
        ageProperty!.GetCustomAttribute<RangeAttribute>().Should().NotBeNull();
    }

    [Fact]
    public void Facet_ShouldNotCopyAttributes_WhenCopyAttributesIsFalse()
    {
        // Arrange & Act
        var dtoType = typeof(UserWithDataAnnotationsNoCopyDto);

        // Assert
        var firstNameProperty = dtoType.GetProperty("FirstName");
        var emailProperty = dtoType.GetProperty("Email");

        firstNameProperty.Should().NotBeNull();
        firstNameProperty!.GetCustomAttributes<ValidationAttribute>().Should().BeEmpty();

        emailProperty.Should().NotBeNull();
        emailProperty!.GetCustomAttributes<ValidationAttribute>().Should().BeEmpty();
    }

    [Fact]
    public void Facet_ShouldPreserveAttributeParameters_WhenCopyingAttributes()
    {
        // Arrange & Act
        var firstNameProperty = typeof(UserWithDataAnnotationsDto).GetProperty("FirstName");

        // Assert
        var stringLengthAttr = firstNameProperty!.GetCustomAttribute<StringLengthAttribute>();
        stringLengthAttr.Should().NotBeNull();
        stringLengthAttr!.MaximumLength.Should().Be(50);
    }

    [Fact]
    public void Facet_ShouldCopyRangeAttribute_WithCorrectBounds()
    {
        // Arrange
        var ageProperty = typeof(UserWithDataAnnotationsDto).GetProperty("Age");

        // Assert
        var rangeAttr = ageProperty!.GetCustomAttribute<RangeAttribute>();
        rangeAttr.Should().NotBeNull();
        rangeAttr!.Minimum.Should().Be(0);
        rangeAttr.Maximum.Should().Be(150);
    }

    [Fact]
    public void Facet_ShouldNotCopyCompilerGeneratedAttributes()
    {
        // Arrange
        var dtoType = typeof(UserWithDataAnnotationsDto);

        // Assert
        foreach (var property in dtoType.GetProperties())
        {
            var attributes = property.GetCustomAttributes(true);
            foreach (var attr in attributes)
            {
                var attrType = attr.GetType();
                attrType.Namespace.Should().NotStartWith("System.Runtime.CompilerServices",
                    "Compiler-generated attributes should not be copied");
            }
        }
    }

    [Fact]
    public void Facet_ShouldCopyMultipleAttributes_OnSameProperty()
    {
        // Arrange & Act
        var firstNameProperty = typeof(UserWithDataAnnotationsDto).GetProperty("FirstName");

        // Assert
        var attributes = firstNameProperty!.GetCustomAttributes<ValidationAttribute>().ToList();
        attributes.Should().HaveCountGreaterThanOrEqualTo(2,
            "FirstName should have multiple validation attributes");
        attributes.Should().Contain(a => a is RequiredAttribute);
        attributes.Should().Contain(a => a is StringLengthAttribute);
    }

    [Fact]
    public void Facet_ShouldCopyAttributes_WithNestedFacets()
    {
        var orderDtoType = typeof(ComplexOrderDto);
        var customerProperty = orderDtoType.GetProperty("Customer");
        var orderNumberProperty = orderDtoType.GetProperty("OrderNumber");
        var totalAmountProperty = orderDtoType.GetProperty("TotalAmount");

        orderNumberProperty.Should().NotBeNull();
        orderNumberProperty!.GetCustomAttribute<RequiredAttribute>().Should().NotBeNull();
        orderNumberProperty.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength.Should().Be(20);

        totalAmountProperty.Should().NotBeNull();
        totalAmountProperty!.GetCustomAttribute<RangeAttribute>().Should().NotBeNull();

        customerProperty.Should().NotBeNull();
        customerProperty!.PropertyType.Should().Be(typeof(ComplexCustomerDto));
    }

    [Fact]
    public void Facet_ShouldCopyAttributes_OnNestedFacetProperties()
    {
        var customerDtoType = typeof(ComplexCustomerDto);
        var emailProperty = customerDtoType.GetProperty("Email");
        var fullNameProperty = customerDtoType.GetProperty("FullName");

        emailProperty.Should().NotBeNull();
        emailProperty!.GetCustomAttribute<RequiredAttribute>().Should().NotBeNull();
        emailProperty!.GetCustomAttribute<EmailAddressAttribute>().Should().NotBeNull();

        fullNameProperty.Should().NotBeNull();
        fullNameProperty!.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength.Should().Be(100);
    }

    [Fact]
    public void Facet_ShouldCopyCustomAttributes()
    {
        var productType = typeof(ComplexProductDto);
        var skuProperty = productType.GetProperty("Sku");

        skuProperty.Should().NotBeNull();
        var regexAttr = skuProperty!.GetCustomAttribute<RegularExpressionAttribute>();
        regexAttr.Should().NotBeNull();
    }
}

// Source model with data annotations
public class UserWithDataAnnotations
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Range(0, 150)]
    public int Age { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }

    // This should be excluded and not appear in DTO
    public string Password { get; set; } = string.Empty;
}

// DTO with CopyAttributes = true
[Facet(typeof(UserWithDataAnnotations), "Password", "PhoneNumber", CopyAttributes = true)]
public partial class UserWithDataAnnotationsDto
{
}

// DTO with CopyAttributes = false (default)
[Facet(typeof(UserWithDataAnnotations), "Password", "PhoneNumber", "Age")]
public partial class UserWithDataAnnotationsNoCopyDto
{
}

public class ComplexCustomer
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }
}

[Facet(typeof(ComplexCustomer), "PhoneNumber", CopyAttributes = true)]
public partial class ComplexCustomerDto
{
}

public class ComplexOrder
{
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string OrderNumber { get; set; } = string.Empty;

    [Range(0.01, 1000000)]
    public decimal TotalAmount { get; set; }

    public DateTime OrderDate { get; set; }

    public ComplexCustomer Customer { get; set; } = null!;

    public string? InternalNotes { get; set; }
}

[Facet(typeof(ComplexOrder), "InternalNotes", CopyAttributes = true, NestedFacets = [typeof(ComplexCustomerDto)])]
public partial class ComplexOrderDto
{
}

public class ComplexProduct
{
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^[A-Z]{3}-\d{4}$")]
    public string Sku { get; set; } = string.Empty;

    [Range(0, 10000)]
    public decimal Price { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [Url]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; }
}

[Facet(typeof(ComplexProduct), "IsActive", "ImageUrl", CopyAttributes = true)]
public partial class ComplexProductDto
{
}
