using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class CircularReferenceTests
{
    [Fact]
    public void MaxDepth_Should_Prevent_StackOverflow_With_Circular_References()
    {
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

        var facet = new AuthorFacetWithDepth(author);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("John Doe");
        facet.Books.Should().NotBeNull();
        facet.Books.Should().HaveCount(1);

        var bookFacet = facet.Books![0];
        bookFacet.Title.Should().Be("Test Book");

        bookFacet.Author.Should().NotBeNull();
        bookFacet.Author!.Id.Should().Be(1);
        bookFacet.Author.Name.Should().Be("John Doe");

        bookFacet.Author.Books.Should().BeEmpty();
    }

    [Fact]
    public void MaxDepth_Should_Handle_Multiple_Books_Per_Author()
    {
        var author = new Author
        {
            Id = 1,
            Name = "Prolific Author",
            Books = new List<Book>()
        };

        var book1 = new Book { Id = 1, Title = "Book 1", Author = author };
        var book2 = new Book { Id = 2, Title = "Book 2", Author = author };
        var book3 = new Book { Id = 3, Title = "Book 3", Author = author };

        author.Books.AddRange(new[] { book1, book2, book3 });

        var facet = new AuthorFacetWithDepth(author);

        facet.Books.Should().HaveCount(3);
        facet.Books![0].Title.Should().Be("Book 1");
        facet.Books![1].Title.Should().Be("Book 2");
        facet.Books![2].Title.Should().Be("Book 3");

        foreach (var book in facet.Books!)
        {
            book.Author.Should().NotBeNull();
            book.Author!.Name.Should().Be("Prolific Author");
            book.Author.Books.Should().BeEmpty();
        }
    }

    [Fact]
    public void PreserveReferences_Should_Detect_And_Break_Circular_References()
    {
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

        var facet = new AuthorFacetWithTracking(author);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Jane Smith");
        facet.Books.Should().NotBeNull();

        facet.Books.Should().HaveCountGreaterThanOrEqualTo(1);

        facet.Books![0].Title.Should().Match(t => t == "First Book" || t == "Second Book");

        if (facet.Books[0].Author != null)
        {
            facet.Books[0].Author.Books.Should().NotBeNull();
            facet.Books[0].Author.Books!.All(b => b.Author == null).Should().BeTrue();
        }
    }

    [Fact]
    public void PreserveReferences_Should_Handle_Same_Object_In_Multiple_Collections()
    {
        var ceo = new OrgEmployee
        {
            Id = 1,
            Name = "CEO",
            Manager = null,
            DirectReports = new List<OrgEmployee>()
        };

        var sharedEmployee = new OrgEmployee
        {
            Id = 2,
            Name = "Shared Employee",
            Manager = ceo,
            DirectReports = new List<OrgEmployee>()
        };

        ceo.DirectReports.Add(sharedEmployee);
        ceo.DirectReports.Add(sharedEmployee); 

        var facet = new OrgEmployeeFacet(ceo);

        facet.DirectReports.Should().NotBeNull();
        
        var nonNullReports = facet.DirectReports!.Where(r => r != null).ToList();
        
        nonNullReports.Count.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public void SelfReferencing_OrgEmployee_Should_Handle_Hierarchy_Without_StackOverflow()
    {
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

        var facet = new OrgEmployeeFacet(ceo);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("CEO");
        facet.Manager.Should().BeNull();
        facet.DirectReports.Should().NotBeNull();

        facet.DirectReports.Should().HaveCountGreaterThanOrEqualTo(1);

        var directorFacet = facet.DirectReports!.FirstOrDefault(e => e.Name == "Director");
        directorFacet.Should().NotBeNull();
        directorFacet!.Name.Should().Be("Director");

    }

    [Fact]
    public void SelfReferencing_Should_Handle_Manager_Pointing_Up()
    {
        var employee = new OrgEmployee
        {
            Id = 1,
            Name = "Employee",
            DirectReports = new List<OrgEmployee>()
        };

        var manager = new OrgEmployee
        {
            Id = 2,
            Name = "Manager",
            DirectReports = new List<OrgEmployee> { employee }
        };

        var director = new OrgEmployee
        {
            Id = 3,
            Name = "Director",
            DirectReports = new List<OrgEmployee> { manager }
        };

        employee.Manager = manager;
        manager.Manager = director;
        director.Manager = null;

        var facet = new OrgEmployeeFacet(employee);

        facet.Name.Should().Be("Employee");
        facet.Manager.Should().NotBeNull();
        facet.Manager!.Name.Should().Be("Manager");
        facet.Manager.Manager.Should().NotBeNull();
        facet.Manager.Manager!.Name.Should().Be("Director");
    }

    [Fact]
    public void CircularReference_Should_Handle_Null_Collections()
    {
        var author = new Author
        {
            Id = 1,
            Name = "Author Without Books",
            Books = new List<Book>()
        };

        var facet = new AuthorFacetWithDepth(author);

        facet.Should().NotBeNull();
        facet.Books.Should().NotBeNull();
        facet.Books.Should().BeEmpty();
    }

    [Fact]
    public void CircularReference_Should_Handle_Empty_DirectReports()
    {
        var employee = new OrgEmployee
        {
            Id = 1,
            Name = "Solo Employee",
            Manager = null,
            DirectReports = new List<OrgEmployee>()
        };

        var facet = new OrgEmployeeFacet(employee);

        facet.DirectReports.Should().NotBeNull();
        facet.DirectReports.Should().BeEmpty();
    }

    [Fact]
    public void CircularReference_Should_Handle_Single_Element_Cycle()
    {
        var employee = new OrgEmployee
        {
            Id = 1,
            Name = "Self-Managed",
            DirectReports = new List<OrgEmployee>()
        };

        employee.Manager = employee;
        employee.DirectReports.Add(employee);

        var facet = new OrgEmployeeFacet(employee);

        facet.Should().NotBeNull();
        facet.Name.Should().Be("Self-Managed");
    }

    [Fact]
    public void CircularReference_Should_Handle_Complex_Graphs()
    {
        var author1 = new Author { Id = 1, Name = "Author 1", Books = new List<Book>() };
        var author2 = new Author { Id = 2, Name = "Author 2", Books = new List<Book>() };

        var sharedBook = new Book { Id = 1, Title = "Shared Book", Author = author1 };

        author1.Books.Add(sharedBook);
        author2.Books.Add(sharedBook); 

        var facet1 = new AuthorFacetWithTracking(author1);
        var facet2 = new AuthorFacetWithTracking(author2);

        facet1.Should().NotBeNull();
        facet2.Should().NotBeNull();
        facet1.Books.Should().HaveCount(1);
        facet2.Books.Should().HaveCount(1);
    }

    [Fact]
    public void CircularReference_Should_Handle_Null_Navigation_Properties()
    {
        var book = new Book
        {
            Id = 1,
            Title = "Standalone Book",
            Author = null
        };

        var author = new Author
        {
            Id = 1,
            Name = "Author",
            Books = new List<Book> { book }
        };

        var facet = new AuthorFacetWithDepth(author);

        facet.Books.Should().HaveCount(1);
        facet.Books![0].Title.Should().Be("Standalone Book");
        facet.Books[0].Author.Should().BeNull();
    }

    [Fact]
    public void MaxDepth_And_PreserveReferences_Should_Work_Together()
    {
        var author = new Author
        {
            Id = 1,
            Name = "Author",
            Books = new List<Book>()
        };

        var book1 = new Book { Id = 1, Title = "Book 1", Author = author };
        var book2 = new Book { Id = 2, Title = "Book 2", Author = author };

        author.Books.Add(book1);
        author.Books.Add(book2);
        author.Books.Add(book1); 

        var facet = new AuthorFacetWithTracking(author);

        facet.Should().NotBeNull();
        facet.Books.Should().NotBeNull();
        
        var nonNullBooks = facet.Books!.Where(b => b != null).ToList();
        nonNullBooks.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void Deep_Hierarchy_With_Reference_Tracking_Should_Not_Overflow()
    {
        OrgEmployee? current = null;
        OrgEmployee? root = null;

        for (int i = 10; i >= 1; i--)
        {
            var employee = new OrgEmployee
            {
                Id = i,
                Name = $"Level {i}",
                Manager = current,
                DirectReports = new List<OrgEmployee>()
            };

            if (current != null)
            {
                current.DirectReports.Add(employee);
            }

            if (i == 1)
            {
                root = employee;
            }

            current = employee;
        }

        var facet = new OrgEmployeeFacet(root!);

        facet.Should().NotBeNull();
        facet.Name.Should().Be("Level 1");
    }

    [Fact]
    public void Collection_With_Circular_References_Should_Map_Correctly()
    {
        var authors = new List<Author>();

        var author1 = new Author { Id = 1, Name = "Author 1", Books = new List<Book>() };
        var author2 = new Author { Id = 2, Name = "Author 2", Books = new List<Book>() };

        var book1 = new Book { Id = 1, Title = "Book 1", Author = author1 };
        var book2 = new Book { Id = 2, Title = "Book 2", Author = author2 };

        author1.Books.Add(book1);
        author2.Books.Add(book2);

        authors.Add(author1);
        authors.Add(author2);

        var facets = authors.Select(a => new AuthorFacetWithDepth(a)).ToList();

        facets.Should().HaveCount(2);
        facets[0].Name.Should().Be("Author 1");
        facets[1].Name.Should().Be("Author 2");
        facets[0].Books.Should().HaveCount(1);
        facets[1].Books.Should().HaveCount(1);
    }

    [Fact]
    public void Empty_Collection_Should_Not_Cause_Issues()
    {
        var employee = new OrgEmployee
        {
            Id = 1,
            Name = "Manager",
            DirectReports = new List<OrgEmployee>()
        };

        var facet = new OrgEmployeeFacet(employee);

        facet.DirectReports.Should().NotBeNull();
        facet.DirectReports.Should().BeEmpty();
    }

    [Fact]
    public void ToSource_OnAuthorFacetMaxDepth1_ShouldNotThrow()
    {
        var author = new Author
        {
            Id = 1,
            Name = "Test Author",
            Books = new List<Book>()
        };
        var book = new Book { Id = 1, Title = "Some Book", Author = author };
        author.Books.Add(book);

        var facet = new AuthorFacetMaxDepth1(author);

        var mappedAuthor = facet.ToSource();

        mappedAuthor.Should().NotBeNull();
        mappedAuthor.Id.Should().Be(1);
        mappedAuthor.Name.Should().Be("Test Author");
    }

    [Fact]
    public void ToSource_OnBookFacetMaxDepth1_ShouldNotThrow()
    {
        var author = new Author
        {
            Id = 3,
            Name = "Some Author",
            Books = new List<Book>()
        };
        var book = new Book { Id = 2, Title = "Deep Dive", Author = author };
        author.Books.Add(book);

        var facet = new BookFacetMaxDepth1(book);

        var mappedBook = facet.ToSource();

        mappedBook.Should().NotBeNull();
        mappedBook.Id.Should().Be(2);
        mappedBook.Title.Should().Be("Deep Dive");
    }

    [Fact]
    public void BackTo_Should_Handle_Circular_References_Without_Error()
    {
        var author = new Author
        {
            Id = 1,
            Name = "Test Author",
            Books = new List<Book>()
        };

        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Author = author
        };

        author.Books.Add(book);

        var facet = new AuthorFacetWithDepth(author);

        var mappedAuthor = facet.ToSource();

        mappedAuthor.Should().NotBeNull();
        mappedAuthor.Id.Should().Be(1);
        mappedAuthor.Name.Should().Be("Test Author");
        mappedAuthor.Books.Should().NotBeNull();
    }

    [Fact]
    public void CircularReference_Detection_Should_Be_Fast_For_Large_Graphs()
    {
        var root = new OrgEmployee
        {
            Id = 1,
            Name = "Root",
            DirectReports = new List<OrgEmployee>()
        };

        CreateOrgHierarchy(root, depth: 0, maxDepth: 2, childrenPerLevel: 5);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var facet = new OrgEmployeeFacet(root);
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
        facet.Should().NotBeNull();
    }

    private void CreateOrgHierarchy(OrgEmployee parent, int depth, int maxDepth, int childrenPerLevel)
    {
        if (depth >= maxDepth) return;

        for (int i = 0; i < childrenPerLevel; i++)
        {
            var child = new OrgEmployee
            {
                Id = parent.Id * 10 + i,
                Name = $"Employee {parent.Id}-{i}",
                Manager = parent,
                DirectReports = new List<OrgEmployee>()
            };

            parent.DirectReports.Add(child);
            CreateOrgHierarchy(child, depth + 1, maxDepth, childrenPerLevel);
        }
    }

    [Fact]
    public void DefaultSettings_Should_Prevent_StackOverflow_With_Bidirectional_References()
    {
        var lookup = new CircularLookup
        {
            Id = 1,
            Value = "en-US",
            Identifier = new CircularIdentifier
            {
                Id = 1,
                Name = "LanguageCode",
                Lookups = new List<CircularLookup>()
            }
        };

        lookup.Identifier.Lookups.Add(lookup);

        var facet = new CircularLookupDefaultDto(lookup);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Value.Should().Be("en-US");
        facet.Identifier.Should().NotBeNull();
        facet.Identifier!.Name.Should().Be("LanguageCode");

        facet.Identifier.Lookups.Should().NotBeNull();
    }

    [Fact]
    public void DefaultSettings_Should_Handle_Deep_Nesting_Up_To_MaxDepth()
    {
        var level1 = new OrgEmployee { Id = 1, Name = "Level 1", DirectReports = new List<OrgEmployee>() };
        var level2 = new OrgEmployee { Id = 2, Name = "Level 2", Manager = level1, DirectReports = new List<OrgEmployee>() };
        var level3 = new OrgEmployee { Id = 3, Name = "Level 3", Manager = level2, DirectReports = new List<OrgEmployee>() };
        var level4 = new OrgEmployee { Id = 4, Name = "Level 4", Manager = level3, DirectReports = new List<OrgEmployee>() };
        var level5 = new OrgEmployee { Id = 5, Name = "Level 5", Manager = level4, DirectReports = new List<OrgEmployee>() };

        level1.DirectReports.Add(level2);
        level2.DirectReports.Add(level3);
        level3.DirectReports.Add(level4);
        level4.DirectReports.Add(level5);

        var facet = new OrgEmployeeDefaultFacet(level1);

        facet.Should().NotBeNull();
        facet.Name.Should().Be("Level 1");
        facet.DirectReports.Should().NotBeNull();
        facet.DirectReports.Should().HaveCount(1);

        var level2Facet = facet.DirectReports![0];
        level2Facet.Should().NotBeNull();
        level2Facet.Name.Should().Be("Level 2");
        level2Facet.DirectReports.Should().NotBeNull();
        level2Facet.DirectReports.Should().HaveCount(1);

        var level3Facet = level2Facet.DirectReports![0];
        level3Facet.Should().NotBeNull();
        level3Facet.Name.Should().Be("Level 3");

        level2Facet.Manager.Should().BeNull(); 
        level3Facet.Manager.Should().BeNull(); 
    }

    [Fact]
    public void LeafFacet_Without_NestedFacets_Should_Work_As_NestedFacet()
    {
        var parent = new ParentWithLeaf
        {
            Id = 1,
            Name = "Parent",
            Leaf = new SimpleLeaf { Id = 2, Value = "Leaf Value" }
        };

        var facet = new ParentWithLeafDto(parent);

        facet.Should().NotBeNull();
        facet.Name.Should().Be("Parent");
        facet.Leaf.Should().NotBeNull();
        facet.Leaf!.Value.Should().Be("Leaf Value");
    }

    [Fact]
    public void MixedSettings_ExplicitAndDefault_Should_WorkTogether()
    {
        var author = new Author
        {
            Id = 1,
            Name = "Mixed Settings Author",
            Books = new List<Book>()
        };

        var book = new Book
        {
            Id = 1,
            Title = "Mixed Settings Book",
            Author = author
        };

        author.Books.Add(book);

        var facet = new MixedSettingsAuthorDto(author);

        facet.Should().NotBeNull();
        facet.Name.Should().Be("Mixed Settings Author");
        facet.Books.Should().NotBeNull();
        facet.Books.Should().HaveCount(1);
    }

    [Fact]
    public void SharedReference_In_Collection_Should_Be_Tracked()
    {
        var sharedAuthor = new Author
        {
            Id = 1,
            Name = "Shared Author",
            Books = new List<Book>()
        };

        var book1 = new Book { Id = 1, Title = "Book 1", Author = sharedAuthor };
        var book2 = new Book { Id = 2, Title = "Book 2", Author = sharedAuthor };

        sharedAuthor.Books.AddRange(new[] { book1, book2 });

        var authors = new List<Author> { sharedAuthor, sharedAuthor };

        var facets = authors.Select(a => new AuthorDefaultDto(a)).ToList();

        facets.Should().HaveCount(2);
        facets[0].Name.Should().Be("Shared Author");
        facets[1].Name.Should().Be("Shared Author");
    }

    [Fact]
    public void ComplexGraph_With_Multiple_Circular_Paths_Should_Not_Overflow()
    {
        var centralNode = new OrgEmployee
        {
            Id = 1,
            Name = "Central",
            DirectReports = new List<OrgEmployee>()
        };

        var node2 = new OrgEmployee
        {
            Id = 2,
            Name = "Node 2",
            Manager = centralNode,
            DirectReports = new List<OrgEmployee>()
        };

        var node3 = new OrgEmployee
        {
            Id = 3,
            Name = "Node 3",
            Manager = centralNode,
            DirectReports = new List<OrgEmployee>()
        };

        centralNode.DirectReports.Add(node2);
        centralNode.DirectReports.Add(node3);
        node2.DirectReports.Add(node3);
        node3.DirectReports.Add(node2);
        node2.DirectReports.Add(centralNode); 

        var facet = new OrgEmployeeDefaultFacet(centralNode);

        facet.Should().NotBeNull();
        facet.Name.Should().Be("Central");
        facet.DirectReports.Should().NotBeNull();
    }

    [Fact]
    public void ZeroMaxDepth_Should_Still_Work_For_NonRecursive_Structures()
    {
        var leaf = new SimpleLeaf { Id = 1, Value = "Simple" };
        var parent = new ParentWithLeaf { Id = 2, Name = "Parent", Leaf = leaf };

        var facet = new ParentWithLeafNoDepthDto(parent);

        facet.Should().NotBeNull();
        facet.Name.Should().Be("Parent");
    }

    [Fact]
    public void DefaultSettings_Should_Allow_Constructor_Chaining()
    {
        var author = new Author
        {
            Id = 1,
            Name = "Constructor Chain Test",
            Books = new List<Book>()
        };

        var facet1 = new AuthorDefaultDto(author);

        var facet2 = new AuthorDefaultDto(author);

        facet1.Should().NotBeNull();
        facet2.Should().NotBeNull();
        facet1.Should().NotBeSameAs(facet2);
        facet1.Name.Should().Be(facet2.Name);
    }

    [Fact]
    public void TripleNestedCircular_Reference_Should_Be_Handled()
    {
        var entityA = new TripleCircularA
        {
            Id = 1,
            Name = "Entity A",
            BReferences = new List<TripleCircularB>()
        };

        var entityB = new TripleCircularB
        {
            Id = 2,
            Name = "Entity B",
            A = entityA,
            CReferences = new List<TripleCircularC>()
        };

        var entityC = new TripleCircularC
        {
            Id = 3,
            Name = "Entity C",
            B = entityB,
            A = entityA
        };

        entityA.BReferences.Add(entityB);
        entityB.CReferences.Add(entityC);

        var facet = new TripleCircularADto(entityA);

        facet.Should().NotBeNull();
        facet.Name.Should().Be("Entity A");
        facet.BReferences.Should().NotBeNull();
    }

    [Fact]
    public void DefaultMaxDepth_Should_Handle_CircularReferences_Without_Explicit_MaxDepth()
    {
        var author = new Author
        {
            Id = 1,
            Name = "Default Depth Author",
            Books = new List<Book>()
        };
        var book = new Book { Id = 1, Title = "Default Book", Author = author };
        author.Books.Add(book);

        var facet = new AuthorFacetDefault(author);

        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Name.Should().Be("Default Depth Author");
        facet.Books.Should().NotBeNull();
        facet.Books.Should().HaveCount(1);
        facet.Books![0].Title.Should().Be("Default Book");

        facet.Books[0].Author.Should().BeNull(
            "same Author instance is already being processed; PreserveReferences prevents re-entry");
    }

    [Fact]
    public void DefaultMaxDepth_Should_Allow_Deep_NonCircular_Nesting_Beyond_Three()
    {
        var source = new Level0
        {
            Name = "L0",
            Child = new Level1
            {
                Name = "L1",
                Child = new Level2
                {
                    Name = "L2",
                    Child = new Level3
                    {
                        Name = "L3",
                        Child = new Level4 { Name = "L4" }
                    }
                }
            }
        };

        var facet = new Level0Facet(source);

        facet.Name.Should().Be("L0");
        facet.Child.Should().NotBeNull();
        facet.Child!.Name.Should().Be("L1");
        facet.Child.Child.Should().NotBeNull();
        facet.Child.Child!.Name.Should().Be("L2");
        facet.Child.Child.Child.Should().NotBeNull();
        facet.Child.Child.Child!.Name.Should().Be("L3");
        facet.Child.Child.Child.Child.Should().NotBeNull();
        facet.Child.Child.Child.Child!.Name.Should().Be("L4");
    }
}

#region Additional Test Models for New Tests

public class CircularLookup
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public CircularIdentifier Identifier { get; set; } = null!;
}

