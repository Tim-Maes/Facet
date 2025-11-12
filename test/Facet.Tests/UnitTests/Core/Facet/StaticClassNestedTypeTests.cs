using Facet.Tests.TestModels.StaticClassTest;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for issue #145: Generator fails to create static imports
/// </summary>
public class StaticClassNestedTypeTests
{
    [Fact]
    public void Facet_ShouldGenerateCorrectly_WhenSourceTypeIsNestedInStaticClass()
    {
        // Arrange
        var bar = new Application.Example1.Foo.Bar
        {
            Name = "Test",
            Value = 42
        };

        // Act
        var dto = new BarDto(bar);

        // Assert
        dto.Should().NotBeNull();
        dto.Name.Should().Be("Test");
        dto.Value.Should().Be(42);
    }

    [Fact]
    public void Facet_ShouldMap_WhenSourceTypeIsNestedInStaticClass()
    {
        // Arrange
        var bar = new Application.Example1.Foo.Bar
        {
            Name = "Test",
            Value = 42
        };

        // Act
        var dto = bar.ToFacet<Application.Example1.Foo.Bar, BarDto>();

        // Assert
        dto.Should().NotBeNull();
        dto.Name.Should().Be("Test");
        dto.Value.Should().Be(42);
    }
}
