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
        var source = new ModelWithListProperty
        {
            Tags = new List<string> { "tag1", "tag2" }
        };

        var dto = new RecordWithListDefault(source);

        dto.Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2" });
    }

    [Fact]
    public void RecordWithListDefault_ParameterlessConstructor_ShouldWork()
    {
        var dto = new RecordWithListDefault();

        dto.Tags.Should().BeNull();
    }

    [Fact]
    public void RecordWithListNoParameterless_ShouldConstructFromSource()
    {
        var source = new ModelWithListProperty
        {
            Tags = new List<string> { "test" }
        };

        var dto = new RecordWithListNoParameterless(source);

        dto.Tags.Should().ContainSingle().Which.Should().Be("test");
    }

    [Fact]
    public void RecordWithListNoProjection_ShouldConstructFromSource()
    {
        var source = new ModelWithListProperty
        {
            Tags = new List<string> { "a", "b", "c" }
        };

        var dto = new RecordWithListNoProjection(source);

        dto.Tags.Should().HaveCount(3);
    }

    [Fact]
    public void RecordWithMultipleProperties_ShouldWork()
    {
        var source = new ModelWithMultipleProperties
        {
            Name = "Test",
            Tags = new List<string> { "tag1", "tag2" },
            Count = 42
        };

        var dto = new RecordWithMultipleProperties(source);

        dto.Name.Should().Be("Test");
        dto.Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2" });
        dto.Count.Should().Be(42);
    }

    [Fact]
    public void RecordWithMultipleProperties_ParameterlessConstructor_ShouldWork()
    {
        var dto = new RecordWithMultipleProperties();

        dto.Name.Should().BeEmpty();
        dto.Tags.Should().BeNull();
        dto.Count.Should().Be(0);
    }

    [Fact]
    public void RecordWithNullableList_ShouldWork()
    {
        var source = new ModelWithNullableList
        {
            Tags = new List<string> { "nullable" }
        };

        var dto = new RecordWithNullableList(source);

        dto.Tags.Should().ContainSingle().Which.Should().Be("nullable");
    }

    [Fact]
    public void RecordWithNullableList_NullValue_ShouldWork()
    {
        var source = new ModelWithNullableList
        {
            Tags = null
        };

        var dto = new RecordWithNullableList(source);

        dto.Tags.Should().BeNull();
    }

    [Fact]
    public void RecordWithListDefault_WithExpression_ShouldWork()
    {
        var source = new ModelWithListProperty
        {
            Tags = new List<string> { "original" }
        };
        var dto = new RecordWithListDefault(source);

        var modified = dto with { Tags = new List<string> { "modified" } };

        modified.Tags.Should().ContainSingle().Which.Should().Be("modified");
        dto.Tags.Should().ContainSingle().Which.Should().Be("original");
    }
}
