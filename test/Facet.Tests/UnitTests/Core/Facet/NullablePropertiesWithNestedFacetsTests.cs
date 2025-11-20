namespace Facet.Tests.UnitTests.Core.Facet;

// Test entities that mimic EF Core entities with navigation properties
public class ChunkEmbedding1024
{
    public int Id { get; set; }
    public int ChunkIdFk { get; set; }
    public int ModelIdFk { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();

    public virtual Chunk ChunkIdFkNavigation { get; set; } = null!;
    public virtual EmbeddingModel ModelIdFkNavigation { get; set; } = null!;
}

public class Chunk
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public int DocumentId { get; set; }
}

public class EmbeddingModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Dimensions { get; set; }
}

// Test entity with nullable nested navigation property
public class NullableWorkerEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NullableCompanyEntity? Company { get; set; }
}

public class NullableCompanyEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

// Test entity for collection nested facets with NullableProperties
public class NullableOrganizationEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<NullableEmployeeEntity> Employees { get; set; } = new();
}

public class NullableEmployeeEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

// Facet DTOs - see GitHub issue #116
[Facet(typeof(Chunk))]
public partial class ChunkDto;

[Facet(typeof(EmbeddingModel))]
public partial class EmbeddingModelDto;

[Facet(
    typeof(ChunkEmbedding1024),
    exclude: [nameof(ChunkEmbedding1024.ModelIdFkNavigation)],
    NestedFacets = [typeof(ChunkDto), typeof(EmbeddingModelDto)],
    NullableProperties = true)]
public partial class ChunkEmbedding1024Dto;

// Test facets for nested facets with nullable properties
[Facet(typeof(NullableCompanyEntity), NullableProperties = true)]
public partial class NullableCompanyFacet;

[Facet(
    typeof(NullableWorkerEntity),
    NestedFacets = [typeof(NullableCompanyFacet)],
    NullableProperties = true)]
public partial class NullableWorkerFacet;

// Test facets for collection nested facets with nullable properties
[Facet(typeof(NullableEmployeeEntity), NullableProperties = true)]
public partial class NullableEmployeeFacet;

[Facet(
    typeof(NullableOrganizationEntity),
    NestedFacets = [typeof(NullableEmployeeFacet)],
    NullableProperties = true)]
public partial class NullableOrganizationFacet;

public class NullablePropertiesWithNestedFacetsTests
{
    [Fact]
    public void NestedFacet_ShouldBeNullable_WhenNullablePropertiesIsTrue()
    {
        // Arrange & Act
        var dtoType = typeof(NullableWorkerFacet);

        // Assert
        var idProp = dtoType.GetProperty("Id");
        idProp.Should().NotBeNull();
        idProp!.PropertyType.Should().Be(typeof(int?), "Id should be nullable int");

        var nameProp = dtoType.GetProperty("Name");
        nameProp.Should().NotBeNull();
        nameProp!.PropertyType.Should().Be(typeof(string), "Name is a reference type");

        var companyProp = dtoType.GetProperty("Company");
        companyProp.Should().NotBeNull();

        companyProp!.PropertyType.Should().Be(typeof(NullableCompanyFacet),
            "Company nested facet should be nullable reference type (NullableCompanyFacet?)");
    }

    [Fact]
    public void ChunkEmbedding1024Dto_ShouldHaveNullableNestedFacets_WhenNullablePropertiesIsTrue()
    {
        // Arrange & Act
        var dtoType = typeof(ChunkEmbedding1024Dto);

        // Assert - All properties should be nullable
        var idProp = dtoType.GetProperty("Id");
        idProp.Should().NotBeNull();
        idProp!.PropertyType.Should().Be(typeof(int?), "Id should be nullable int");

        var chunkIdFkProp = dtoType.GetProperty("ChunkIdFk");
        chunkIdFkProp.Should().NotBeNull();
        chunkIdFkProp!.PropertyType.Should().Be(typeof(int?), "ChunkIdFk should be nullable int");

        var modelIdFkProp = dtoType.GetProperty("ModelIdFk");
        modelIdFkProp.Should().NotBeNull();
        modelIdFkProp!.PropertyType.Should().Be(typeof(int?), "ModelIdFk should be nullable int");

        var chunkNavProp = dtoType.GetProperty("ChunkIdFkNavigation");
        chunkNavProp.Should().NotBeNull();
        chunkNavProp!.PropertyType.Should().Be(typeof(ChunkDto),
            "ChunkIdFkNavigation nested facet should be nullable (ChunkDto?)");
    }

