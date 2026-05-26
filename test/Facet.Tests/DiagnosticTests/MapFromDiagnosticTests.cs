namespace Facet.Tests.DiagnosticTests;

public class UserSource
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
}

[Facet(typeof(UserSource), Include = [])]
public partial class UserModelWithValidMapFrom
{
    [MapFrom("Email")]
    public string ContactEmail { get; set; } = string.Empty;
}
