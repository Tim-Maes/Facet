using Facet.Tests.TestModels.FacetMap;

namespace Facet.Tests.UnitTests.Core.FacetMap;

/// <summary>
/// Tests for FacetMap nested type mapping where source and target property types differ
/// (e.g., ICollection&lt;AddressEntity&gt; on source vs ICollection&lt;AddressDto&gt; on target).
/// </summary>
public class FacetMapNestedTypeTests
{
    #region Single nested type mapping

    [Fact]
    public void ToTarget_ShouldMapSingleNestedType()
    {
        var entity = new ContactEntity
        {
            Id = 1,
            Name = "John",
            Email = "john@example.com",
            CultureInfo = new CultureInfoEntity
            {
                Id = 10,
                Code = "en-US",
                DisplayName = "English (US)"
            },
            Addresses = new List<ContactAddressEntity>()
        };

        var dto = entity.ToContactEntityDto();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("John");
        dto.Email.Should().Be("john@example.com");
        dto.CultureInfo.Should().NotBeNull();
        dto.CultureInfo!.Id.Should().Be(10);
        dto.CultureInfo.Code.Should().Be("en-US");
        dto.CultureInfo.DisplayName.Should().Be("English (US)");
    }

    [Fact]
    public void ToTarget_NullSingleNestedType_ShouldMapToNull()
    {
        var entity = new ContactEntity
        {
            Id = 1,
            Name = "Jane",
            Email = "jane@example.com",
            CultureInfo = null,
            Addresses = new List<ContactAddressEntity>()
        };

        var dto = entity.ToContactEntityDto();

        dto.Should().NotBeNull();
        dto.CultureInfo.Should().BeNull();
    }

    [Fact]
    public void ToSource_ShouldMapSingleNestedTypeBack()
    {
        var dto = new ContactEntityDto
        {
            Id = 2,
            Name = "Jane",
            Email = "jane@example.com",
            CultureInfo = new CultureInfoEntityDto
            {
                Id = 20,
                Code = "da-DK",
                DisplayName = "Danish"
            },
            Addresses = new List<ContactAddressEntityDto>()
        };

        var entity = dto.ToContactEntity();

        entity.Should().NotBeNull();
        entity.Id.Should().Be(2);
        entity.CultureInfo.Should().NotBeNull();
        entity.CultureInfo!.Id.Should().Be(20);
        entity.CultureInfo.Code.Should().Be("da-DK");
        entity.CultureInfo.DisplayName.Should().Be("Danish");
    }

    [Fact]
    public void ToSource_NullSingleNestedType_ShouldMapToNull()
    {
        var dto = new ContactEntityDto
        {
            Id = 3,
            Name = "Test",
            Email = "test@example.com",
            CultureInfo = null,
            Addresses = new List<ContactAddressEntityDto>()
        };

        var entity = dto.ToContactEntity();

        entity.Should().NotBeNull();
        entity.CultureInfo.Should().BeNull();
    }

    #endregion

    #region Collection nested type mapping

    [Fact]
    public void ToTarget_ShouldMapCollectionNestedType()
    {
        var entity = new ContactEntity
        {
            Id = 1,
            Name = "John",
            Email = "john@example.com",
            CultureInfo = null,
            Addresses = new List<ContactAddressEntity>
            {
                new() { Id = 1, Street = "Main St", City = "Springfield", PostalCode = "12345" },
                new() { Id = 2, Street = "Oak Ave", City = "Shelbyville", PostalCode = "67890" }
            }
        };

        var dto = entity.ToContactEntityDto();

        dto.Should().NotBeNull();
        dto.Addresses.Should().NotBeNull();
        dto.Addresses.Should().HaveCount(2);
        dto.Addresses.First().Street.Should().Be("Main St");
        dto.Addresses.First().City.Should().Be("Springfield");
        dto.Addresses.Last().Street.Should().Be("Oak Ave");
        dto.Addresses.Last().PostalCode.Should().Be("67890");
    }

    [Fact]
    public void ToSource_ShouldMapCollectionNestedTypeBack()
    {
        var dto = new ContactEntityDto
        {
            Id = 2,
            Name = "Jane",
            Email = "jane@example.com",
            CultureInfo = null,
            Addresses = new List<ContactAddressEntityDto>
            {
                new() { Id = 10, Street = "Elm St", City = "Portland", PostalCode = "97201" }
            }
        };

        var entity = dto.ToContactEntity();

        entity.Should().NotBeNull();
        entity.Addresses.Should().NotBeNull();
        entity.Addresses.Should().HaveCount(1);
        entity.Addresses.First().Street.Should().Be("Elm St");
        entity.Addresses.First().City.Should().Be("Portland");
    }

