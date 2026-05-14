using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

public class GenerateDtosEnumConversionTests
{
    [Fact]
    public void GenerateDtos_ConvertEnumsToString_ShouldConvertEnumTypesAndValues()
    {
        var source = new TestOrderStringEnumConversion
        {
            Id = 1,
            Status = OrderStatus.Processing,
            PreviousStatus = OrderStatus.Pending
        };

        var dto = new TestOrderStringEnumConversionResponse(source);

        dto.Status.Should().Be("Processing");
        dto.PreviousStatus.Should().Be("Pending");
    }

    [Fact]
    public void GenerateDtos_ConvertEnumsToString_ToSource_ShouldConvertBack()
    {
        var dto = new TestOrderStringEnumConversionResponse
        {
            Id = 2,
            Status = "Delivered",
            PreviousStatus = "Shipped"
        };

        var source = dto.ToSource();

        source.Status.Should().Be(OrderStatus.Delivered);
        source.PreviousStatus.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public void GenerateDtos_ConvertEnumsToInt_ShouldConvertEnumTypesAndValues()
    {
        var source = new TestOrderIntEnumConversion
        {
            Id = 3,
            Status = OrderStatus.Cancelled,
            PreviousStatus = OrderStatus.Processing
        };

        var dto = new TestOrderIntEnumConversionResponse(source);

        dto.Status.Should().Be((int)OrderStatus.Cancelled);
        dto.PreviousStatus.Should().Be((int)OrderStatus.Processing);
    }

    [Fact]
    public void GenerateDtos_ConvertEnumsToInt_ToSource_ShouldConvertBack()
    {
        var dto = new TestOrderIntEnumConversionResponse
        {
            Id = 4,
            Status = (int)OrderStatus.Pending,
            PreviousStatus = (int)OrderStatus.Delivered
        };

        var source = dto.ToSource();

        source.Status.Should().Be(OrderStatus.Pending);
        source.PreviousStatus.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void GenerateDtos_ConvertEnumsTo_ShouldSetFacetAttributeConvertEnumsTo()
    {
        var stringFacetAttribute = (FacetAttribute)typeof(TestOrderStringEnumConversionResponse)
            .GetCustomAttributes(typeof(FacetAttribute), false)
            .Single();
        var intFacetAttribute = (FacetAttribute)typeof(TestOrderIntEnumConversionResponse)
            .GetCustomAttributes(typeof(FacetAttribute), false)
            .Single();

        stringFacetAttribute.ConvertEnumsTo.Should().Be(typeof(string));
        intFacetAttribute.ConvertEnumsTo.Should().Be(typeof(int));
    }
}
