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
        var source = new ModelTypeForChaining
        {
            MaxValue = 42,
            Name = "Test"
        };

        var dto = new ChainedConstructorDto(source);

        dto.Initialized.Should().BeTrue("Parameterless constructor should have run");
        dto.Value.Should().Be(100, "Value should be initialized by parameterless constructor");
        dto.MaxValue.Should().Be(42, "MaxValue should be mapped from source");
        dto.Name.Should().Be("Test", "Name should be mapped from source");
    }

    [Fact]
    public void NonChainedConstructor_ShouldNotCallParameterlessConstructor()
    {
        var source = new ModelTypeForChaining
        {
            MaxValue = 42,
            Name = "Test"
        };

        var dto = new NonChainedConstructorDto(source);

        dto.Initialized.Should().BeFalse("Parameterless constructor should NOT have run");
        dto.Value.Should().Be(0, "Value should have default value since parameterless ctor didn't run");
        dto.MaxValue.Should().Be(42, "MaxValue should still be mapped from source");
        dto.Name.Should().Be("Test", "Name should still be mapped from source");
    }

    [Fact]
    public void ChainedConstructorNoDepth_ShouldCallParameterlessConstructor()
    {
        var source = new ModelTypeForChaining
        {
            MaxValue = 99,
            Name = "NoDepth"
        };

        var dto = new ChainedConstructorNoDepthDto(source);

        dto.Initialized.Should().BeTrue("Parameterless constructor should have run");
        dto.Value.Should().Be(200, "Value should be 200 from this specific parameterless constructor");
        dto.MaxValue.Should().Be(99, "MaxValue should be mapped from source");
        dto.Name.Should().Be("NoDepth", "Name should be mapped from source");
    }

    [Fact]
    public void ChainedConstructor_ManualParameterlessCall_ShouldWork()
    {
        var dto = new ChainedConstructorDto();

        dto.Initialized.Should().BeTrue();
        dto.Value.Should().Be(100);
        dto.MaxValue.Should().Be(0, "MaxValue should have default int value");
    }

    [Fact]
    public void ChainedConstructor_FromSource_ShouldWorkWithChaining()
    {
        var source = new ModelTypeForChaining
        {
            MaxValue = 123,
            Name = "FromSource"
        };

        var dto = ChainedConstructorDto.FromSource(source);

        dto.Initialized.Should().BeTrue("Initialization should have occurred");
        dto.Value.Should().Be(100, "Value should be set by parameterless constructor");
        dto.MaxValue.Should().Be(123, "MaxValue should be mapped from source");
    }
}
