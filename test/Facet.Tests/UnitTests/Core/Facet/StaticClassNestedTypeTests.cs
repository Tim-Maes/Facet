using Facet.Tests.TestModels.StaticClassTest;

namespace Facet.Tests.UnitTests.Core.Facet;

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

    [Fact]
    public void Facet_ShouldGenerateCorrectly_WhenSourceHasNestedClassProperty()
    {
        // Arrange - issue #272: nested class property should generate using static, not using
        var bar = new Application.Example1.Foo.Bar
        {
            Name = "Test",
            Value = 42,
            Arr1 = new Application.Example1.Foo.Bar.Arr { Length = 10 }
        };

        // Act
        var dto = new BarDto(bar);

        // Assert
        dto.Should().NotBeNull();
        dto.Name.Should().Be("Test");
        dto.Value.Should().Be(42);
        dto.Arr1.Should().NotBeNull();
        dto.Arr1!.Length.Should().Be(10);
    }

    [Fact]
    public void Facet_ShouldHandleNullNestedClassProperty()
    {
        // Arrange
        var bar = new Application.Example1.Foo.Bar
        {
            Name = "Test",
            Value = 42,
            Arr1 = null
        };

        // Act
        var dto = new BarDto(bar);

        // Assert
        dto.Should().NotBeNull();
        dto.Arr1.Should().BeNull();
    }
}
