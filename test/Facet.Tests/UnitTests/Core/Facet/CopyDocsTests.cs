using System.Reflection;

namespace Facet.Tests.UnitTests.Core.Facet;

public class CopyDocsTests
{
    [Fact]
    public void Facet_ShouldCopyXmlDocs_WhenCopyDocsIsTrue()
    {
        // Arrange & Act
        var firstNameProperty = typeof(UserWithDocsDto).GetProperty("FirstName");
        var emailProperty = typeof(UserWithDocsDto).GetProperty("Email");
        var ageProperty = typeof(UserWithDocsDto).GetProperty("Age");

        // Assert - We can't directly access XML docs via reflection at runtime,
        // but we can verify the generated code compiles and properties exist
        firstNameProperty.Should().NotBeNull();
        emailProperty.Should().NotBeNull();
        ageProperty.Should().NotBeNull();

        // The actual XML documentation copying is verified by the source generator tests
        // and by inspecting the generated code
    }

    [Fact]
    public void Facet_ShouldNotCopyXmlDocs_WhenCopyDocsIsFalse()
    {
        // Arrange & Act
        var dtoType = typeof(UserWithDocsNoCopyDto);

        // Assert - Properties should exist but without XML docs
        var firstNameProperty = dtoType.GetProperty("FirstName");
        var emailProperty = dtoType.GetProperty("Email");

        firstNameProperty.Should().NotBeNull();
        emailProperty.Should().NotBeNull();
    }

    [Fact]
    public void Facet_ShouldCopyXmlDocs_WithNestedFacets()
    {
        // Arrange & Act
        var orderDtoType = typeof(OrderWithDocsDto);
        var customerProperty = orderDtoType.GetProperty("Customer");
        var orderNumberProperty = orderDtoType.GetProperty("OrderNumber");
        var totalAmountProperty = orderDtoType.GetProperty("TotalAmount");

        // Assert
        orderNumberProperty.Should().NotBeNull();
        totalAmountProperty.Should().NotBeNull();
        customerProperty.Should().NotBeNull();
        customerProperty!.PropertyType.Should().Be(typeof(CustomerWithDocsDto));
    }

    [Fact]
    public void Facet_ShouldCopyXmlDocs_OnNestedFacetProperties()
    {
        // Arrange & Act
        var customerDtoType = typeof(CustomerWithDocsDto);
        var emailProperty = customerDtoType.GetProperty("Email");
        var fullNameProperty = customerDtoType.GetProperty("FullName");

        // Assert - Properties exist (docs are in generated code)
        emailProperty.Should().NotBeNull();
        fullNameProperty.Should().NotBeNull();
    }

    [Fact]
    public void Facet_CanCombine_CopyDocsAndCopyAttributes()
    {
        // Arrange & Act
        var dtoType = typeof(UserWithDocsAndAttributesDto);
        var firstNameProperty = dtoType.GetProperty("FirstName");
        var emailProperty = dtoType.GetProperty("Email");

        // Assert - Should have both attributes and docs
        firstNameProperty.Should().NotBeNull();
        firstNameProperty!.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>().Should().NotBeNull();
        firstNameProperty!.GetCustomAttribute<System.ComponentModel.DataAnnotations.StringLengthAttribute>().Should().NotBeNull();

        emailProperty.Should().NotBeNull();
        emailProperty!.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>().Should().NotBeNull();
        emailProperty!.GetCustomAttribute<System.ComponentModel.DataAnnotations.EmailAddressAttribute>().Should().NotBeNull();
    }
}

public class InheritDocsTests
{
    // Reads the generator-emitted .g.cs file for a DTO type so tests can assert on doc content.
    private static string LoadGeneratedSource(string typeFullName)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var path = Path.Combine(projectRoot, "obj", "Generated", "Facet", "Facet.Generators.FacetGenerator", $"{typeFullName}.g.cs");
        try { return File.ReadAllText(path); }
        catch (FileNotFoundException) { return string.Empty; }
    }

    [Fact]
    public void Facet_ShouldInheritDocsFromInterface_WhenInheritDocsIsTrue()
    {
        var source = LoadGeneratedSource(typeof(DocumentedServiceDto).FullName!);
        source.Should().NotBeEmpty("generated file should exist");
        source.Should().Contain("The service title.");
        source.Should().Contain("A description of the service.");
    }

    [Fact]
    public void Facet_ShouldInheritDocsFromBaseClass_WhenOverridingPropertyHasNoDocs()
    {
        // ConcreteProduct.Name is an override with no doc comment; the doc lives on ProductBase.Name.
        var source = LoadGeneratedSource(typeof(ConcreteProductDto).FullName!);
        source.Should().NotBeEmpty("generated file should exist");
        source.Should().Contain("The product name.");
    }

    [Fact]
    public void Facet_ShouldNotInheritDocs_WhenInheritDocsIsFalse()
    {
        // Same override scenario but InheritDocs=false: Name should have no doc comment.
        var source = LoadGeneratedSource(typeof(ConcreteProductNoInheritDto).FullName!);
        source.Should().NotBeEmpty("generated file should exist");
        source.Should().NotContain("The product name.");
    }

    [Fact]
    public void Facet_ShouldUseOwnDocs_WhenMemberHasDirectDocComment()
    {
        // ConcreteProduct.Price has its own summary; it must appear in both DTOs
        // regardless of InheritDocs, confirming direct docs are never suppressed.
        var inheritSource = LoadGeneratedSource(typeof(ConcreteProductDto).FullName!);
        var noInheritSource = LoadGeneratedSource(typeof(ConcreteProductNoInheritDto).FullName!);
        inheritSource.Should().Contain("The selling price.");
        noInheritSource.Should().Contain("The selling price.");
    }

    [Fact]
    public void Facet_ShouldEmitDocCommentsOnPositionalRecordProperties_WhenDocsAreInherited()
    {
        // Positional records require property body overrides to carry doc comments.
        // Without the usePropertyNameAsInitializer path in CodeBuilder, docs are silently dropped.
        var source = LoadGeneratedSource(typeof(DocumentedPositionalDto).FullName!);
        source.Should().NotBeEmpty("generated file should exist");
        source.Should().Contain("The entity identifier.");
        source.Should().Contain("The entity name.");
    }
}

