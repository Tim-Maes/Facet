using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

// Test entities with nullable nested properties
public class PersonEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AddressEntity? MailingAddress { get; set; }
}

public class DataTableEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string StringResource { get; set; } = string.Empty;
    public DataTableExtendedDataEntity? ExtendedData { get; set; }
}

public class DataTableExtendedDataEntity
{
    public int Id { get; set; }
    public string Metadata { get; set; } = string.Empty;
}

public class OrganizationEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<PersonEntity>? OptionalMembers { get; set; }
}

// Facet DTOs with nullable nested facets
[Facet(typeof(DataTableExtendedDataEntity), GenerateToSource = true)]
public partial record DataTableExtendedDataDto;

[Facet(
    typeof(DataTableEntity),
    NestedFacets = [typeof(DataTableExtendedDataDto)],
    GenerateToSource = true)]
public partial record DataTableFacetDto;

[Facet(
    typeof(PersonEntity),
    NestedFacets = [typeof(AddressFacet)],
    GenerateToSource = true)]
public partial record PersonDto;

[Facet(
    typeof(OrganizationEntity),
    NestedFacets = [typeof(PersonDto)],
    GenerateToSource = true)]
public partial record OrganizationDto;

public class NullableNestedFacetsTests
{
    [Fact]
    public void Constructor_ShouldHandleNullNestedFacet_WithoutThrowingException()
    {
        // Arrange
        var dataTable = new DataTableEntity
        {
            Id = 1,
            Code = "TEST001",
            StringResource = "Test Resource",
            ExtendedData = null
        };

        // Act
        var dto = new DataTableFacetDto(dataTable);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Code.Should().Be("TEST001");
        dto.StringResource.Should().Be("Test Resource");
        dto.ExtendedData.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapNonNullNestedFacet_Correctly()
    {
        // Arrange
        var dataTable = new DataTableEntity
        {
            Id = 2,
            Code = "TEST002",
            StringResource = "Another Resource",
            ExtendedData = new DataTableExtendedDataEntity
            {
                Id = 100,
                Metadata = "Extended metadata"
            }
        };

        // Act
        var dto = new DataTableFacetDto(dataTable);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(2);
        dto.Code.Should().Be("TEST002");
        dto.StringResource.Should().Be("Another Resource");
        dto.ExtendedData.Should().NotBeNull();
        dto.ExtendedData!.Id.Should().Be(100);
        dto.ExtendedData.Metadata.Should().Be("Extended metadata");
    }

    [Fact]
    public void Constructor_ShouldHandleNullNestedFacet_InMultipleProperties()
    {
        // Arrange
        var person = new PersonEntity
        {
            Id = 1,
            Name = "John Doe",
            MailingAddress = null
        };

        // Act
        var dto = new PersonDto(person);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("John Doe");
        dto.MailingAddress.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapNonNullNestedFacet_WhenProvided()
    {
        // Arrange
        var person = new PersonEntity
        {
            Id = 2,
            Name = "Jane Smith",
            MailingAddress = new AddressEntity
            {
                Street = "123 Main St",
                City = "Anytown",
                State = "CA",
                ZipCode = "12345",
                Country = "USA"
            }
        };

        // Act
        var dto = new PersonDto(person);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(2);
        dto.Name.Should().Be("Jane Smith");
        dto.MailingAddress.Should().NotBeNull();
        dto.MailingAddress!.Street.Should().Be("123 Main St");
        dto.MailingAddress.City.Should().Be("Anytown");
    }

    [Fact]
    public void ToSource_ShouldHandleNullNestedFacet_Correctly()
    {
        // Arrange
        var dto = new DataTableFacetDto
        {
            Id = 3,
            Code = "TEST003",
            StringResource = "Resource",
            ExtendedData = null
        };

        // Act
        var entity = dto.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(3);
        entity.Code.Should().Be("TEST003");
        entity.StringResource.Should().Be("Resource");
        entity.ExtendedData.Should().BeNull();
    }

    [Fact]
    public void ToSource_ShouldMapNonNullNestedFacet_Correctly()
    {
        // Arrange
        var dto = new DataTableFacetDto
        {
            Id = 4,
            Code = "TEST004",
            StringResource = "Another",
            ExtendedData = new DataTableExtendedDataDto
            {
                Id = 200,
                Metadata = "Metadata value"
            }
        };

        // Act
        var entity = dto.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(4);
        entity.Code.Should().Be("TEST004");
        entity.StringResource.Should().Be("Another");
        entity.ExtendedData.Should().NotBeNull();
        entity.ExtendedData!.Id.Should().Be(200);
        entity.ExtendedData.Metadata.Should().Be("Metadata value");
    }

    [Fact]
    public void Constructor_ShouldHandleNullCollectionNestedFacet_WithoutThrowingException()
    {
        // Arrange
        var org = new OrganizationEntity
        {
            Id = 1,
            Name = "Test Org",
            OptionalMembers = null
        };

        // Act
        var dto = new OrganizationDto(org);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Test Org");
        dto.OptionalMembers.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldMapNonNullCollectionNestedFacet_Correctly()
    {
        // Arrange
        var org = new OrganizationEntity
        {
            Id = 2,
            Name = "Another Org",
            OptionalMembers = new List<PersonEntity>
            {
                new PersonEntity { Id = 1, Name = "Person 1", MailingAddress = null },
                new PersonEntity
                {
                    Id = 2,
                    Name = "Person 2",
                    MailingAddress = new AddressEntity
                    {
                        City = "Test City"
                    }
                }
            }
        };

        // Act
        var dto = new OrganizationDto(org);

        // Assert
        dto.Should().NotBeNull();
        dto.Id.Should().Be(2);
        dto.Name.Should().Be("Another Org");
        dto.OptionalMembers.Should().NotBeNull();
        dto.OptionalMembers.Should().HaveCount(2);
        dto.OptionalMembers![0].Id.Should().Be(1);
        dto.OptionalMembers[0].Name.Should().Be("Person 1");
        dto.OptionalMembers[0].MailingAddress.Should().BeNull();
        dto.OptionalMembers[1].Id.Should().Be(2);
        dto.OptionalMembers[1].Name.Should().Be("Person 2");
        dto.OptionalMembers[1].MailingAddress.Should().NotBeNull();
        dto.OptionalMembers[1].MailingAddress!.City.Should().Be("Test City");
    }

    [Fact]
    public void ToSource_ShouldHandleNullCollectionNestedFacet_Correctly()
    {
        // Arrange
        var dto = new OrganizationDto
        {
            Id = 3,
            Name = "Org 3",
            OptionalMembers = null
        };

        // Act
        var entity = dto.ToSource();

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(3);
        entity.Name.Should().Be("Org 3");
        entity.OptionalMembers.Should().BeNull();
    }

    [Fact]
    public void Projection_ShouldHandleNullNestedFacet_InLinqQuery()
    {
        // Arrange
        var dataTables = new[]
        {
            new DataTableEntity
            {
                Id = 1,
                Code = "A001",
                StringResource = "Resource A",
                ExtendedData = null
            },
            new DataTableEntity
            {
                Id = 2,
                Code = "B001",
                StringResource = "Resource B",
                ExtendedData = new DataTableExtendedDataEntity { Id = 100, Metadata = "Meta B" }
            }
        }.AsQueryable();

        // Act
        var dtos = dataTables.Select(DataTableFacetDto.Projection).ToList();

        // Assert
        dtos.Should().HaveCount(2);
        dtos[0].Id.Should().Be(1);
        dtos[0].ExtendedData.Should().BeNull();
        dtos[1].Id.Should().Be(2);
        dtos[1].ExtendedData.Should().NotBeNull();
        dtos[1].ExtendedData!.Metadata.Should().Be("Meta B");
    }
}
