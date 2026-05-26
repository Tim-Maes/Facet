namespace Facet.Tests.UnitTests.Core.Facet;

public class DataExampleEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;

    public int? StringResourceId { get; set; }
    public virtual StringResourceEntity? StringResource { get; set; }

    public int? ExtendedDataId { get; set; }
    public virtual ExtendedDataEntity? ExtendedData { get; set; }
}

public class StringResourceEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ExtendedDataEntity
{
    public int Id { get; set; }
    public string Metadata { get; set; } = string.Empty;
}

[Facet(typeof(StringResourceEntity))]
public partial class StringResourceDto;

[Facet(typeof(ExtendedDataEntity))]
public partial class ExtendedDto;

[Facet(
    typeof(DataExampleEntity),
    NestedFacets = [typeof(StringResourceDto), typeof(ExtendedDto)])]
public partial class DataExampleFacet;

public class NullableForeignKeyTests
{
    [Fact]
    public void Projection_ShouldHandleNullNavigationProperty_WhenForeignKeyIsNull()
    {
        var dataExamples = new[]
        {
            new DataExampleEntity
            {
                Id = 1,
                Code = "TEST001",
                StringResourceId = null,
                StringResource = null!,
                ExtendedDataId = null,
                ExtendedData = null!
            },
            new DataExampleEntity
            {
                Id = 2,
                Code = "TEST002",
                StringResourceId = 100,
                StringResource = new StringResourceEntity { Id = 100, Name = "Resource 1" },
                ExtendedDataId = 200,
                ExtendedData = new ExtendedDataEntity { Id = 200, Metadata = "Metadata 1" }
            }
        }.AsQueryable();

        var dtos = dataExamples.Select(DataExampleFacet.Projection).ToList();

        dtos.Should().HaveCount(2);

        dtos[0].Id.Should().Be(1);
        dtos[0].Code.Should().Be("TEST001");
        dtos[0].StringResource.Should().BeNull();
        dtos[0].ExtendedData.Should().BeNull();

        dtos[1].Id.Should().Be(2);
        dtos[1].Code.Should().Be("TEST002");
        dtos[1].StringResource.Should().NotBeNull();
        dtos[1].StringResource!.Id.Should().Be(100);
        dtos[1].ExtendedData.Should().NotBeNull();
        dtos[1].ExtendedData!.Id.Should().Be(200);
    }

    [Fact]
    public void Constructor_ShouldHandleNullNavigationProperty_WhenForeignKeyIsNull()
    {
        var dataExample = new DataExampleEntity
        {
            Id = 1,
            Code = "TEST001",
            StringResourceId = null,
            StringResource = null!,
            ExtendedDataId = null,
            ExtendedData = null!
        };

        var dto = new DataExampleFacet(dataExample);

        dto.Id.Should().Be(1);
        dto.StringResource.Should().BeNull();
        dto.ExtendedData.Should().BeNull();
    }
}
