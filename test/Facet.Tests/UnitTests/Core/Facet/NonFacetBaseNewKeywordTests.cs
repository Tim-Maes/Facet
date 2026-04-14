using Facet.Tests.TestModels.NonFacetBaseNewKeyword;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests that when a Facet DTO inherits from a non-Facet base class,
/// the generator does not emit unnecessary 'new' modifiers on
/// FromSource, Projection, ToSource, or BackTo (which would cause CS0109).
/// </summary>
public class NonFacetBaseNewKeywordTests
{
    [Fact]
    public void Facet_ShouldNotEmitNew_WhenBaseClassIsNotFacet()
    {
        // Arrange
        var source = new OrderLineBaseEntity
        {
            Id = 1,
            Number = "ORD-001",
            Status = 2,
            UpdatedBy = "admin",
            ModifiedByUnitId = 10,
            ModifiedByUserId = 20
        };

        // Act
        var dto = OrderLineBaseDto312.FromSource(source);

        // Assert
        dto.Should().NotBeNull();
        dto.Number.Should().Be("ORD-001");
        dto.Status.Should().Be(2);
    }

    [Fact]
    public void Facet_ShouldNotEmitNew_ToSource_WhenBaseClassIsNotFacet()
    {
        var source = new OrderLineBaseEntity
        {
            Number = "ORD-002",
            Status = 3
        };

        var dto = new OrderLineBaseDto312(source);
        var roundTripped = dto.ToSource();

        roundTripped.Should().NotBeNull();
        roundTripped.Number.Should().Be("ORD-002");
        roundTripped.Status.Should().Be(3);
    }
}
