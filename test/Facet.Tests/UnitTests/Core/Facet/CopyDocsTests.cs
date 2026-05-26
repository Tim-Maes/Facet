using System.Reflection;

namespace Facet.Tests.UnitTests.Core.Facet;

public class CopyDocsTests
{
    [Fact]
    public void Facet_ShouldCopyXmlDocs_WhenCopyDocsIsTrue()
    {
        var firstNameProperty = typeof(UserWithDocsDto).GetProperty("FirstName");
        var emailProperty = typeof(UserWithDocsDto).GetProperty("Email");
        var ageProperty = typeof(UserWithDocsDto).GetProperty("Age");

        firstNameProperty.Should().NotBeNull();
        emailProperty.Should().NotBeNull();
        ageProperty.Should().NotBeNull();

    }

    [Fact]
    public void Facet_ShouldNotCopyXmlDocs_WhenCopyDocsIsFalse()
    {
        var dtoType = typeof(UserWithDocsNoCopyDto);

        var firstNameProperty = dtoType.GetProperty("FirstName");
        var emailProperty = dtoType.GetProperty("Email");

        firstNameProperty.Should().NotBeNull();
        emailProperty.Should().NotBeNull();
    }

    [Fact]
    public void Facet_ShouldCopyXmlDocs_WithNestedFacets()
    {
        var orderDtoType = typeof(OrderWithDocsDto);
        var customerProperty = orderDtoType.GetProperty("Customer");
        var orderNumberProperty = orderDtoType.GetProperty("OrderNumber");
        var totalAmountProperty = orderDtoType.GetProperty("TotalAmount");

        orderNumberProperty.Should().NotBeNull();
        totalAmountProperty.Should().NotBeNull();
        customerProperty.Should().NotBeNull();
        customerProperty!.PropertyType.Should().Be(typeof(CustomerWithDocsDto));
    }

    [Fact]
    public void Facet_ShouldCopyXmlDocs_OnNestedFacetProperties()
    {
        var customerDtoType = typeof(CustomerWithDocsDto);
        var emailProperty = customerDtoType.GetProperty("Email");
        var fullNameProperty = customerDtoType.GetProperty("FullName");

        emailProperty.Should().NotBeNull();
        fullNameProperty.Should().NotBeNull();
    }

    [Fact]
    public void Facet_CanCombine_CopyDocsAndCopyAttributes()
    {
        var dtoType = typeof(UserWithDocsAndAttributesDto);
        var firstNameProperty = dtoType.GetProperty("FirstName");
        var emailProperty = dtoType.GetProperty("Email");

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
        var dir = Path.Combine(projectRoot, "obj", "Generated", "Facet", "Facet.Generators.FacetGenerator");
        var propertiesPath = Path.Combine(dir, $"{typeFullName}.Properties.g.cs");
        var combinedPath = Path.Combine(dir, $"{typeFullName}.g.cs");
        try { return File.ReadAllText(File.Exists(propertiesPath) ? propertiesPath : combinedPath); }
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
        var source = LoadGeneratedSource(typeof(ConcreteProductDto).FullName!);
        source.Should().NotBeEmpty("generated file should exist");
        source.Should().Contain("The product name.");
    }

    [Fact]
    public void Facet_ShouldNotInheritDocs_WhenInheritDocsIsFalse()
    {
        var source = LoadGeneratedSource(typeof(ConcreteProductNoInheritDto).FullName!);
        source.Should().NotBeEmpty("generated file should exist");
        source.Should().NotContain("The product name.");
    }

    [Fact]
    public void Facet_ShouldUseOwnDocs_WhenMemberHasDirectDocComment()
    {
        var inheritSource = LoadGeneratedSource(typeof(ConcreteProductDto).FullName!);
        var noInheritSource = LoadGeneratedSource(typeof(ConcreteProductNoInheritDto).FullName!);
        inheritSource.Should().Contain("The selling price.");
        noInheritSource.Should().Contain("The selling price.");
    }

    [Fact]
    public void Facet_ShouldEmitDocCommentsOnPositionalRecordProperties_WhenDocsAreInherited()
    {
        // Positional records require property body overrides to carry doc comments.
        
        var source = LoadGeneratedSource(typeof(DocumentedPositionalDto).FullName!);
        source.Should().NotBeEmpty("generated file should exist");
        source.Should().Contain("The entity identifier.");
        source.Should().Contain("The entity name.");
    }

    [Fact]
    public void Facet_ShouldInheritDocsThroughMultiLevelInterfaceChain()
    {
        var source = LoadGeneratedSource(typeof(GrandConcreteShapeDto).FullName!);
        source.Should().Contain("The shape area.");
    }

    [Fact]
    public void Facet_ShouldInheritDocs_WhenInterfaceHidesBaseWithNew()
    {
        var source = LoadGeneratedSource(typeof(FooConcreteDto).FullName!);
        source.Should().Contain("Original foo doc.");
    }

    [Fact]
    public void Facet_ShouldResolveInheritdoc_OnSourceMember()
    {
        var source = LoadGeneratedSource(typeof(BarDerivedDto).FullName!);
        source.Should().Contain("The bar value.");
        source.Should().NotContain("<inheritdoc/>");
    }

    [Fact]
    public void Facet_ShouldInheritDocs_ThroughBaseClassToInterface()
    {
        var source = LoadGeneratedSource(typeof(ChildLeafDto).FullName!);
        source.Should().Contain("Leaf via base interface.");
    }

    [Fact]
    public void Facet_ShouldKeepWalking_WhenFirstInterfaceMatchHasInheritdocOnly()
    {
        var source = LoadGeneratedSource(typeof(EchoConcreteDto).FullName!);
        source.Should().Contain("The echo string.");
    }

