using Facet.Tests.TestModels.FacetMap;

namespace Facet.Tests.UnitTests.Core.FacetMap;

/// <summary>
/// Tests for FacetMap with IFacetProjectionMapConfiguration support.
/// </summary>
public class FacetMapProjectionConfigTests
{
    [Fact]
    public void ToTarget_WithProjectionConfig_ShouldApplyMappings()
    {
        var entity = new InvoiceEntity
        {
            Id = 1,
            Number = "INV-001",
            SubTotal = 100m,
            Tax = 25m
        };

        var dto = entity.ToInvoiceDto();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Number.Should().Be("INV-001");
        dto.SubTotal.Should().Be(100m);
        dto.Tax.Should().Be(25m);
        dto.Total.Should().Be(125m, "Total should be computed from SubTotal + Tax via projection config");
    }

    [Fact]
    public void ToTarget_WithProjectionConfig_ShouldHandleZeroValues()
    {
        var entity = new InvoiceEntity
        {
            Id = 2,
            Number = "INV-002",
            SubTotal = 0m,
            Tax = 0m
        };

        var dto = entity.ToInvoiceDto();

        dto.Should().NotBeNull();
        dto.Total.Should().Be(0m);
    }
}
