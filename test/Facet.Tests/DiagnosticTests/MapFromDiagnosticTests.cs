namespace Facet.Tests.DiagnosticTests;

// Test models to verify FAC024 diagnostic for MapFrom with non-existing source properties

public class UserSource
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
}

// This should trigger FAC024 error: "NONEXISTING" does not exist on UserSource
[Facet(typeof(UserSource), Include = [])]
public partial class UserModelWithInvalidMapFrom
{
    [MapFrom("NONEXISTING")]
    public string Id { get; set; } = string.Empty;
}

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
