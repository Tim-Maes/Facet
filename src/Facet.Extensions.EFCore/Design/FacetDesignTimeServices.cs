using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Facet.Extensions.EFCore.Design;

/// <summary>
/// EF Core design-time services that keep the Facet EF model manifest in sync with the model
/// snapshot: every <c>dotnet ef migrations add</c>/<c>remove</c> also rewrites
/// <c>{ContextName}.facetmodel.json</c> beside the snapshot (see <see cref="FacetEfModelManifest"/>).
/// </summary>
/// <remarks>
/// Because these services live in a referenced library rather than the startup project,
/// <c>dotnet ef</c> does not discover them automatically. Register them with one assembly
/// attribute in the startup project:
/// <code>
/// [assembly: Microsoft.EntityFrameworkCore.Design.DesignTimeServicesReference(
///     "Facet.Extensions.EFCore.Design.FacetDesignTimeServices, Facet.Extensions.EFCore")]
/// </code>
/// Then expose the manifest to the Facet generator in the project that declares
/// <c>[GenerateDtos]</c> attributes:
/// <code>
/// &lt;AdditionalFiles Include="Migrations/*.facetmodel.json" /&gt;
/// </code>
/// </remarks>
public sealed class FacetDesignTimeServices : IDesignTimeServices
{
    /// <inheritdoc />
    public void ConfigureDesignTimeServices(IServiceCollection services)
        // Scoped to mirror EF's own IMigrationsScaffolder registration; added (not TryAdd) so
        // this appended registration wins resolution over the default scaffolder.
        => services.AddScoped<IMigrationsScaffolder, FacetManifestMigrationsScaffolder>();
}