public class CircularIdentifier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<CircularLookup> Lookups { get; set; } = new();
}

[Facet(typeof(CircularIdentifier), NestedFacets = [typeof(CircularLookupDefaultDto)])]
public partial record CircularIdentifierDefaultDto;

[Facet(typeof(CircularLookup), NestedFacets = [typeof(CircularIdentifierDefaultDto)])]
public partial record CircularLookupDefaultDto;

public class SimpleLeaf
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
}

public class ParentWithLeaf
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SimpleLeaf? Leaf { get; set; }
}

[Facet(typeof(SimpleLeaf))]
public partial record SimpleLeafDto;

[Facet(typeof(ParentWithLeaf), NestedFacets = [typeof(SimpleLeafDto)])]
public partial record ParentWithLeafDto;

[Facet(typeof(SimpleLeaf), MaxDepth = 0, PreserveReferences = false)]
public partial record SimpleLeafNoDepthDto;

[Facet(typeof(ParentWithLeaf), MaxDepth = 0, PreserveReferences = false, NestedFacets = [typeof(SimpleLeafNoDepthDto)])]
public partial record ParentWithLeafNoDepthDto;

[Facet(typeof(OrgEmployee), PreserveReferences = true, NestedFacets = [typeof(OrgEmployeeDefaultFacet)])]
public partial record OrgEmployeeDefaultFacet;

