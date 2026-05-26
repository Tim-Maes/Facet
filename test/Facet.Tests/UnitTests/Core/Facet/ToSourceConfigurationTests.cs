using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class ToSourceConfigurationTests
{
    [Fact]
    public void ToSource_ShouldCallToSourceConfiguration_WhenConfigured()
    {
        var entity = new JsonStoredEntity
        {
            Id = 1,
            Name = "Test Order",
            MetadataJson = "{\"tag\":\"urgent\",\"priority\":5}"
        };

        var dto = new OrderDto(entity);

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Test Order");
        dto.Metadata.Should().NotBeNull();
        dto.Metadata!.Tag.Should().Be("urgent");
        dto.Metadata.Priority.Should().Be(5);

        var result = dto.ToSource();

        result.Id.Should().Be(1);
        result.Name.Should().Be("Test Order");
        result.MetadataJson.Should().Be("{\"tag\":\"urgent\",\"priority\":5}");
    }

    [Fact]
    public void ToSource_ShouldSerialiseUpdatedMetadata_WhenMetadataIsChanged()
    {
        var entity = new JsonStoredEntity { Id = 42, Name = "Updated Order", MetadataJson = "{\"tag\":\"low\",\"priority\":1}" };
        var dto = new OrderDto(entity);

        dto.Metadata = new OrderMetadata { Tag = "high", Priority = 9 };

        var result = dto.ToSource();

        result.MetadataJson.Should().Be("{\"tag\":\"high\",\"priority\":9}");
    }

    [Fact]
    public void ToSource_ShouldWriteDefaultJson_WhenMetadataIsNull()
    {
        var dto = new OrderDto
        {
            Id = 99,
            Name = "Empty Order",
            Metadata = null
        };

        var result = dto.ToSource();

        result.MetadataJson.Should().Be("{}");
    }

    [Fact]
    public void ToSource_ShouldPreserveSimpleProperties_AlongsideCustomConfiguration()
    {
        var entity = new JsonStoredEntity { Id = 7, Name = "Simple", MetadataJson = "{\"tag\":\"test\",\"priority\":3}" };
        var dto = new OrderDto(entity);

        var result = dto.ToSource();

        result.Id.Should().Be(7);
        result.Name.Should().Be("Simple");
        
        result.MetadataJson.Should().Be("{\"tag\":\"test\",\"priority\":3}");
    }
}
