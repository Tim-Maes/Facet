using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for GitHub issue #251 - Nullable issues with object properties.
/// When a record facet has a single reference type property like List&lt;string&gt;,
/// the generated constructors should not cause ambiguity with the record's
/// compiler-generated copy constructor.
/// </summary>
public class RecordListPropertyTests
{
    [Fact]
    public void RecordWithListDefault_ShouldCompileAndConstruct()
    {
        // Arrange
        var source = new ModelWithListProperty
        {
            Tags = new List<string> { "tag1", "tag2" }
        };

        // Act - Should not throw ambiguous constructor error
        var dto = new RecordWithListDefault(source);

        // Assert
        dto.Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2" });
    }

    [Fact]
    public void RecordWithListDefault_ParameterlessConstructor_ShouldWork()
    {
        // Act - Should be able to call parameterless constructor without ambiguity
        var dto = new RecordWithListDefault();

        // Assert - Tags should be default(List<string>)! which is null
        // The null-forgiving operator ensures no null reference issues at compile time
        dto.Tags.Should().BeNull();
    }

    [Fact]
    public void RecordWithListNoParameterless_ShouldConstructFromSource()
    {
        // Arrange
        var source = new ModelWithListProperty
        {
            Tags = new List<string> { "test" }
        };

        // Act
        var dto = new RecordWithListNoParameterless(source);

        // Assert
        dto.Tags.Should().ContainSingle().Which.Should().Be("test");
    }

    [Fact]
    public void RecordWithListNoProjection_ShouldConstructFromSource()
    {
        // Arrange
        var source = new ModelWithListProperty
        {
            Tags = new List<string> { "a", "b", "c" }
        };

        // Act
        var dto = new RecordWithListNoProjection(source);

        // Assert
        dto.Tags.Should().HaveCount(3);
    }

    [Fact]
    public void RecordWithMultipleProperties_ShouldWork()
    {
        // Arrange
        var source = new ModelWithMultipleProperties
        {
            Name = "Test",
            Tags = new List<string> { "tag1", "tag2" },
            Count = 42
        };

        // Act
        var dto = new RecordWithMultipleProperties(source);

        // Assert
        dto.Name.Should().Be("Test");
        dto.Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2" });
        dto.Count.Should().Be(42);
    }

    [Fact]
    public void RecordWithMultipleProperties_ParameterlessConstructor_ShouldWork()
    {
        // Act
        var dto = new RecordWithMultipleProperties();

        // Assert - string gets string.Empty, List gets default, int gets 0
        dto.Name.Should().BeEmpty();
        dto.Tags.Should().BeNull();
        dto.Count.Should().Be(0);
    }

    [Fact]
    public void RecordWithNullableList_ShouldWork()
    {
        // Arrange
        var source = new ModelWithNullableList
        {
            Tags = new List<string> { "nullable" }
        };

        // Act
        var dto = new RecordWithNullableList(source);

        // Assert
        dto.Tags.Should().ContainSingle().Which.Should().Be("nullable");
    }

    [Fact]
    public void RecordWithNullableList_NullValue_ShouldWork()
    {
        // Arrange
        var source = new ModelWithNullableList
        {
            Tags = null
        };

        // Act
        var dto = new RecordWithNullableList(source);

        // Assert
        dto.Tags.Should().BeNull();
    }

    [Fact]
    public void RecordWithListDefault_WithExpression_ShouldWork()
    {
        // Arrange
        var source = new ModelWithListProperty
        {
            Tags = new List<string> { "original" }
        };
        var dto = new RecordWithListDefault(source);

        // Act - Records should support 'with' expressions
        var modified = dto with { Tags = new List<string> { "modified" } };

        // Assert
        modified.Tags.Should().ContainSingle().Which.Should().Be("modified");
        dto.Tags.Should().ContainSingle().Which.Should().Be("original");
    }
}
