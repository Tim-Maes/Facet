using Facet.Tests.TestModels.FacetMap;

namespace Facet.Tests.UnitTests.Core.FacetMap;

public class FacetMapTypeConversionTests
{
    // ========================================
    // Nullable -> Non-nullable value type conversion
    // ========================================

    [Fact]
    public void ToTarget_NullableToNonNullable_WithValues_ShouldMapCorrectly()
    {
        var entity = new NullableSourceEntity
        {
            Id = 1,
            Name = "Test",
            NullableCount = 42,
            NullableAmount = 99.99m,
            NullableDate = new DateTime(2024, 6, 15),
            NullableFlag = true
        };

        var dto = entity.ToNonNullableTargetDto();

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Test");
        dto.NullableCount.Should().Be(42);
        dto.NullableAmount.Should().Be(99.99m);
        dto.NullableDate.Should().Be(new DateTime(2024, 6, 15));
        dto.NullableFlag.Should().BeTrue();
    }

    [Fact]
    public void ToTarget_NullableToNonNullable_WithNulls_ShouldUseDefaults()
    {
        var entity = new NullableSourceEntity
        {
            Id = 2,
            Name = "NullTest",
            NullableCount = null,
            NullableAmount = null,
            NullableDate = null,
            NullableFlag = null
        };

        var dto = entity.ToNonNullableTargetDto();

        dto.Id.Should().Be(2);
        dto.Name.Should().Be("NullTest");
        dto.NullableCount.Should().Be(0);
        dto.NullableAmount.Should().Be(0m);
        dto.NullableDate.Should().Be(default(DateTime));
        dto.NullableFlag.Should().BeFalse();
    }

    [Fact]
    public void ToSource_NullableToNonNullable_ShouldMapBack()
    {
        var dto = new NonNullableTargetDto
        {
            Id = 3,
            Name = "ReverseTest",
            NullableCount = 10,
            NullableAmount = 55.5m,
            NullableDate = new DateTime(2025, 1, 1),
            NullableFlag = false
        };

        var entity = dto.ToNullableSourceEntity();

        entity.Id.Should().Be(3);
        entity.Name.Should().Be("ReverseTest");
        entity.NullableCount.Should().Be(10);
        entity.NullableAmount.Should().Be(55.5m);
        entity.NullableDate.Should().Be(new DateTime(2025, 1, 1));
        entity.NullableFlag.Should().BeFalse();
    }

    // ========================================
    // Non-nullable -> Nullable value type conversion (implicit widening)
    // ========================================

    [Fact]
    public void ToTarget_NonNullableToNullable_ShouldMapCorrectly()
    {
        var entity = new NonNullableSourceEntity
        {
            Id = 1,
            Count = 42,
            Amount = 99.99m
        };

        var dto = entity.ToNullableTargetDto();

        dto.Id.Should().Be(1);
        dto.Count.Should().Be(42);
        dto.Amount.Should().Be(99.99m);
    }

    [Fact]
    public void ToSource_NonNullableToNullable_ShouldMapBack()
    {
        var dto = new NullableTargetDto
        {
            Id = 2,
            Count = 10,
            Amount = 55.5m
        };

        var entity = dto.ToNonNullableSourceEntity();

        entity.Id.Should().Be(2);
        entity.Count.Should().Be(10);
        entity.Amount.Should().Be(55.5m);
    }

    [Fact]
    public void ToSource_NonNullableToNullable_WithNulls_ShouldUseDefaults()
    {
        var dto = new NullableTargetDto
        {
            Id = 3,
            Count = null,
            Amount = null
        };

        var entity = dto.ToNonNullableSourceEntity();

        entity.Id.Should().Be(3);
        entity.Count.Should().Be(0);
        entity.Amount.Should().Be(0m);
    }

    // ========================================
    // Incompatible types - properties with same name but different incompatible types
    // ========================================

    [Fact]
    public void ToTarget_IncompatibleTypes_ShouldSkipMismatchedProperties()
    {
        var entity = new EntityWithStringProp
        {
            Id = 1,
            Name = "Test",
            PrinterSettings = "{\"format\": \"A4\"}"
        };

        var dto = entity.ToDtoWithComplexProp();

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Test");
        // PrinterSettings should NOT be mapped (string -> PrinterSettingsType is incompatible)
        // The PrinterSettings on the DTO should remain as the default
        dto.PrinterSettings.Should().NotBeNull();
        dto.PrinterSettings.Format.Should().Be(string.Empty);
        dto.PrinterSettings.Copies.Should().Be(0);
    }

    // ========================================
    // Dictionary property tests
    // ========================================

    [Fact]
    public void ToTarget_SameDictionaryType_ShouldMapCorrectly()
    {
        var entity = new EntityWithDictionary
        {
            Id = 1,
            Name = "DictTest",
            Metadata = new Dictionary<string, int> { { "key1", 1 }, { "key2", 2 } }
        };

        var dto = entity.ToDtoWithDictionary();

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("DictTest");
        dto.Metadata.Should().ContainKey("key1").WhoseValue.Should().Be(1);
        dto.Metadata.Should().ContainKey("key2").WhoseValue.Should().Be(2);
    }

    [Fact]
    public void ToSource_SameDictionaryType_ShouldMapBack()
    {
        var dto = new DtoWithDictionary
        {
            Id = 2,
            Name = "ReverseDict",
            Metadata = new Dictionary<string, int> { { "a", 10 } }
        };

        var entity = dto.ToEntityWithDictionary();

        entity.Id.Should().Be(2);
        entity.Name.Should().Be("ReverseDict");
        entity.Metadata.Should().ContainKey("a").WhoseValue.Should().Be(10);
    }

    [Fact]
    public void ToTarget_DifferentDictionaryTypes_ShouldSkipMismatched()
    {
        var entity = new EntityWithDifferentDict
        {
            Id = 1,
            Scores = new Dictionary<string, int> { { "math", 95 } }
        };

        var dto = entity.ToDtoWithDifferentDict();

        dto.Id.Should().Be(1);
        // Scores should NOT be mapped (Dictionary<string,int> -> Dictionary<string,string> is incompatible)
        dto.Scores.Should().BeEmpty();
    }
}
