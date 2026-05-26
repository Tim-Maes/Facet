using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for the GenerateCopyConstructor feature
/// Verifies that a copy constructor is generated that copies all member values from another instance.
/// </summary>
public class CopyConstructorTests
{
    [Fact]
    public void CopyConstructor_ShouldCopyAllProperties()
    {
        var source = new PersonForCopyAndEquality
        {
            Id = 42,
            Name = "Alice",
            Email = "alice@example.com",
            Age = 30,
            BirthDate = new DateTime(1994, 6, 15)
        };
        var original = new PersonWithCopyConstructorDto(source);

        var copy = new PersonWithCopyConstructorDto(original);

        copy.Should().NotBeSameAs(original);
        copy.Id.Should().Be(42);
        copy.Name.Should().Be("Alice");
        copy.Email.Should().Be("alice@example.com");
        copy.Age.Should().Be(30);
        copy.BirthDate.Should().Be(new DateTime(1994, 6, 15));
    }

    [Fact]
    public void CopyConstructor_ShouldHandleNullableProperties()
    {
        var source = new PersonForCopyAndEquality
        {
            Id = 1,
            Name = "Bob",
            Email = "bob@example.com",
            Age = 25,
            BirthDate = null
        };
        var original = new PersonWithCopyConstructorDto(source);

        var copy = new PersonWithCopyConstructorDto(original);

        copy.BirthDate.Should().BeNull();
    }

    [Fact]
    public void CopyConstructor_ShouldCreateIndependentCopy()
    {
        var source = new PersonForCopyAndEquality
        {
            Id = 1,
            Name = "Charlie",
            Email = "charlie@example.com",
            Age = 35,
            BirthDate = new DateTime(1989, 1, 1)
        };
        var original = new PersonWithCopyConstructorDto(source);

        var copy = new PersonWithCopyConstructorDto(original);
        
        original.Name = "Changed";
        original.Age = 99;

        copy.Name.Should().Be("Charlie");
        copy.Age.Should().Be(35);
    }

    [Fact]
    public void CopyConstructor_ShouldThrowOnNull_ForClassFacets()
    {
        var act = () => new PersonWithCopyConstructorDto((PersonWithCopyConstructorDto)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CopyConstructor_ShouldWorkWithBothFeatures()
    {
        var source = new PersonForCopyAndEquality
        {
            Id = 7,
            Name = "Diana",
            Email = "diana@example.com",
            Age = 28,
            BirthDate = new DateTime(1996, 3, 20)
        };
        var original = new PersonWithCopyAndEqualityDto(source);

        var copy = new PersonWithCopyAndEqualityDto(original);

        copy.Should().Be(original);
        copy.Id.Should().Be(7);
    }

    [Fact]
    public void CopyConstructor_ShouldWorkOnStruct()
    {
        var source = new PersonForCopyAndEquality
        {
            Id = 10,
            Name = "Eve",
            Email = "eve@example.com",
            Age = 22
        };
        var original = new PersonStructWithCopyAndEquality(source);

        var copy = new PersonStructWithCopyAndEquality(original);

        copy.Id.Should().Be(10);
        copy.Name.Should().Be("Eve");
        copy.Email.Should().Be("eve@example.com");
        copy.Age.Should().Be(22);
    }
}
