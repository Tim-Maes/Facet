using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class CircularReferenceTests
{
    [Fact]
    public void MaxDepth_Should_Prevent_StackOverflow_With_Circular_References()
    {
        // Arrange - Create circular reference: Author -> Book -> Author
        var author = new Author
        {
            Id = 1,
            Name = "John Doe",
            Books = new List<Book>()
        };

        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Author = author
        };

        author.Books.Add(book);

        // Act - This should not cause stack overflow
        var facet = new AuthorFacetWithDepth(author);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("John Doe");
        facet.Books.Should().NotBeNull();
        facet.Books.Should().HaveCount(1);

        var bookFacet = facet.Books![0];
        bookFacet.Title.Should().Be("Test Book");

        // At MaxDepth = 2, we allow 2 levels: Author -> Book -> Author
        // But the nested Author cannot have Books (that would be level 3)
        bookFacet.Author.Should().NotBeNull();
        bookFacet.Author!.Id.Should().Be(1);
        bookFacet.Author.Name.Should().Be("John Doe");

        // At depth 2, Books is cut off to prevent going to level 3
        bookFacet.Author.Books.Should().BeNull();
    }

    [Fact]
    public void PreserveReferences_Should_Detect_And_Break_Circular_References()
    {
        // Arrange - Create circular reference
        var author = new Author
        {
            Id = 1,
            Name = "Jane Smith",
            Books = new List<Book>()
        };

        var book1 = new Book
        {
            Id = 1,
            Title = "First Book",
            Author = author
        };

        var book2 = new Book
        {
            Id = 2,
            Title = "Second Book",
            Author = author
        };

        author.Books.Add(book1);
        author.Books.Add(book2);

        // Act - PreserveReferences should detect we're processing the same author twice
        var facet = new AuthorFacetWithTracking(author);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Jane Smith");
        facet.Books.Should().NotBeNull();

        facet.Books.Should().HaveCountGreaterThanOrEqualTo(1);

        // The first book should be created with nested author data
        facet.Books![0].Title.Should().Match(t => t == "First Book" || t == "Second Book");

        if (facet.Books[0].Author != null)
        {
            facet.Books[0].Author.Books.Should().NotBeNull();
            facet.Books[0].Author.Books!.All(b => b.Author == null).Should().BeTrue();
        }
    }

    [Fact]
    public void SelfReferencing_OrgEmployee_Should_Handle_Hierarchy_Without_StackOverflow()
    {
        // Arrange - Create employee hierarchy with circular reference
        var ceo = new OrgEmployee
        {
            Id = 1,
            Name = "CEO",
            Manager = null,
            DirectReports = new List<OrgEmployee>()
        };

        var director = new OrgEmployee
        {
            Id = 2,
            Name = "Director",
            Manager = ceo,
            DirectReports = new List<OrgEmployee>()
        };

        var manager = new OrgEmployee
        {
            Id = 3,
            Name = "Manager",
            Manager = director,
            DirectReports = new List<OrgEmployee>()
        };

        var employee = new OrgEmployee
        {
            Id = 4,
            Name = "Employee",
            Manager = manager,
            DirectReports = new List<OrgEmployee>()
        };

        ceo.DirectReports.Add(director);
        director.DirectReports.Add(manager);
        manager.DirectReports.Add(employee);

        ceo.DirectReports.Add(employee);

        // Act - Should not cause stack overflow
        var facet = new OrgEmployeeFacet(ceo);

        // Assert - No stack overflow occurred, that's the main succces
        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("CEO");
        facet.Manager.Should().BeNull();
        facet.DirectReports.Should().NotBeNull();

        facet.DirectReports.Should().HaveCountGreaterThanOrEqualTo(1);

        // Verify we have at least the director
        var directorFacet = facet.DirectReports!.FirstOrDefault(e => e.Name == "Director");
        directorFacet.Should().NotBeNull();
        directorFacet!.Name.Should().Be("Director");

        // The important thing is no stack overflow occurred
    }
}
