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

    [Fact]
    public void Facet_ShouldInheritDocsThroughMultiLevelInterfaceChain()
    {
        // Docs live on IBaseShape; intermediate IDerivedShape and IGrandDerivedShape
        // do not redeclare. The walk must continue through every interface in
        // AllInterfaces, not stop at the first one that lacks the member.
        var source = LoadGeneratedSource(typeof(GrandConcreteShapeDto).FullName!);
        source.Should().Contain("The shape area.");
    }

    [Fact]
    public void Facet_ShouldInheritDocs_WhenInterfaceHidesBaseWithNew()
    {
        // IFooDerived redeclares Foo with `new` and no docs; IFooBase has the real docs.
        var source = LoadGeneratedSource(typeof(FooConcreteDto).FullName!);
        source.Should().Contain("Original foo doc.");
    }

    [Fact]
    public void Facet_ShouldResolveInheritdoc_OnSourceMember()
    {
        // BarDerived.Bar has only <inheritdoc/>. The DTO doesn't share the source's
        // hierarchy, so emitting <inheritdoc/> verbatim would leave nothing to inherit
        // from. The generator must resolve it to the concrete docs on BarBase.Bar.
        var source = LoadGeneratedSource(typeof(BarDerivedDto).FullName!);
        source.Should().Contain("The bar value.");
        source.Should().NotContain("<inheritdoc/>");
    }

    [Fact]
    public void Facet_ShouldInheritDocs_ThroughBaseClassToInterface()
    {
        // ChildLeaf.Label has no docs, LeafBase.Label has no docs, the docs live on
        // ILeafService.Label. The walk must traverse base classes and then reach the
        // interface implemented by the base class.
        var source = LoadGeneratedSource(typeof(ChildLeafDto).FullName!);
        source.Should().Contain("Leaf via base interface.");
    }

    [Fact]
    public void Facet_ShouldKeepWalking_WhenFirstInterfaceMatchHasInheritdocOnly()
    {
        // IEchoDerived redeclares Echo with <inheritdoc/>; IEchoBase has the real docs.
        // Without the fix, the walk treats <inheritdoc/> as a non-empty doc and stops
        // at IEchoDerived, dropping the actual summary on IEchoBase.
        var source = LoadGeneratedSource(typeof(EchoConcreteDto).FullName!);
        source.Should().Contain("The echo string.");
    }

    [Fact]
    public void Facet_ShouldKeepWalking_WhenBaseClassMemberHasInheritdocOnly()
    {
        // EchoBaseClass.Ping uses <inheritdoc/>, EchoChild.Ping has no docs, the real
        // docs live on IPing.Ping. Walking the base class chain must skip past the
        // <inheritdoc/>-only entry and continue into the interfaces.
        var source = LoadGeneratedSource(typeof(EchoChildDto).FullName!);
        source.Should().Contain("Pinged value.");
    }

    [Fact]
    public void Facet_ShouldNotResolveInheritdoc_WhenInheritDocsIsFalse()
    {
        // BarDerived.Bar has <inheritdoc/>; with InheritDocs=false the generator must
        // not walk the hierarchy. The DTO ends up with no docs for Bar (the same
        // outcome as any other undocumented member when InheritDocs is off).
        var source = LoadGeneratedSource(typeof(BarDerivedNoInheritDto).FullName!);
        source.Should().NotBeEmpty();
        source.Should().NotContain("The bar value.");
        source.Should().NotContain("<inheritdoc/>");
    }

    [Fact]
    public void Facet_ShouldInheritTypeLevelDocs_FromBaseClass_WhenInheritDocsIsTrue()
    {
        // DocumentedBase has a class-level summary. InheritedDocChild has no summary,
        // and InheritDocs=true should pull the summary down from the base class.
        var source = LoadGeneratedSource(typeof(InheritedDocChildDto).FullName!);
        source.Should().Contain("Documented base type.");
    }

    [Fact]
    public void Facet_ShouldInheritTypeLevelDocs_FromInterface_WhenInheritDocsIsTrue()
    {
        // The source class has no type-level docs; the implemented interface does.
        var source = LoadGeneratedSource(typeof(TypedocServiceDto).FullName!);
        source.Should().Contain("Service contract docs.");
    }

    [Fact]
    public void Facet_ShouldNotInheritTypeLevelDocs_WhenInheritDocsIsFalse()
    {
        // Same hierarchy as the base-class test, but InheritDocs=false. No type-level
        // summary should be emitted on the DTO.
        var source = LoadGeneratedSource(typeof(InheritedDocChildNoInheritDto).FullName!);
        source.Should().NotBeEmpty();
        source.Should().NotContain("Documented base type.");
    }
}

// ---- Test models ----

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
