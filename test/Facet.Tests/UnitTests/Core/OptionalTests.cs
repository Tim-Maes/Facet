using Facet;

namespace Facet.Tests.UnitTests.Core;

public class OptionalTests
{
    [Fact]
    public void Optional_DefaultConstructor_ShouldHaveNoValue()
    {
        var optional = new Optional<string>();

        optional.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Optional_WithValue_ShouldHaveValue()
    {
        var optional = new Optional<string>("test");

        optional.HasValue.Should().BeTrue();
        optional.Value.Should().Be("test");
    }

    [Fact]
    public void Optional_WithNullValue_ShouldHaveValue()
    {
        var optional = new Optional<string?>(null);

        optional.HasValue.Should().BeTrue("null is a valid value that was explicitly set");
        optional.Value.Should().BeNull();
    }

    [Fact]
    public void Optional_AccessingValueWhenNotSet_ShouldThrow()
    {
        var optional = new Optional<string>();

        Action act = () => { var value = optional.Value; };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Optional does not have a value.");
    }

    [Fact]
    public void Optional_GetValueOrDefault_WhenHasValue_ShouldReturnValue()
    {
        var optional = new Optional<int>(42);

        var result = optional.GetValueOrDefault(0);

        result.Should().Be(42);
    }

    [Fact]
    public void Optional_GetValueOrDefault_WhenNoValue_ShouldReturnDefault()
    {
        var optional = new Optional<int>();

        var result = optional.GetValueOrDefault(99);

        result.Should().Be(99);
    }

    [Fact]
    public void Optional_ImplicitConversion_ShouldWork()
    {
        Optional<string> optional = "test";

        optional.HasValue.Should().BeTrue();
        optional.Value.Should().Be("test");
    }

    [Fact]
    public void Optional_Equals_BothEmpty_ShouldBeEqual()
    {
        var optional1 = new Optional<string>();
        var optional2 = new Optional<string>();

        optional1.Equals(optional2).Should().BeTrue();
        (optional1 == optional2).Should().BeTrue();
    }

    [Fact]
    public void Optional_Equals_SameValue_ShouldBeEqual()
    {
        var optional1 = new Optional<string>("test");
        var optional2 = new Optional<string>("test");

        optional1.Equals(optional2).Should().BeTrue();
        (optional1 == optional2).Should().BeTrue();
    }

    [Fact]
    public void Optional_Equals_DifferentValues_ShouldNotBeEqual()
    {
        var optional1 = new Optional<string>("test1");
        var optional2 = new Optional<string>("test2");

        optional1.Equals(optional2).Should().BeFalse();
        (optional1 != optional2).Should().BeTrue();
    }

    [Fact]
    public void Optional_Equals_OneEmptyOneWithValue_ShouldNotBeEqual()
    {
        var optional1 = new Optional<string>();
        var optional2 = new Optional<string>("test");

        optional1.Equals(optional2).Should().BeFalse();
        (optional1 != optional2).Should().BeTrue();
    }

    [Fact]
    public void Optional_ToString_WhenHasValue_ShouldReturnValueString()
    {
        var optional = new Optional<int>(42);

        var result = optional.ToString();

        result.Should().Be("42");
    }

    [Fact]
    public void Optional_ToString_WhenHasNullValue_ShouldReturnNull()
    {
        var optional = new Optional<string?>(null);

        var result = optional.ToString();

        result.Should().Be("null");
    }

    [Fact]
    public void Optional_ToString_WhenNoValue_ShouldReturnUnspecified()
    {
        var optional = new Optional<string>();

        var result = optional.ToString();

        result.Should().Be("unspecified");
    }

    [Fact]
    public void Optional_GetHashCode_BothEmpty_ShouldBeEqual()
    {
        var optional1 = new Optional<string>();
        var optional2 = new Optional<string>();

        optional1.GetHashCode().Should().Be(optional2.GetHashCode());
    }

    [Fact]
    public void Optional_GetHashCode_SameValue_ShouldBeEqual()
    {
        var optional1 = new Optional<string>("test");
        var optional2 = new Optional<string>("test");

        optional1.GetHashCode().Should().Be(optional2.GetHashCode());
    }

    [Fact]
    public void Optional_WithValueType_ShouldWork()
    {
        var optionalInt = new Optional<int>(42);
        var optionalBool = new Optional<bool>(true);
        var optionalDateTime = new Optional<DateTime>(new DateTime(2024, 1, 1));

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
        var unspecified = new Optional<int?>();
        var explicitlyNull = new Optional<int?>(null);
        var withValue = new Optional<int?>(42);

        unspecified.HasValue.Should().BeFalse("value was not specified");

        explicitlyNull.HasValue.Should().BeTrue("null was explicitly set");
        explicitlyNull.Value.Should().BeNull();

        withValue.HasValue.Should().BeTrue();
        withValue.Value.Should().Be(42);
    }
}
