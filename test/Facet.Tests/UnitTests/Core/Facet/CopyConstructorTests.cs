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
        // Arrange
        var source = new PersonForCopyAndEquality
        {
            Id = 42,
            Name = "Alice",
            Email = "alice@example.com",
            Age = 30,
            BirthDate = new DateTime(1994, 6, 15)
        };
        var original = new PersonWithCopyConstructorDto(source);

        // Act
        var copy = new PersonWithCopyConstructorDto(original);

        // Assert
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
        // Arrange
        var source = new PersonForCopyAndEquality
        {
            Id = 1,
            Name = "Bob",
            Email = "bob@example.com",
            Age = 25,
            BirthDate = null
        };
        var original = new PersonWithCopyConstructorDto(source);

        // Act
        var copy = new PersonWithCopyConstructorDto(original);

        // Assert
        copy.BirthDate.Should().BeNull();
    }

    [Fact]
    public void CopyConstructor_ShouldCreateIndependentCopy()
    {
        // Arrange
        var source = new PersonForCopyAndEquality
        {
            Id = 1,
            Name = "Charlie",
            Email = "charlie@example.com",
            Age = 35,
            BirthDate = new DateTime(1989, 1, 1)
        };
        var original = new PersonWithCopyConstructorDto(source);

        // Act
        var copy = new PersonWithCopyConstructorDto(original);
        // Modify the original — the copy should remain unchanged
        original.Name = "Changed";
        original.Age = 99;

        // Assert
        copy.Name.Should().Be("Charlie");
        copy.Age.Should().Be(35);
    }

    [Fact]
    public void CopyConstructor_ShouldThrowOnNull_ForClassFacets()
    {
        // Act & Assert
        var act = () => new PersonWithCopyConstructorDto((PersonWithCopyConstructorDto)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CopyConstructor_ShouldWorkWithBothFeatures()
    {
        // Arrange — facet with both GenerateCopyConstructor and GenerateEquality
        var source = new PersonForCopyAndEquality
        {
            Id = 7,
            Name = "Diana",
            Email = "diana@example.com",
            Age = 28,
            BirthDate = new DateTime(1996, 3, 20)
        };
        var original = new PersonWithCopyAndEqualityDto(source);

        // Act
        var copy = new PersonWithCopyAndEqualityDto(original);

        // Assert — copy should equal the original
        copy.Should().Be(original);
        copy.Id.Should().Be(7);
    }

    [Fact]
    public void CopyConstructor_ShouldWorkOnStruct()
    {
        // Arrange
        var source = new PersonForCopyAndEquality
        {
            Id = 10,
            Name = "Eve",
            Email = "eve@example.com",
            Age = 22
        };
        var original = new PersonStructWithCopyAndEquality(source);

        // Act
        var copy = new PersonStructWithCopyAndEquality(original);

        // Assert
        copy.Id.Should().Be(10);
        copy.Name.Should().Be("Eve");
        copy.Email.Should().Be("eve@example.com");
        copy.Age.Should().Be(22);
    }
}
