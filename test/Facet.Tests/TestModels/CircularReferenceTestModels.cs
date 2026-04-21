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

// Facets with MaxDepth for depth limiting (without reference tracking)
[Facet(typeof(Author), MaxDepth = 2, PreserveReferences = false, NestedFacets = [typeof(BookFacetWithDepth)], GenerateToSource = true)]
public partial record AuthorFacetWithDepth;

[Facet(typeof(Book), MaxDepth = 2, PreserveReferences = false, NestedFacets = [typeof(AuthorFacetWithDepth)], GenerateToSource = true)]
public partial record BookFacetWithDepth;

// Facets with PreserveReferences for runtime tracking (also needs MaxDepth to prevent generator SO)
[Facet(typeof(Author), MaxDepth = 3, PreserveReferences = true, NestedFacets = [typeof(BookFacetWithTracking)])]
public partial record AuthorFacetWithTracking;

[Facet(typeof(Book), MaxDepth = 3, PreserveReferences = true, NestedFacets = [typeof(AuthorFacetWithTracking)])]
public partial record BookFacetWithTracking;

// Self-referencing facet with both MaxDepth and PreserveReferences
[Facet(typeof(OrgEmployee), MaxDepth = 5, PreserveReferences = true, NestedFacets = [typeof(OrgEmployeeFacet)])]
public partial record OrgEmployeeFacet;

// Facets relying on default MaxDepth (10) — no explicit MaxDepth or PreserveReferences
// These verify that the defaults work correctly for circular reference scenarios
[Facet(typeof(Author), NestedFacets = [typeof(BookFacetDefault)])]
public partial record AuthorFacetDefault;

[Facet(typeof(Book), NestedFacets = [typeof(AuthorFacetDefault)])]
public partial record BookFacetDefault;

// Deep non-circular chain to verify default MaxDepth allows at least depth 5
public class Level0 { public Level1? Child { get; set; } public string Name { get; set; } = ""; }
public class Level1 { public Level2? Child { get; set; } public string Name { get; set; } = ""; }
public class Level2 { public Level3? Child { get; set; } public string Name { get; set; } = ""; }
public class Level3 { public Level4? Child { get; set; } public string Name { get; set; } = ""; }
public class Level4 { public string Name { get; set; } = ""; }

[Facet(typeof(Level0), NestedFacets = [typeof(Level1Facet)])]
public partial record Level0Facet;
[Facet(typeof(Level1), NestedFacets = [typeof(Level2Facet)])]
public partial record Level1Facet;
[Facet(typeof(Level2), NestedFacets = [typeof(Level3Facet)])]
public partial record Level2Facet;
[Facet(typeof(Level3), NestedFacets = [typeof(Level4Facet)])]
public partial record Level3Facet;
[Facet(typeof(Level4))]
public partial record Level4Facet;