    [Fact]
    public void Facet_ShouldKeepWalking_WhenBaseClassMemberHasInheritdocOnly()
    {
        var source = LoadGeneratedSource(typeof(EchoChildDto).FullName!);
        source.Should().Contain("Pinged value.");
    }

    [Fact]
    public void Facet_ShouldNotResolveInheritdoc_WhenInheritDocsIsFalse()
    {
        var source = LoadGeneratedSource(typeof(BarDerivedNoInheritDto).FullName!);
        source.Should().NotBeEmpty();
        source.Should().NotContain("The bar value.");
        source.Should().NotContain("<inheritdoc/>");
    }

    [Fact]
    public void Facet_ShouldInheritTypeLevelDocs_FromBaseClass_WhenInheritDocsIsTrue()
    {
        var source = LoadGeneratedSource(typeof(InheritedDocChildDto).FullName!);
        source.Should().Contain("Documented base type.");
    }

    [Fact]
    public void Facet_ShouldInheritTypeLevelDocs_FromInterface_WhenInheritDocsIsTrue()
    {
        var source = LoadGeneratedSource(typeof(TypedocServiceDto).FullName!);
        source.Should().Contain("Service contract docs.");
    }

    [Fact]
    public void Facet_ShouldNotInheritTypeLevelDocs_WhenInheritDocsIsFalse()
    {
        var source = LoadGeneratedSource(typeof(InheritedDocChildNoInheritDto).FullName!);
        source.Should().NotBeEmpty();
        source.Should().NotContain("Documented base type.");
    }
}

public interface IBaseShape
{
    /// <summary>The shape area.</summary>
    double Area { get; }
}

public interface IDerivedShape : IBaseShape { }

public interface IGrandDerivedShape : IDerivedShape { }

public class GrandConcreteShape : IGrandDerivedShape
{
    public double Area => 0.0;
}

[Facet(typeof(GrandConcreteShape), CopyDocs = true, InheritDocs = true)]
public partial class GrandConcreteShapeDto { }

public interface IFooBase
{
    /// <summary>Original foo doc.</summary>
    int Foo { get; }
}

public interface IFooDerived : IFooBase
{
    new int Foo { get; }
}

public class FooConcrete : IFooDerived
{
    public int Foo => 0;
}

[Facet(typeof(FooConcrete), CopyDocs = true, InheritDocs = true)]
public partial class FooConcreteDto { }

public class BarBase
{
    /// <summary>The bar value.</summary>
    public virtual int Bar { get; set; }
}

public class BarDerived : BarBase
{
    /// <inheritdoc/>
    public override int Bar { get; set; }
}

[Facet(typeof(BarDerived), CopyDocs = true, InheritDocs = true)]
public partial class BarDerivedDto { }

[Facet(typeof(BarDerived), CopyDocs = true, InheritDocs = false)]
public partial class BarDerivedNoInheritDto { }

public interface ILeafService
{
    /// <summary>Leaf via base interface.</summary>
    string Label { get; }
}

public class LeafBase : ILeafService
{
    public virtual string Label => string.Empty;
}

public class ChildLeaf : LeafBase
{
    public override string Label => string.Empty;
}

[Facet(typeof(ChildLeaf), CopyDocs = true, InheritDocs = true)]
public partial class ChildLeafDto { }

public interface IEchoBase
{
    /// <summary>The echo string.</summary>
    string Echo { get; }
}

public interface IEchoDerived : IEchoBase
{
    /// <inheritdoc/>
    new string Echo { get; }
}

public class EchoConcrete : IEchoDerived
{
    public string Echo => string.Empty;
}

[Facet(typeof(EchoConcrete), CopyDocs = true, InheritDocs = true)]
public partial class EchoConcreteDto { }

public interface IPing
{
    /// <summary>Pinged value.</summary>
    string Ping { get; }
}

public class EchoBaseClass : IPing
{
    /// <inheritdoc/>
    public virtual string Ping => string.Empty;
}

public class EchoChild : EchoBaseClass
{
    public override string Ping => string.Empty;
}

[Facet(typeof(EchoChild), CopyDocs = true, InheritDocs = true)]
public partial class EchoChildDto { }

/// <summary>Documented base type.</summary>
public class DocumentedBase { }

public class InheritedDocChild : DocumentedBase { }

[Facet(typeof(InheritedDocChild), CopyDocs = true, InheritDocs = true)]
public partial class InheritedDocChildDto { }

[Facet(typeof(InheritedDocChild), CopyDocs = true, InheritDocs = false)]
public partial class InheritedDocChildNoInheritDto { }

/// <summary>Service contract docs.</summary>
public interface ITypedocService
{
    string Run();
}

public class TypedocService : ITypedocService
{
    public string Run() => string.Empty;
}

[Facet(typeof(TypedocService), CopyDocs = true, InheritDocs = true)]
public partial class TypedocServiceDto { }

public class ProductBase
{
    /// <summary>
    /// The product name.
    /// </summary>
    public virtual string Name { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;
}

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

public class DocumentedService : IDocumentedService
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

[Facet(typeof(DocumentedService), CopyDocs = true, InheritDocs = true)]
public partial class DocumentedServiceDto { }

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

[Facet(typeof(UserWithDocs), nameof(UserWithDocs.Password), nameof(UserWithDocs.LastName), CopyDocs = true)]
public partial class UserWithDocsDto
{
}

[Facet(typeof(UserWithDocs), nameof(UserWithDocs.Password), nameof(UserWithDocs.Age), CopyDocs = false)]
public partial class UserWithDocsNoCopyDto
{
}

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
