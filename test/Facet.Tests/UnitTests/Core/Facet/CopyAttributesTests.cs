using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

    [Fact]
    public void Facet_ShouldCopyAttributesFromDifferentNamespaces()
    {
        // This test verifies the fix for GitHub issue: Source generation adds attributes
        // like "DefaultValue" and "Column" but not their using statements.
        // Arrange & Act
        var dtoType = typeof(DatabaseTableModelDto);

        // Assert - Verify attributes from different namespaces are copied correctly
        var databaseTableIdProperty = dtoType.GetProperty("DatabaseTableID");
        var firstNameProperty = dtoType.GetProperty("FirstName");
        var systemChangeDateProperty = dtoType.GetProperty("SystemChangeDate");
        var systemChangeTypeProperty = dtoType.GetProperty("SystemChangeType");

        // Check [Key] attribute (System.ComponentModel.DataAnnotations)
        databaseTableIdProperty.Should().NotBeNull();
        databaseTableIdProperty!.GetCustomAttribute<KeyAttribute>().Should().NotBeNull();

        // Check [Column] attribute (System.ComponentModel.DataAnnotations.Schema)
        systemChangeDateProperty.Should().NotBeNull();
        var columnAttr = systemChangeDateProperty!.GetCustomAttribute<ColumnAttribute>();
        columnAttr.Should().NotBeNull();
        columnAttr!.Order.Should().Be(500);

        // Check [DefaultValue] attribute (System.ComponentModel)
        systemChangeTypeProperty.Should().NotBeNull();
        var defaultValueAttr = systemChangeTypeProperty!.GetCustomAttribute<DefaultValueAttribute>();
        defaultValueAttr.Should().NotBeNull();
        defaultValueAttr!.Value.Should().Be("I");

        // Verify all Column attributes with Order
        var systemChangeLoginProperty = dtoType.GetProperty("SystemChangeLogin");
        systemChangeLoginProperty.Should().NotBeNull();
        var loginColumnAttr = systemChangeLoginProperty!.GetCustomAttribute<ColumnAttribute>();
        loginColumnAttr.Should().NotBeNull();
        loginColumnAttr!.Order.Should().Be(502);
    }

    [Fact]
    public void Facet_ShouldCopyConcurrencyCheckAttribute()
    {
        // Arrange & Act
        var dtoType = typeof(DatabaseTableModelDto);
        var systemChangeDateProperty = dtoType.GetProperty("SystemChangeDate");

        // Assert - Check [ConcurrencyCheck] attribute (System.ComponentModel.DataAnnotations)
        systemChangeDateProperty.Should().NotBeNull();
        systemChangeDateProperty!.GetCustomAttribute<ConcurrencyCheckAttribute>().Should().NotBeNull();
    }

    [Fact]
    public void Facet_ShouldCopyCustomAttributeWithEnumConstructorArgument()
    {
        // Arrange & Act
        var dtoType = typeof(DatabaseTableWithEnumAttributeDto);

        // Assert - Verify the attribute with enum was copied correctly
        var amountProperty = dtoType.GetProperty("Amount");
        amountProperty.Should().NotBeNull();

        var defaultSortAttr = amountProperty!.GetCustomAttribute<DefaultSortAttribute>();
        defaultSortAttr.Should().NotBeNull();
        defaultSortAttr!.Direction.Should().Be(SortDirection.Descending);
        defaultSortAttr.SortPrecedence.Should().Be(0);
    }

    [Fact]
    public void Facet_ShouldCopyAttributeWithMultipleEnumValues()
    {
        // Additional test for enum attributes with different values
        var dtoType = typeof(DatabaseTableWithEnumAttributeDto);

        var createdAtProperty = dtoType.GetProperty("CreatedAt");
        createdAtProperty.Should().NotBeNull();

        var defaultSortAttr = createdAtProperty!.GetCustomAttribute<DefaultSortAttribute>();
        defaultSortAttr.Should().NotBeNull();
        defaultSortAttr!.Direction.Should().Be(SortDirection.Ascending);
        defaultSortAttr.SortPrecedence.Should().Be(1);
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

// Source model with attributes from different namespaces (matching the reported issue)
public class DatabaseTableModel
{
    [Key]
    public long DatabaseTableID { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    [ConcurrencyCheck]
    [Column(Order = 500)]
    public DateTime? SystemChangeDate { get; set; }

    [DefaultValue("I")]
    [Column(Order = 501)]
    public string SystemChangeType { get; set; } = string.Empty;

    [Column(Order = 502)]
    public string SystemChangeLogin { get; set; } = string.Empty;
}

// DTO with CopyAttributes = true - should compile with proper using statements
[Facet(typeof(DatabaseTableModel), CopyAttributes = true)]
public partial class DatabaseTableModelDto
{
}

// Custom attribute with enum parameter
public enum SortDirection
{
    Ascending,
    Descending
}

public class DefaultSortAttribute : Attribute
{
    public SortDirection Direction { get; set; }
    public int SortPrecedence { get; set; } = 0;

    public DefaultSortAttribute(SortDirection direction, int sortPrecedence = 0)
    {
        Direction = direction;
        SortPrecedence = sortPrecedence;
    }
}

// Source model with custom attribute that has enum constructor argument
public class DatabaseTableWithEnumAttribute
{
    [Key]
    public long DatabaseTableID { get; set; }

    [DefaultSort(SortDirection.Descending, 0)]
    public decimal Amount { get; set; }

    [DefaultSort(SortDirection.Ascending, 1)]
    public DateTime CreatedAt { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;
}

// DTO with CopyAttributes = true - should compile with custom enum attribute
[Facet(typeof(DatabaseTableWithEnumAttribute), CopyAttributes = true)]
public partial class DatabaseTableWithEnumAttributeDto
{
}
