using Facet.Tests.TestModels;

namespace Facet.Tests.UnitTests.Core.Facet;

public class CircularReferenceTests
{
    #region MaxDepth Tests

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
    public void MaxDepth_Should_Handle_Multiple_Books_Per_Author()
    {
        // Arrange
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

        // Act
        var facet = new AuthorFacetWithDepth(author);

        // Assert
        facet.Books.Should().HaveCount(3);
        facet.Books![0].Title.Should().Be("Book 1");
        facet.Books![1].Title.Should().Be("Book 2");
        facet.Books![2].Title.Should().Be("Book 3");

        // Each book should have the author, but the author's books should be null (depth limit)
        foreach (var book in facet.Books!)
        {
            book.Author.Should().NotBeNull();
            book.Author!.Name.Should().Be("Prolific Author");
            book.Author.Books.Should().BeNull();
        }
    }

    #endregion

    #region PreserveReferences Tests

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
    public void PreserveReferences_Should_Handle_Same_Object_In_Multiple_Collections()
    {
        // Arrange - Same employee appears in multiple places
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

        // Add the same employee twice (simulating a bug or complex graph)
        ceo.DirectReports.Add(sharedEmployee);
        ceo.DirectReports.Add(sharedEmployee); // Duplicate reference

        // Act
        var facet = new OrgEmployeeFacet(ceo);

        // Assert
        facet.DirectReports.Should().NotBeNull();
        
        // With PreserveReferences, the second occurrence should be filtered out or nulled
        var nonNullReports = facet.DirectReports!.Where(r => r != null).ToList();
        
        // Should have at most 1 instance of the shared employee
        nonNullReports.Count.Should().BeLessThanOrEqualTo(1);
    }

    #endregion

    #region Self-Referencing Tests

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

