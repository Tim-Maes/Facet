using Facet.Tests.TestModels.FacetMap;

namespace Facet.Tests.UnitTests.Core.FacetMap;

/// <summary>
/// Tests for FacetMap with [MapFrom] and [MapWhen] attributes on target type properties.
/// </summary>
public class FacetMapAttributeTests
{
    #region MapFrom

    [Fact]
    public void ToTarget_MapFrom_ShouldMapFromSpecifiedSourceProperty()
    {
        var entity = new PersonEntity
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com"
        };

        var dto = entity.ToPersonSummaryDto();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("John", "Name should be mapped from FirstName via [MapFrom]");
        dto.Email.Should().Be("john@example.com");
    }

    #endregion

    #region MapWhen

    [Fact]
    public void ToTarget_MapWhen_ConditionTrue_ShouldMapValue()
    {
        var entity = new SensorEntity
        {
            Id = 1,
            Name = "Temperature",
            Value = 42.5,
            IsActive = true
        };

        var dto = entity.ToSensorDto();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Temperature");
        dto.Value.Should().Be(42.5, "Value should be mapped when IsActive is true");
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ToTarget_MapWhen_ConditionFalse_ShouldUseDefault()
    {
        var entity = new SensorEntity
        {
            Id = 2,
            Name = "Pressure",
            Value = 1013.25,
            IsActive = false
        };

        var dto = entity.ToSensorDto();

        dto.Should().NotBeNull();
        dto.Id.Should().Be(2);
        dto.Name.Should().Be("Pressure");
        dto.Value.Should().Be(0, "Value should be default(double) when IsActive is false");
        dto.IsActive.Should().BeFalse();
    }

    #endregion
}