    [Fact]
    public void ToTarget_EmptyCollection_ShouldMapToEmptyCollection()
    {
        var entity = new ContactEntity
        {
            Id = 3,
            Name = "Empty",
            Email = "empty@example.com",
            CultureInfo = null,
            Addresses = new List<ContactAddressEntity>()
        };

        var dto = entity.ToContactEntityDto();

        dto.Should().NotBeNull();
        dto.Addresses.Should().NotBeNull();
        dto.Addresses.Should().BeEmpty();
    }

    #endregion

    #region Combined nested types

    [Fact]
    public void ToTarget_ShouldMapBothSingleAndCollectionNestedTypes()
    {
        var entity = new ContactEntity
        {
            Id = 1,
            Name = "Full",
            Email = "full@example.com",
            CultureInfo = new CultureInfoEntity { Id = 1, Code = "en-GB", DisplayName = "English (UK)" },
            Addresses = new List<ContactAddressEntity>
            {
                new() { Id = 1, Street = "Baker St", City = "London", PostalCode = "NW1 6XE" }
            }
        };

        var dto = entity.ToContactEntityDto();

        dto.Should().NotBeNull();
        dto.CultureInfo.Should().NotBeNull();
        dto.CultureInfo!.Code.Should().Be("en-GB");
        dto.Addresses.Should().HaveCount(1);
        dto.Addresses.First().Street.Should().Be("Baker St");
    }

    [Fact]
    public void ApplyToSource_ShouldMapNestedTypes()
    {
        var dto = new ContactEntityDto
        {
            Id = 5,
            Name = "Updated",
            Email = "updated@example.com",
            CultureInfo = new CultureInfoEntityDto { Id = 50, Code = "fr-FR", DisplayName = "French" },
            Addresses = new List<ContactAddressEntityDto>
            {
                new() { Id = 100, Street = "Champs-Elysees", City = "Paris", PostalCode = "75008" }
            }
        };

        var existingEntity = new ContactEntity
        {
            Id = 5,
            Name = "Original",
            Email = "original@example.com",
            CultureInfo = null,
            Addresses = new List<ContactAddressEntity>()
        };

        dto.ApplyToContactEntity(existingEntity);

        existingEntity.Id.Should().Be(5);
        existingEntity.Name.Should().Be("Updated");
        existingEntity.Email.Should().Be("updated@example.com");
        existingEntity.CultureInfo.Should().NotBeNull();
        existingEntity.CultureInfo!.Code.Should().Be("fr-FR");
        existingEntity.Addresses.Should().HaveCount(1);
        existingEntity.Addresses.First().Street.Should().Be("Champs-Elysees");
    }

    #endregion

    #region Individual nested type mapper tests

    [Fact]
    public void CultureInfoMapper_ShouldMapForward()
    {
        var entity = new CultureInfoEntity { Id = 1, Code = "en-US", DisplayName = "English" };

        var dto = entity.ToCultureInfoEntityDto();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Code.Should().Be("en-US");
    }

    [Fact]
    public void CultureInfoMapper_ShouldMapReverse()
    {
        var dto = new CultureInfoEntityDto { Id = 2, Code = "da-DK", DisplayName = "Danish" };

        var entity = dto.ToCultureInfoEntity();

        entity.Should().NotBeNull();
        entity.Id.Should().Be(2);
        entity.Code.Should().Be("da-DK");
    }

    [Fact]
    public void ContactAddressMapper_ShouldMapForward()
    {
        var entity = new ContactAddressEntity { Id = 1, Street = "Main St", City = "NYC", PostalCode = "10001" };

        var dto = entity.ToContactAddressEntityDto();

        dto.Should().NotBeNull();
        dto.Street.Should().Be("Main St");
        dto.City.Should().Be("NYC");
    }

    [Fact]
    public void ContactAddressMapper_ShouldMapReverse()
    {
        var dto = new ContactAddressEntityDto { Id = 2, Street = "Elm St", City = "LA", PostalCode = "90001" };

        var entity = dto.ToContactAddressEntity();

        entity.Should().NotBeNull();
        entity.Street.Should().Be("Elm St");
        entity.City.Should().Be("LA");
    }

    #endregion
}