    [Fact]
    public void SelfReferencing_Should_Handle_Manager_Pointing_Up()
    {
        // Arrange - Create hierarchy where we walk up through managers
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

        // Act - Start from employee and walk up
        var facet = new OrgEmployeeFacet(employee);

        // Assert
        facet.Name.Should().Be("Employee");
        facet.Manager.Should().NotBeNull();
        facet.Manager!.Name.Should().Be("Manager");
        facet.Manager.Manager.Should().NotBeNull();
        facet.Manager.Manager!.Name.Should().Be("Director");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CircularReference_Should_Handle_Null_Collections()
    {
        // Arrange - Author with no books
        var author = new Author
        {
            Id = 1,
            Name = "Author Without Books",
            Books = new List<Book>()
        };

        // Act
        var facet = new AuthorFacetWithDepth(author);

        // Assert
        facet.Should().NotBeNull();
        facet.Books.Should().NotBeNull();
        facet.Books.Should().BeEmpty();
    }

    [Fact]
    public void CircularReference_Should_Handle_Empty_DirectReports()
    {
        // Arrange - Employee with no reports
        var employee = new OrgEmployee
        {
            Id = 1,
            Name = "Solo Employee",
            Manager = null,
            DirectReports = new List<OrgEmployee>()
        };

        // Act
        var facet = new OrgEmployeeFacet(employee);

        // Assert
        facet.DirectReports.Should().NotBeNull();
        facet.DirectReports.Should().BeEmpty();
    }

    [Fact]
    public void CircularReference_Should_Handle_Single_Element_Cycle()
    {
        // Arrange - Employee is their own manager (weird but possible in bad data)
        var employee = new OrgEmployee
        {
            Id = 1,
            Name = "Self-Managed",
            DirectReports = new List<OrgEmployee>()
        };

        employee.Manager = employee;
        employee.DirectReports.Add(employee);

        // Act - Should not hang
        var facet = new OrgEmployeeFacet(employee);

        // Assert - Should complete without error
        facet.Should().NotBeNull();
        facet.Name.Should().Be("Self-Managed");
    }

    [Fact]
    public void CircularReference_Should_Handle_Complex_Graphs()
    {
        // Arrange - Multiple authors sharing books
        var author1 = new Author { Id = 1, Name = "Author 1", Books = new List<Book>() };
        var author2 = new Author { Id = 2, Name = "Author 2", Books = new List<Book>() };

        var sharedBook = new Book { Id = 1, Title = "Shared Book", Author = author1 };

        author1.Books.Add(sharedBook);
        author2.Books.Add(sharedBook); // Same book instance

        // Act
        var facet1 = new AuthorFacetWithTracking(author1);
        var facet2 = new AuthorFacetWithTracking(author2);

        // Assert - Both should succeed
        facet1.Should().NotBeNull();
        facet2.Should().NotBeNull();
        facet1.Books.Should().HaveCount(1);
        facet2.Books.Should().HaveCount(1);
    }

    [Fact]
    public void CircularReference_Should_Handle_Null_Navigation_Properties()
    {
        // Arrange
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

        // Act
        var facet = new AuthorFacetWithDepth(author);

        // Assert
        facet.Books.Should().HaveCount(1);
        facet.Books![0].Title.Should().Be("Standalone Book");
        facet.Books[0].Author.Should().BeNull();
    }

    #endregion

    #region Combined MaxDepth and PreserveReferences Tests

    [Fact]
    public void MaxDepth_And_PreserveReferences_Should_Work_Together()
    {
        // Arrange - Create complex circular structure
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
        author.Books.Add(book1); // Duplicate reference

        // Act - Both MaxDepth and PreserveReferences should apply
        var facet = new AuthorFacetWithTracking(author);

        // Assert
        facet.Should().NotBeNull();
        facet.Books.Should().NotBeNull();
        
        // Should handle both depth limiting and reference tracking
        var nonNullBooks = facet.Books!.Where(b => b != null).ToList();
        nonNullBooks.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void Deep_Hierarchy_With_Reference_Tracking_Should_Not_Overflow()
    {
        // Arrange - Create 10-level deep hierarchy
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

        // Act - MaxDepth = 5 and PreserveReferences = true
        var facet = new OrgEmployeeFacet(root!);

        // Assert - Should complete without overflow
        facet.Should().NotBeNull();
        facet.Name.Should().Be("Level 1");
    }

    #endregion

    #region Collection Mapping Tests

    [Fact]
    public void Collection_With_Circular_References_Should_Map_Correctly()
    {
        // Arrange
        var authors = new List<Author>();

        var author1 = new Author { Id = 1, Name = "Author 1", Books = new List<Book>() };
        var author2 = new Author { Id = 2, Name = "Author 2", Books = new List<Book>() };

        var book1 = new Book { Id = 1, Title = "Book 1", Author = author1 };
        var book2 = new Book { Id = 2, Title = "Book 2", Author = author2 };

        author1.Books.Add(book1);
        author2.Books.Add(book2);

        authors.Add(author1);
        authors.Add(author2);

        // Act
        var facets = authors.Select(a => new AuthorFacetWithDepth(a)).ToList();

        // Assert
        facets.Should().HaveCount(2);
        facets[0].Name.Should().Be("Author 1");
        facets[1].Name.Should().Be("Author 2");
        facets[0].Books.Should().HaveCount(1);
        facets[1].Books.Should().HaveCount(1);
    }

    [Fact]
    public void Empty_Collection_Should_Not_Cause_Issues()
    {
        // Arrange
        var employee = new OrgEmployee
        {
            Id = 1,
            Name = "Manager",
            DirectReports = new List<OrgEmployee>()
        };

        // Act
        var facet = new OrgEmployeeFacet(employee);

        // Assert
        facet.DirectReports.Should().NotBeNull();
        facet.DirectReports.Should().BeEmpty();
    }

    #endregion

    #region BackTo Tests with Circular References

    [Fact]
    public void BackTo_Should_Handle_Circular_References_Without_Error()
    {
        // Arrange
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

        // Act - ToSource should work even with circular references
        var mappedAuthor = facet.ToSource();

        // Assert
        mappedAuthor.Should().NotBeNull();
        mappedAuthor.Id.Should().Be(1);
        mappedAuthor.Name.Should().Be("Test Author");
        mappedAuthor.Books.Should().NotBeNull();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void CircularReference_Detection_Should_Be_Fast_For_Large_Graphs()
    {
        // Arrange - Create large org hierarchy
        var root = new OrgEmployee
        {
            Id = 1,
            Name = "Root",
            DirectReports = new List<OrgEmployee>()
        };

        // Create 3 levels with 5 children each = 1 + 5 + 25 = 31 employees
        CreateOrgHierarchy(root, depth: 0, maxDepth: 2, childrenPerLevel: 5);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var facet = new OrgEmployeeFacet(root);
        stopwatch.Stop();

        // Assert - Should complete quickly (under 1 second)
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

    #endregion

    #region Default Settings Tests (MaxDepth=3, PreserveReferences=true)

    [Fact]
    public void DefaultSettings_Should_Prevent_StackOverflow_With_Bidirectional_References()
    {
        // Arrange - Simulate the user's StringLookup/StringIdentifier scenario
        // This tests the fix for the reported issue where users were getting constructor errors
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

        // Act - This should work with default settings (MaxDepth=3, PreserveReferences=true)
        var facet = new CircularLookupDefaultDto(lookup);

        // Assert
        facet.Should().NotBeNull();
        facet.Id.Should().Be(1);
        facet.Value.Should().Be("en-US");
        facet.Identifier.Should().NotBeNull();
        facet.Identifier!.Name.Should().Be("LanguageCode");

        // With PreserveReferences=true (default), the circular reference should be handled
        // The CircularLookup should appear in the CircularIdentifier's collection
        facet.Identifier.Lookups.Should().NotBeNull();
    }

    [Fact]
    public void DefaultSettings_Should_Handle_Deep_Nesting_Up_To_MaxDepth()
    {
        // Arrange - Create a chain longer than default
        var level1 = new OrgEmployee { Id = 1, Name = "Level 1", DirectReports = new List<OrgEmployee>() };
        var level2 = new OrgEmployee { Id = 2, Name = "Level 2", Manager = level1, DirectReports = new List<OrgEmployee>() };
        var level3 = new OrgEmployee { Id = 3, Name = "Level 3", Manager = level2, DirectReports = new List<OrgEmployee>() };
        var level4 = new OrgEmployee { Id = 4, Name = "Level 4", Manager = level3, DirectReports = new List<OrgEmployee>() };
        var level5 = new OrgEmployee { Id = 5, Name = "Level 5", Manager = level4, DirectReports = new List<OrgEmployee>() };

        level1.DirectReports.Add(level2);
        level2.DirectReports.Add(level3);
        level3.DirectReports.Add(level4);
        level4.DirectReports.Add(level5);

        // Act - Use facet with default settings
        var facet = new OrgEmployeeDefaultFacet(level1);

        // Assert - we can traverse deeper
        facet.Should().NotBeNull();
        facet.Name.Should().Be("Level 1");
        facet.DirectReports.Should().NotBeNull();
        facet.DirectReports.Should().HaveCount(1);

        // Level 2 should be included
        var level2Facet = facet.DirectReports![0];
        level2Facet.Should().NotBeNull();
        level2Facet.Name.Should().Be("Level 2");
        level2Facet.DirectReports.Should().NotBeNull();
        level2Facet.DirectReports.Should().HaveCount(1);

        // Level 3 should be included
        var level3Facet = level2Facet.DirectReports![0];
        level3Facet.Should().NotBeNull();
        level3Facet.Name.Should().Be("Level 3");

        // Manager circular references should be properly handled - they should be null
        // because the parent is already in the __processed set
        level2Facet.Manager.Should().BeNull(); // Level1 is already being processed
        level3Facet.Manager.Should().BeNull(); // Level2 is already being processed
    }

    [Fact]
    public void LeafFacet_Without_NestedFacets_Should_Work_As_NestedFacet()
    {
        // Arrange - SimpleLeaf has no nested facets, but uses default settings
        var parent = new ParentWithLeaf
        {
            Id = 1,
            Name = "Parent",
            Leaf = new SimpleLeaf { Id = 2, Value = "Leaf Value" }
        };

        // Act - This should compile and run (previously caused constructor error)
        var facet = new ParentWithLeafDto(parent);

        // Assert
        facet.Should().NotBeNull();
        facet.Name.Should().Be("Parent");
        facet.Leaf.Should().NotBeNull();
        facet.Leaf!.Value.Should().Be("Leaf Value");
    }

    [Fact]
    public void MixedSettings_ExplicitAndDefault_Should_WorkTogether()
    {
        // Arrange - One facet uses explicit settings, another uses defaults
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

        // Act - MixedSettingsAuthorDto has explicit settings, but its nested facet uses defaults
        var facet = new MixedSettingsAuthorDto(author);

        // Assert
        facet.Should().NotBeNull();
        facet.Name.Should().Be("Mixed Settings Author");
        facet.Books.Should().NotBeNull();
        facet.Books.Should().HaveCount(1);
    }

    [Fact]
    public void SharedReference_In_Collection_Should_Be_Tracked()
    {
        // Arrange - Same author appears multiple times in a collection
        var sharedAuthor = new Author
        {
            Id = 1,
            Name = "Shared Author",
            Books = new List<Book>()
        };

        var book1 = new Book { Id = 1, Title = "Book 1", Author = sharedAuthor };
        var book2 = new Book { Id = 2, Title = "Book 2", Author = sharedAuthor };

        sharedAuthor.Books.AddRange(new[] { book1, book2 });

        // Create a collection with the same author referenced multiple times
        var authors = new List<Author> { sharedAuthor, sharedAuthor };

        // Act - With PreserveReferences=true, should handle shared references
        var facets = authors.Select(a => new AuthorDefaultDto(a)).ToList();

        // Assert
        facets.Should().HaveCount(2);
        facets[0].Name.Should().Be("Shared Author");
        facets[1].Name.Should().Be("Shared Author");
    }

    [Fact]
    public void ComplexGraph_With_Multiple_Circular_Paths_Should_Not_Overflow()
    {
        // Arrange - Create a complex graph with multiple circular paths
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

        // Create multiple circular paths
        centralNode.DirectReports.Add(node2);
        centralNode.DirectReports.Add(node3);
        node2.DirectReports.Add(node3);
        node3.DirectReports.Add(node2);
        node2.DirectReports.Add(centralNode); // Back to central

        // Act - Should handle complex circular graph
        var facet = new OrgEmployeeDefaultFacet(centralNode);

        // Assert - No stack overflow, that's the main success
        facet.Should().NotBeNull();
        facet.Name.Should().Be("Central");
        facet.DirectReports.Should().NotBeNull();
    }

    [Fact]
    public void ZeroMaxDepth_Should_Still_Work_For_NonRecursive_Structures()
    {
        // Arrange - Simple non-recursive structure with MaxDepth = 0
        var leaf = new SimpleLeaf { Id = 1, Value = "Simple" };
        var parent = new ParentWithLeaf { Id = 2, Name = "Parent", Leaf = leaf };

        // Act - With MaxDepth=0, should still construct non-recursive structures
        var facet = new ParentWithLeafNoDepthDto(parent);

        // Assert
        facet.Should().NotBeNull();
        facet.Name.Should().Be("Parent");
    }

    [Fact]
    public void DefaultSettings_Should_Allow_Constructor_Chaining()
    {
        // Arrange
        var author = new Author
        {
            Id = 1,
            Name = "Constructor Chain Test",
            Books = new List<Book>()
        };

        // Act - Call public constructor (should chain to internal one)
        var facet1 = new AuthorDefaultDto(author);

        // Also test that we can create multiple instances
        var facet2 = new AuthorDefaultDto(author);

        // Assert - Both should be independent instances
        facet1.Should().NotBeNull();
        facet2.Should().NotBeNull();
        facet1.Should().NotBeSameAs(facet2);
        facet1.Name.Should().Be(facet2.Name);
    }

    [Fact]
    public void TripleNestedCircular_Reference_Should_Be_Handled()
    {
        // Arrange - A -> B -> C -> A circular reference
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

        // Act - Should handle triple circular reference
        var facet = new TripleCircularADto(entityA);

        // Assert
        facet.Should().NotBeNull();
        facet.Name.Should().Be("Entity A");
        facet.BReferences.Should().NotBeNull();
    }

    #endregion
}

#region Additional Test Models for New Tests

// Models for the user's reported scenario (bidirectional circular references)
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

// Simple leaf facet without nested facets (tests the core fix)
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

// For testing MaxDepth = 0
[Facet(typeof(SimpleLeaf), MaxDepth = 0, PreserveReferences = false)]
public partial record SimpleLeafNoDepthDto;

[Facet(typeof(ParentWithLeaf), MaxDepth = 0, PreserveReferences = false, NestedFacets = [typeof(SimpleLeafNoDepthDto)])]
public partial record ParentWithLeafNoDepthDto;

// Facets with default settings for existing models
[Facet(typeof(OrgEmployee), NestedFacets = [typeof(OrgEmployeeDefaultFacet)])]
public partial record OrgEmployeeDefaultFacet;

[Facet(typeof(Author), NestedFacets = [typeof(BookDefaultDto)])]
public partial record AuthorDefaultDto;

[Facet(typeof(Book), NestedFacets = [typeof(AuthorDefaultDto)])]
public partial record BookDefaultDto;

// For mixed settings test
[Facet(typeof(Author), MaxDepth = 5, PreserveReferences = true, NestedFacets = [typeof(BookDefaultDto)])]
public partial record MixedSettingsAuthorDto;

// Triple circular reference models (A -> B -> C -> A)
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
