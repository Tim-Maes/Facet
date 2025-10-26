namespace Facet.Tests.TestModels;

// Models with circular references
public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Book> Books { get; set; } = new();
}

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Author? Author { get; set; }
}

// Self-referencing model
public class OrgEmployee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public OrgEmployee? Manager { get; set; }
    public List<OrgEmployee> DirectReports { get; set; } = new();
}

// Facets with MaxDepth for depth limiting
[Facet(typeof(Author), MaxDepth = 2, NestedFacets = [typeof(BookFacetWithDepth)])]
public partial record AuthorFacetWithDepth;

[Facet(typeof(Book), MaxDepth = 2, NestedFacets = [typeof(AuthorFacetWithDepth)])]
public partial record BookFacetWithDepth;

// Facets with PreserveReferences for runtime tracking (also needs MaxDepth to prevent generator SO)
[Facet(typeof(Author), MaxDepth = 3, PreserveReferences = true, NestedFacets = [typeof(BookFacetWithTracking)])]
public partial record AuthorFacetWithTracking;

[Facet(typeof(Book), MaxDepth = 3, PreserveReferences = true, NestedFacets = [typeof(AuthorFacetWithTracking)])]
public partial record BookFacetWithTracking;

// Self-referencing facet with both MaxDepth and PreserveReferences
[Facet(typeof(OrgEmployee), MaxDepth = 5, PreserveReferences = true, NestedFacets = [typeof(OrgEmployeeFacet)])]
public partial record OrgEmployeeFacet;
