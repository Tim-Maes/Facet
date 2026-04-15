using System.Linq.Expressions;

namespace Facet.Tests.UnitTests.Core.Facet;

public class EmployeeEntity332
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public int HoursWorked { get; set; }
}

/// <summary>
/// Configuration that ONLY implements IFacetProjectionMapConfiguration (no IFacetMapConfiguration).
/// The generator should compile these expressions into a cached Action for the constructor.
/// </summary>
public class EmployeeDto332MapConfig
    : IFacetProjectionMapConfiguration<EmployeeEntity332, EmployeeDto332>
{
    public static void ConfigureProjection(IFacetProjectionBuilder<EmployeeEntity332, EmployeeDto332> builder)
    {
        builder.Map(d => d.FullName, s => s.FirstName + " " + s.LastName);
        builder.Map(d => d.TotalPay, s => s.HourlyRate * s.HoursWorked);
    }
}

[Facet(typeof(EmployeeEntity332), Configuration = typeof(EmployeeDto332MapConfig), GenerateProjection = true)]
public partial class EmployeeDto332
{
    public string FullName { get; set; } = string.Empty;
    public decimal TotalPay { get; set; }
}

/// <summary>
/// Tests for projection-only configuration reuse: when a config class only implements
/// IFacetProjectionMapConfiguration, the constructor compiles and invokes those expressions.
/// </summary>
public class ProjectionOnlyMapConfigTests
{
    [Fact]
    public void Constructor_ShouldApplyProjectionExpressions()
    {
        var source = new EmployeeEntity332
        {
            Id = 1,
            FirstName = "Jane",
            LastName = "Doe",
            HourlyRate = 50m,
            HoursWorked = 40
        };

        var dto = new EmployeeDto332(source);

        dto.Id.Should().Be(1);
        dto.FullName.Should().Be("Jane Doe", "ConfigureProjection expressions should be compiled and applied in the constructor");
        dto.TotalPay.Should().Be(2000m, "ConfigureProjection expressions should be compiled and applied in the constructor");
    }

    [Fact]
    public void FromSource_ShouldApplyProjectionExpressions()
    {
        var source = new EmployeeEntity332
        {
            Id = 2,
            FirstName = "John",
            LastName = "Smith",
            HourlyRate = 75m,
            HoursWorked = 30
        };

        var dto = EmployeeDto332.FromSource(source);

        dto.Id.Should().Be(2);
        dto.FullName.Should().Be("John Smith");
        dto.TotalPay.Should().Be(2250m);
    }

    [Fact]
    public void Projection_ShouldWorkForEfCoreQueries()
    {
        var source = new EmployeeEntity332
        {
            Id = 3,
            FirstName = "Alice",
            LastName = "Walker",
            HourlyRate = 100m,
            HoursWorked = 20
        };

        var compiled = EmployeeDto332.Projection.Compile();
        var dto = compiled(source);

        dto.Id.Should().Be(3);
        dto.FullName.Should().Be("Alice Walker");
        dto.TotalPay.Should().Be(2000m);
    }

    [Fact]
    public void Projection_ShouldReturnPureMemberInitExpression()
    {
        var expr = EmployeeDto332.Projection;

        expr.Body.NodeType.Should().Be(ExpressionType.MemberInit,
            "the Projection body must be a MemberInitExpression so EF Core can translate it");
    }

    [Fact]
    public void Constructor_And_Projection_ShouldProduceSameResults()
    {
        var source = new EmployeeEntity332
        {
            Id = 4,
            FirstName = "Bob",
            LastName = "Builder",
            HourlyRate = 60m,
            HoursWorked = 35
        };

        var fromCtor = new EmployeeDto332(source);
        var fromProjection = EmployeeDto332.Projection.Compile()(source);

        fromCtor.Id.Should().Be(fromProjection.Id);
        fromCtor.FullName.Should().Be(fromProjection.FullName);
        fromCtor.TotalPay.Should().Be(fromProjection.TotalPay);
    }
}
