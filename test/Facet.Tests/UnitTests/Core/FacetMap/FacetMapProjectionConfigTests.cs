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

    [Fact]
    public void ToTarget_WithProjectionConfigAndNoAutoMatchedProperties_ShouldGenerate()
    {
        // Test case where NO properties auto-match, all mapping via IFacetProjectionMapConfiguration
        var entity = new WarehouseItemEntity
        {
            ItemId = 42,
            PackingLevel = new PackingLevelEntity { TotalLabels = 10 },
            ProductionLine = new ProductionLineEntity
            {
                MultiRangeIntakeRef = 100,
                TransferRef = 200
            }
        };

        var dto = entity.ToWarehouseItemDto();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(42, "Id should map from ItemId via config");
        dto.LabelCount.Should().Be(10, "LabelCount should map from PackingLevel.TotalLabels via config");
        dto.IntakeReference.Should().Be(100, "IntakeReference should map from ProductionLine.MultiRangeIntakeRef via config");
        dto.TransferReference.Should().Be(200, "TransferReference should map from ProductionLine.TransferRef via config");
    }

    [Fact]
    public void ToTarget_WithProjectionConfigAndNoAutoMatchedProperties_ShouldHandleNullProduction()
    {
        var entity = new WarehouseItemEntity
        {
            ItemId = 99,
            PackingLevel = new PackingLevelEntity { TotalLabels = 5 },
            ProductionLine = null
        };

        var dto = entity.ToWarehouseItemDto();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(99);
        dto.LabelCount.Should().Be(5);
        dto.IntakeReference.Should().BeNull("ProductionLine is null, so IntakeReference should be null");
        dto.TransferReference.Should().BeNull("ProductionLine is null, so TransferReference should be null");
    }
}