    [Fact]
    public void CollectionNestedFacet_ShouldBeNullable_WhenNullablePropertiesIsTrue()
    {
        // Arrange & Act
        var dtoType = typeof(NullableOrganizationFacet);

        // Assert
        var idProp = dtoType.GetProperty("Id");
        idProp.Should().NotBeNull();
        idProp!.PropertyType.Should().Be(typeof(int?), "Id should be nullable int");

        var employeesProp = dtoType.GetProperty("Employees");
        employeesProp.Should().NotBeNull();
        employeesProp!.PropertyType.Should().Be(typeof(List<NullableEmployeeFacet>),
            "Employees collection should be nullable (List<NullableEmployeeFacet>?)");
    }

    [Fact]
    public void Constructor_ShouldHandleNullNestedFacet_WithNullableProperties()
    {
        // Arrange
        var worker = new NullableWorkerEntity
        {
            Id = 1,
            Name = "John Doe",
            Company = null
        };

        // Act
        var dto = new NullableWorkerFacet(worker);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("John Doe");
        dto.Company.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapNonNullNestedFacet_WithNullableProperties()
    {
        // Arrange
        var worker = new NullableWorkerEntity
        {
            Id = 2,
            Name = "Jane Smith",
            Company = new NullableCompanyEntity
            {
                Id = 100,
                Name = "Acme Corp",
                Address = "123 Main St"
            }
        };

        // Act
        var dto = new NullableWorkerFacet(worker);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(2);
        dto.Name.Should().Be("Jane Smith");
        dto.Company.Should().NotBeNull();
        dto.Company!.Id.Should().Be(100);
        dto.Company.Name.Should().Be("Acme Corp");
        dto.Company.Address.Should().Be("123 Main St");
    }

    [Fact]
    public void ToSource_ShouldHandleNullableProperties_WithoutCompilationErrors()
    {
        // Arrange
        var dto = new ChunkEmbedding1024Dto
        {
            Id = 1,
            ChunkIdFk = 10,
            ModelIdFk = 20,
            Embedding = new float[] { 0.1f, 0.2f },
            ChunkIdFkNavigation = new ChunkDto
            {
                Id = 10,
                Content = "Test content",
                DocumentId = 5
            }
        };

        // Act
        var entity = dto.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(1);
        entity.ChunkIdFk.Should().Be(10);
        entity.ModelIdFk.Should().Be(20);
        entity.ChunkIdFkNavigation.Should().NotBeNull();
        entity.ChunkIdFkNavigation.Id.Should().Be(10);
        entity.ChunkIdFkNavigation.Content.Should().Be("Test content");
    }

    [Fact]
    public void ToSource_ShouldHandleNullNestedFacet_WithNullableProperties()
    {
        // Arrange
        var dto = new NullableWorkerFacet
        {
            Id = 3,
            Name = "Bob Johnson",
            Company = null
        };

        // Act
        var entity = dto.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(3);
        entity.Name.Should().Be("Bob Johnson");
        entity.Company.Should().BeNull();
    }

    [Fact]
    public void ToSource_ShouldMapNonNullNestedFacet_WithNullableProperties()
    {
        // Arrange
        var dto = new NullableWorkerFacet
        {
            Id = 4,
            Name = "Alice Williams",
            Company = new NullableCompanyFacet
            {
                Id = 200,
                Name = "TechCo",
                Address = "456 Tech Ave"
            }
        };

        // Act
        var entity = dto.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(4);
        entity.Name.Should().Be("Alice Williams");
        entity.Company.Should().NotBeNull();
        entity.Company!.Id.Should().Be(200);
        entity.Company.Name.Should().Be("TechCo");
        entity.Company.Address.Should().Be("456 Tech Ave");
    }

    [Fact]
    public void Projection_ShouldHandleNullableNestedFacet_WithNullableProperties()
    {
        // Arrange
        var workers = new[]
        {
            new NullableWorkerEntity
            {
                Id = 1,
                Name = "Worker 1",
                Company = null
            },
            new NullableWorkerEntity
            {
                Id = 2,
                Name = "Worker 2",
                Company = new NullableCompanyEntity { Id = 100, Name = "Company A", Address = "Address A" }
            }
        }.AsQueryable();

        // Act
        var dtos = workers.Select(NullableWorkerFacet.Projection).ToList();

        // Assert
        dtos.Should().HaveCount(2);
        dtos[0].Id.Should().Be(1);
        dtos[0].Company.Should().BeNull();
        dtos[1].Id.Should().Be(2);
        dtos[1].Company.Should().NotBeNull();
        dtos[1].Company!.Name.Should().Be("Company A");
    }
}
