using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// When a user has initialization logic in their parameterless constructor,
/// the generated constructor should be able to chain to it using `: this()`.
/// </summary>
public class ChainToParameterlessConstructorTests
{
    [Fact]
    public void ChainedConstructor_ShouldCallParameterlessConstructorFirst()
    {
        // Arrange
        var source = new ModelTypeForChaining
        {
            MaxValue = 42,
            Name = "Test"
        };

        // Act
        var dto = new ChainedConstructorDto(source);

        // Assert - Parameterless constructor should have run, setting Value = 100 and Initialized = true
        // Then the source properties should be mapped
        dto.Initialized.Should().BeTrue("Parameterless constructor should have run");
        dto.Value.Should().Be(100, "Value should be initialized by parameterless constructor");
        dto.MaxValue.Should().Be(42, "MaxValue should be mapped from source");
        dto.Name.Should().Be("Test", "Name should be mapped from source");
    }

    [Fact]
    public void NonChainedConstructor_ShouldNotCallParameterlessConstructor()
    {
        // Arrange
        var source = new ModelTypeForChaining
        {
            MaxValue = 42,
            Name = "Test"
        };

        // Act
        var dto = new NonChainedConstructorDto(source);

        // Assert - Parameterless constructor should NOT have run
        // The Initialized and Value properties should have their default values
        dto.Initialized.Should().BeFalse("Parameterless constructor should NOT have run");
        dto.Value.Should().Be(0, "Value should have default value since parameterless ctor didn't run");
        dto.MaxValue.Should().Be(42, "MaxValue should still be mapped from source");
        dto.Name.Should().Be("Test", "Name should still be mapped from source");
    }

    [Fact]
    public void ChainedConstructorNoDepth_ShouldCallParameterlessConstructor()
    {
        // Arrange
        var source = new ModelTypeForChaining
        {
            MaxValue = 99,
            Name = "NoDepth"
        };

        // Act
        var dto = new ChainedConstructorNoDepthDto(source);

        // Assert - Parameterless constructor should have run with different Value
        dto.Initialized.Should().BeTrue("Parameterless constructor should have run");
        dto.Value.Should().Be(200, "Value should be 200 from this specific parameterless constructor");
        dto.MaxValue.Should().Be(99, "MaxValue should be mapped from source");
        dto.Name.Should().Be("NoDepth", "Name should be mapped from source");
    }

    [Fact]
    public void ChainedConstructor_ManualParameterlessCall_ShouldWork()
    {
        // Arrange & Act - Call the user's parameterless constructor directly
        var dto = new ChainedConstructorDto();

        // Assert
        dto.Initialized.Should().BeTrue();
        dto.Value.Should().Be(100);
        dto.MaxValue.Should().Be(0, "MaxValue should have default int value");
        // Note: Name won't be set by parameterless constructor, but the property has a default value from the source
    }

    [Fact]
    public void ChainedConstructor_FromSource_ShouldWorkWithChaining()
    {
        // Arrange
        var source = new ModelTypeForChaining
        {
            MaxValue = 123,
            Name = "FromSource"
        };

        // Act
        var dto = ChainedConstructorDto.FromSource(source);

        // Assert - FromSource should also use the constructor that chains
        dto.Initialized.Should().BeTrue("Initialization should have occurred");
        dto.Value.Should().Be(100, "Value should be set by parameterless constructor");
        dto.MaxValue.Should().Be(123, "MaxValue should be mapped from source");
    }
}
