using Facet;

namespace Facet.Tests.UnitTests.Core;

/// <summary>
/// Tests for the Optional&lt;T&gt; type used in Patch DTOs.
/// </summary>
public class OptionalTests
{
    [Fact]
    public void Optional_DefaultConstructor_ShouldHaveNoValue()
    {
        // Arrange & Act
        var optional = new Optional<string>();

        // Assert
        optional.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Optional_WithValue_ShouldHaveValue()
    {
        // Arrange & Act
        var optional = new Optional<string>("test");

        // Assert
        optional.HasValue.Should().BeTrue();
        optional.Value.Should().Be("test");
    }

    [Fact]
    public void Optional_WithNullValue_ShouldHaveValue()
    {
        // Arrange & Act
        var optional = new Optional<string?>(null);

        // Assert
        optional.HasValue.Should().BeTrue("null is a valid value that was explicitly set");
        optional.Value.Should().BeNull();
    }

    [Fact]
    public void Optional_AccessingValueWhenNotSet_ShouldThrow()
    {
        // Arrange
        var optional = new Optional<string>();

        // Act
        Action act = () => { var value = optional.Value; };

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Optional does not have a value.");
    }

    [Fact]
    public void Optional_GetValueOrDefault_WhenHasValue_ShouldReturnValue()
    {
        // Arrange
        var optional = new Optional<int>(42);

        // Act
        var result = optional.GetValueOrDefault(0);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void Optional_GetValueOrDefault_WhenNoValue_ShouldReturnDefault()
    {
        // Arrange
        var optional = new Optional<int>();

        // Act
        var result = optional.GetValueOrDefault(99);

        // Assert
        result.Should().Be(99);
    }

    [Fact]
    public void Optional_ImplicitConversion_ShouldWork()
    {
        // Arrange & Act
        Optional<string> optional = "test";

        // Assert
        optional.HasValue.Should().BeTrue();
        optional.Value.Should().Be("test");
    }

    [Fact]
    public void Optional_Equals_BothEmpty_ShouldBeEqual()
    {
        // Arrange
        var optional1 = new Optional<string>();
        var optional2 = new Optional<string>();

        // Act & Assert
        optional1.Equals(optional2).Should().BeTrue();
        (optional1 == optional2).Should().BeTrue();
    }

    [Fact]
    public void Optional_Equals_SameValue_ShouldBeEqual()
    {
        // Arrange
        var optional1 = new Optional<string>("test");
        var optional2 = new Optional<string>("test");

        // Act & Assert
        optional1.Equals(optional2).Should().BeTrue();
        (optional1 == optional2).Should().BeTrue();
    }

    [Fact]
    public void Optional_Equals_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var optional1 = new Optional<string>("test1");
        var optional2 = new Optional<string>("test2");

        // Act & Assert
        optional1.Equals(optional2).Should().BeFalse();
        (optional1 != optional2).Should().BeTrue();
    }

    [Fact]
    public void Optional_Equals_OneEmptyOneWithValue_ShouldNotBeEqual()
    {
        // Arrange
        var optional1 = new Optional<string>();
        var optional2 = new Optional<string>("test");

        // Act & Assert
        optional1.Equals(optional2).Should().BeFalse();
        (optional1 != optional2).Should().BeTrue();
    }

    [Fact]
    public void Optional_ToString_WhenHasValue_ShouldReturnValueString()
    {
        // Arrange
        var optional = new Optional<int>(42);

        // Act
        var result = optional.ToString();

        // Assert
        result.Should().Be("42");
    }

    [Fact]
    public void Optional_ToString_WhenHasNullValue_ShouldReturnNull()
    {
        // Arrange
        var optional = new Optional<string?>(null);

        // Act
        var result = optional.ToString();

        // Assert
        result.Should().Be("null");
    }

    [Fact]
    public void Optional_ToString_WhenNoValue_ShouldReturnUnspecified()
    {
        // Arrange
        var optional = new Optional<string>();

        // Act
        var result = optional.ToString();

        // Assert
        result.Should().Be("unspecified");
    }

    [Fact]
    public void Optional_GetHashCode_BothEmpty_ShouldBeEqual()
    {
        // Arrange
        var optional1 = new Optional<string>();
        var optional2 = new Optional<string>();

        // Act & Assert
        optional1.GetHashCode().Should().Be(optional2.GetHashCode());
    }

    [Fact]
    public void Optional_GetHashCode_SameValue_ShouldBeEqual()
    {
        // Arrange
        var optional1 = new Optional<string>("test");
        var optional2 = new Optional<string>("test");

        // Act & Assert
        optional1.GetHashCode().Should().Be(optional2.GetHashCode());
    }

    [Fact]
    public void Optional_WithValueType_ShouldWork()
    {
        // Arrange & Act
        var optionalInt = new Optional<int>(42);
        var optionalBool = new Optional<bool>(true);
        var optionalDateTime = new Optional<DateTime>(new DateTime(2024, 1, 1));

        // Assert
        optionalInt.HasValue.Should().BeTrue();
        optionalInt.Value.Should().Be(42);

        optionalBool.HasValue.Should().BeTrue();
        optionalBool.Value.Should().BeTrue();

        optionalDateTime.HasValue.Should().BeTrue();
        optionalDateTime.Value.Should().Be(new DateTime(2024, 1, 1));
    }

    [Fact]
    public void Optional_WithNullableValueType_ShouldDistinguishNullFromUnspecified()
    {
        // Arrange & Act
        var unspecified = new Optional<int?>();
        var explicitlyNull = new Optional<int?>(null);
        var withValue = new Optional<int?>(42);

        // Assert
        unspecified.HasValue.Should().BeFalse("value was not specified");

        explicitlyNull.HasValue.Should().BeTrue("null was explicitly set");
        explicitlyNull.Value.Should().BeNull();

        withValue.HasValue.Should().BeTrue();
        withValue.Value.Should().Be(42);
    }
}
