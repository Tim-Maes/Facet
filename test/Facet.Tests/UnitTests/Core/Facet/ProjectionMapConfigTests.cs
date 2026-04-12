using System.Linq.Expressions;

namespace Facet.Tests.UnitTests.Core.Facet;

public class PersonEntity325
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class PersonDto325MapConfig
    : IFacetMapConfiguration<PersonEntity325, PersonDto325>,
      IFacetProjectionMapConfiguration<PersonEntity325, PersonDto325>
{
    // Imperative mapping
    public static void Map(PersonEntity325 source, PersonDto325 target)
    {
        target.FullName = source.FirstName + " " + source.LastName;
        target.AuditNote = "SET_BY_SERVICE"; // simulates DI-dependent work
    }

    // Expression mapping
    public static void ConfigureProjection(IFacetProjectionBuilder<PersonEntity325, PersonDto325> builder)
    {
        builder.Map(d => d.FullName, s => s.FirstName + " " + s.LastName);
    }
}

[Facet(typeof(PersonEntity325), Configuration = typeof(PersonDto325MapConfig), GenerateProjection = true)]
public partial class PersonDto325
{
    /// <summary>Computed by PersonDto325MapConfig.Map() and ConfigureProjection().</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Set only by Map() (DI-dependent); absent from Projection.</summary>
    public string AuditNote { get; set; } = string.Empty;
}

/// <summary>
/// Tests for IFacetProjectionMapConfiguration — lazy projection with inlined expression bindings.
/// </summary>
public class ProjectionMapConfigTests
{
    [Fact]
    public void Constructor_ShouldSetBothMappedProperties()
    {
        var source = new PersonEntity325 { Id = 1, FirstName = "Jane", LastName = "Smith" };

        var dto = new PersonDto325(source);

        dto.Id.Should().Be(1);
        dto.FullName.Should().Be("Jane Smith", "Map() should concatenate first + last name");
        dto.AuditNote.Should().Be("SET_BY_SERVICE", "Map() should have set the DI-dependent property");
    }

    [Fact]
    public void FromSource_ShouldSetBothMappedProperties()
    {
        var source = new PersonEntity325 { Id = 2, FirstName = "John", LastName = "Doe" };

        var dto = PersonDto325.FromSource(source);

        dto.FullName.Should().Be("John Doe");
        dto.AuditNote.Should().Be("SET_BY_SERVICE");
    }

    [Fact]
    public void Projection_ShouldIncludeComputedFullName()
    {
        var source = new PersonEntity325 { Id = 3, FirstName = "Alice", LastName = "Walker" };

        var compiled = PersonDto325.Projection.Compile();
        var dto = compiled(source);

        dto.Id.Should().Be(3);
        dto.FullName.Should().Be("Alice Walker",
            "ConfigureProjection should inline the FullName expression into the Projection");
    }

    [Fact]
    public void Projection_ShouldNotIncludeDiDependentProperty()
    {
        var source = new PersonEntity325 { Id = 4, FirstName = "Bob", LastName = "Builder" };

        var compiled = PersonDto325.Projection.Compile();
        var dto = compiled(source);

        dto.AuditNote.Should().BeNullOrEmpty(
            "AuditNote was intentionally omitted from ConfigureProjection and should not be set");
    }

    [Fact]
    public void Projection_ShouldReturnPureMemberInitExpression()
    {
        // Verifies the expression tree contains no Invoke nodes
        var expr = PersonDto325.Projection;

        expr.Body.NodeType.Should().Be(ExpressionType.MemberInit,
            "the Projection body must be a MemberInitExpression so EF Core can translate it");
    }

    [Fact]
    public void Projection_ShouldBeLazyAndReturnSameInstance()
    {
        // LazyInitializer guarantees at-most-one build; same reference on repeated access.
        var first = PersonDto325.Projection;
        var second = PersonDto325.Projection;

        first.Should().BeSameAs(second, "the lazy backing field should return the same expression instance");
    }
}

