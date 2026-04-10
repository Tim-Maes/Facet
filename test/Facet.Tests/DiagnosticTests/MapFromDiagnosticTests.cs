namespace Facet.Tests.DiagnosticTests;

// Test models to verify FAC024 diagnostic for MapFrom with non-existing source properties

public class UserSource
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
}

// FAC024 fires for a simple name that does not exist in the source type:
//   [MapFrom("NONEXISTING")] on a [Facet(typeof(UserSource))] class
// The following is intentionally commented out so the test project compiles.
// To manually verify FAC024, uncomment the block below:
//
//   [Facet(typeof(UserSource), Include = [])]
//   public partial class UserModelWithInvalidMapFrom
//   {
//       [MapFrom("NONEXISTING")]  // FAC024 error here
//       public string Id { get; set; } = string.Empty;
//   }

// This should NOT trigger FAC024: "Email" exists on UserSource
[Facet(typeof(UserSource), Include = [])]
public partial class UserModelWithValidMapFrom
{
    [MapFrom("Email")]
    public string ContactEmail { get; set; } = string.Empty;
}

// This should NOT trigger FAC024: dotted paths are skipped (navigation/expression mappings)
[Facet(typeof(UserSource), Include = [])]
public partial class UserModelWithDottedPath
{
    [MapFrom("Address.Street")]
    public string Street { get; set; } = string.Empty;
}