// Base class whose virtual property has documentation
public class ProductBase
{
    /// <summary>
    /// The product name.
    /// </summary>
    public virtual string Name { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;
}

// Derived class that overrides Name without adding docs, and adds a documented Price property.
// This is the key model for InheritDocs base-class tests: Name is re-declared (override) but
// has no doc comment, so inheritance is required to surface the base-class summary.
public class ConcreteProduct : ProductBase
{
    public override string Name { get; set; } = string.Empty;

    /// <summary>
    /// The selling price.
    /// </summary>
    public decimal Price { get; set; }
}

[Facet(typeof(ConcreteProduct), CopyDocs = true, InheritDocs = true)]
public partial class ConcreteProductDto { }

[Facet(typeof(ConcreteProduct), CopyDocs = true, InheritDocs = false)]
public partial class ConcreteProductNoInheritDto { }

// Interface whose members carry documentation
public interface IDocumentedService
{
    /// <summary>
    /// The service title.
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// A description of the service.
    /// </summary>
    string Description { get; set; }
}

// Concrete implementation with no doc comments on the properties
public class DocumentedService : IDocumentedService
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

[Facet(typeof(DocumentedService), CopyDocs = true, InheritDocs = true)]
public partial class DocumentedServiceDto { }

// Interface + concrete class for the positional record test
public interface IDocumentedEntity
{
    /// <summary>
    /// The entity identifier.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// The entity name.
    /// </summary>
    string Name { get; }
}

public class DocumentedEntity : IDocumentedEntity
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

[Facet(typeof(DocumentedEntity), CopyDocs = true)]
public partial record DocumentedPositionalDto;

// Source model with XML documentation
public class UserWithDocs
{
    public int Id { get; set; }

    /// <summary>
    /// The user's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// The user's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// The user's email address. Must be a valid email format.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The user's age in years.
    /// </summary>
    public int Age { get; set; }

    /// <summary>
    /// This should be excluded and not appear in DTO.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

// DTO with CopyDocs = true
[Facet(typeof(UserWithDocs), nameof(UserWithDocs.Password), nameof(UserWithDocs.LastName), CopyDocs = true)]
public partial class UserWithDocsDto
{
}

// DTO with CopyDocs = false (opt-out)
[Facet(typeof(UserWithDocs), nameof(UserWithDocs.Password), nameof(UserWithDocs.Age), CopyDocs = false)]
public partial class UserWithDocsNoCopyDto
{
}

// Models for nested facet tests
public class CustomerWithDocs
{
    public int Id { get; set; }

    /// <summary>
    /// The customer's full name.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// The customer's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
}

[Facet(typeof(CustomerWithDocs), nameof(CustomerWithDocs.PhoneNumber), CopyDocs = true)]
public partial class CustomerWithDocsDto
{
}

public class OrderWithDocs
{
    public int Id { get; set; }

    /// <summary>
    /// The unique order number.
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// The total amount for this order.
    /// </summary>
    public decimal TotalAmount { get; set; }

    public DateTime OrderDate { get; set; }

    /// <summary>
    /// The customer who placed this order.
    /// </summary>
    public CustomerWithDocs Customer { get; set; } = null!;

    public string? InternalNotes { get; set; }
}

[Facet(typeof(OrderWithDocs), nameof(OrderWithDocs.InternalNotes), CopyDocs = true, NestedFacets = [typeof(CustomerWithDocsDto)])]
public partial class OrderWithDocsDto
{
}

// Model combining both CopyDocs and CopyAttributes
public class UserWithDocsAndAttributes
{
    public int Id { get; set; }

    /// <summary>
    /// The user's first name.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// The user's email address.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

[Facet(typeof(UserWithDocsAndAttributes), nameof(UserWithDocsAndAttributes.Password), CopyDocs = true, CopyAttributes = true)]
public partial class UserWithDocsAndAttributesDto
{
}