[Facet(typeof(Author), PreserveReferences = true, NestedFacets = [typeof(BookDefaultDto)])]
public partial record AuthorDefaultDto;

[Facet(typeof(Book), PreserveReferences = true, NestedFacets = [typeof(AuthorDefaultDto)])]
public partial record BookDefaultDto;

[Facet(typeof(Author), MaxDepth = 5, PreserveReferences = true, NestedFacets = [typeof(BookDefaultDto)])]
public partial record MixedSettingsAuthorDto;

public class TripleCircularA
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<TripleCircularB> BReferences { get; set; } = new();
}

public class TripleCircularB
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TripleCircularA? A { get; set; }
    public List<TripleCircularC> CReferences { get; set; } = new();
}

public class TripleCircularC
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TripleCircularB? B { get; set; }
    public TripleCircularA? A { get; set; }
}

[Facet(typeof(TripleCircularA), NestedFacets = [typeof(TripleCircularBDto)])]
public partial record TripleCircularADto;

[Facet(typeof(TripleCircularB), NestedFacets = [typeof(TripleCircularADto), typeof(TripleCircularCDto)])]
public partial record TripleCircularBDto;

[Facet(typeof(TripleCircularC), NestedFacets = [typeof(TripleCircularBDto), typeof(TripleCircularADto)])]
public partial record TripleCircularCDto;

#endregion
