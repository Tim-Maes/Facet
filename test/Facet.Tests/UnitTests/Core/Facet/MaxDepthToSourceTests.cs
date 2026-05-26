using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

/// <summary>
/// Tests for the <c>MaxDepthToSource</c> attribute property which limits reverse-mapping depth
/// independently of <c>MaxDepth</c> (the forward source-to-DTO direction).
/// </summary>
public class MaxDepthToSourceTests
{
    private static Author BuildAuthorWithBooks(int bookCount = 2)
    {
        var author = new Author { Id = 1, Name = "Allan Michaelsen", Books = new() };
        for (var i = 1; i <= bookCount; i++)
        {
            author.Books.Add(new Book { Id = i, Title = $"Book {i}", Author = author });
        }
        return author;
    }

    [Fact]
    public void ToFacet_Should_Map_FullDepth_When_MaxDepthToSource_Is_Set()
    {
        var author = BuildAuthorWithBooks(2);

        var dto = new AuthorFacetDepthToSource(author);

        dto.Id.Should().Be(1);
        dto.Name.Should().Be("Allan Michaelsen");
        dto.Books.Should().HaveCount(2);
        dto.Books![0].Title.Should().Be("Book 1");
    }

    [Fact]
    public void ToSource_Should_Map_TopLevel_Properties_When_MaxDepthToSource_Is_1()
    {
        var author = BuildAuthorWithBooks(2);
        var dto = new AuthorFacetDepthToSource(author);

        var entity = dto.ToSource();

        entity.Id.Should().Be(1);
        entity.Name.Should().Be("Allan Michaelsen");
    }

    [Fact]
    public void ToSource_Should_Produce_Empty_Books_Collection_When_MaxDepthToSource_Depth_Exceeded()
    {
        var author = BuildAuthorWithBooks(2);
        var dto = new AuthorFacetDepthToSource(author);

        var entity = dto.ToSource();

        entity.Books.Should().NotBeNull();
        entity.Books.Should().HaveCount(2);

        foreach (var book in entity.Books)
        {
            book.Author.Should().BeNull();
        }
    }

    [Fact]
    public void ToSource_From_Book_Should_Map_Direct_Author_But_Not_AuthorsBooks_When_MaxDepthToSource_Is_1()
    {
        var author = BuildAuthorWithBooks(1);
        var bookDto = new BookFacetDepthToSource(author.Books[0]);

        var entity = bookDto.ToSource();

        entity.Title.Should().Be("Book 1");

        entity.Author.Should().NotBeNull();
        entity.Author!.Id.Should().Be(1);
        entity.Author.Name.Should().Be("Allan Michaelsen");
        entity.Author.Books.Should().BeEmpty();
    }

    [Fact]
    public void ToSource_With_No_MaxDepthToSource_Should_Map_Nested_Objects()
    {
        var author = BuildAuthorWithBooks(1);
        var dto = new AuthorFacetWithDepth(author);

        var entity = dto.ToSource();

        entity.Books.Should().HaveCount(1);
        entity.Books[0].Title.Should().Be("Book 1");
    }

    [Fact]
    public void MaxDepthToSource_Does_Not_Affect_MaxDepth_For_ToFacet()
    {
        var author = BuildAuthorWithBooks(1);

        var withDepthToSource = new AuthorFacetDepthToSource(author);   
        var withoutDepthToSource = new AuthorFacetWithDepth(author);    

        withDepthToSource.Books.Should().HaveCount(1);
        withoutDepthToSource.Books.Should().HaveCount(1);

        var entityWithLimit = withDepthToSource.ToSource();
        entityWithLimit.Books[0].Author.Should().BeNull();
    }
}
