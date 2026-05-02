using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for the <c>MaxDepthToSource</c> attribute property which limits reverse-mapping depth
/// independently of <c>MaxDepth</c> (the forward source-to-DTO direction).
/// </summary>
public class MaxDepthToSourceTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Author BuildAuthorWithBooks(int bookCount = 2)
    {
        var author = new Author { Id = 1, Name = "Allan Michaelsen", Books = new() };
        for (var i = 1; i <= bookCount; i++)
        {
            author.Books.Add(new Book { Id = i, Title = $"Book {i}", Author = author });
        }
        return author;
    }

    // ---------------------------------------------------------------------------
    // Forward direction (ToFacet) is unaffected by MaxDepthToSource
    // ---------------------------------------------------------------------------

    [Fact]
    public void ToFacet_Should_Map_FullDepth_When_MaxDepthToSource_Is_Set()
    {
        // Arrange
        var author = BuildAuthorWithBooks(2);

        // Act - MaxDepth = 5 so we expect full nesting (Author -> Books -> Author -> ...)
        var dto = new AuthorFacetDepthToSource(author);

        // Assert - forward mapping works normally
        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Allan Michaelsen");
        dto.Books.Should().HaveCount(2);
        dto.Books![0].Title.Should().Be("Book 1");
    }

    // ---------------------------------------------------------------------------
    // Reverse direction (ToSource) is limited by MaxDepthToSource = 1
    // ---------------------------------------------------------------------------

    [Fact]
    public void ToSource_Should_Map_TopLevel_Properties_When_MaxDepthToSource_Is_1()
    {
        // Arrange
        var author = BuildAuthorWithBooks(2);
        var dto = new AuthorFacetDepthToSource(author);

        // Act
        var entity = dto.ToSource();

        // Assert - top-level scalar properties are mapped
        entity.Id.Should().Be(1);
        entity.Name.Should().Be("Allan Michaelsen");
    }

    [Fact]
    public void ToSource_Should_Produce_Empty_Books_Collection_When_MaxDepthToSource_Depth_Exceeded()
    {
        // Arrange
        var author = BuildAuthorWithBooks(2);
        var dto = new AuthorFacetDepthToSource(author);

        // Act - MaxDepthToSource = 1: depth 0 is allowed but at depth 1 the Books children
        //       of each Book's Author are already at the limit
        var entity = dto.ToSource();

        // At depth 0 (root Author), Books is mapped
        entity.Books.Should().NotBeNull();
        entity.Books.Should().HaveCount(2);

        // Each Book's Author should be null (depth 1 == limit, nested Author at depth 1 is skipped)
        foreach (var book in entity.Books)
        {
            book.Author.Should().BeNull();
        }
    }

    [Fact]
    public void ToSource_From_Book_Should_Map_Only_TopLevel_When_MaxDepthToSource_Is_1()
    {
        // Arrange
        var author = BuildAuthorWithBooks(1);
        var bookDto = new BookFacetDepthToSource(author.Books[0]);

        // Act
        var entity = bookDto.ToSource();

        // Assert - Book's scalar properties are mapped
        entity.Title.Should().Be("Book 1");

        // At MaxDepthToSource = 1: from Book (depth 0), Author is at depth 1 == limit,
        // so Author is null
        entity.Author.Should().BeNull();
    }

    [Fact]
    public void ToSource_With_No_MaxDepthToSource_Should_Map_Nested_Objects()
    {
        // Arrange - use the existing facet that has no MaxDepthToSource (default 0 = unlimited)
        var author = BuildAuthorWithBooks(1);
        var dto = new AuthorFacetWithDepth(author);

        // Act
        var entity = dto.ToSource();

        // Assert - nested Books should be mapped (no ToSource depth limit)
        entity.Books.Should().HaveCount(1);
        entity.Books[0].Title.Should().Be("Book 1");
    }

    [Fact]
    public void MaxDepthToSource_Does_Not_Affect_MaxDepth_For_ToFacet()
    {
        // Arrange - verify the two settings are truly independent
        var author = BuildAuthorWithBooks(1);

        var withDepthToSource = new AuthorFacetDepthToSource(author);   // MaxDepth=5, MaxDepthToSource=1
        var withoutDepthToSource = new AuthorFacetWithDepth(author);    // MaxDepth=2, MaxDepthToSource=0

        // Both should have Books mapped in the forward direction
        withDepthToSource.Books.Should().HaveCount(1);
        withoutDepthToSource.Books.Should().HaveCount(1);

        // Only the one with MaxDepthToSource=1 should have nested Author dropped in ToSource
        var entityWithLimit = withDepthToSource.ToSource();
        entityWithLimit.Books[0].Author.Should().BeNull();
    }
}
