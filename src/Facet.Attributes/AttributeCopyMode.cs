namespace Facet;

/// <summary>
/// Which attributes <c>CopyAttributes</c> carries from a source member onto the generated member.
/// </summary>
/// <remarks>
/// <para>
/// Copying an attribute copies its assembly reference. An attribute from a NuGet package —
/// <c>[Precision]</c> from <c>Microsoft.EntityFrameworkCore.Abstractions</c>, say — therefore
/// writes that package into the generated type's metadata, and a consumer that reflects over the
/// property throws <see cref="System.IO.FileNotFoundException"/> unless the package is deployed
/// alongside. Swagger schema generation and DataAnnotations validation both reflect over
/// properties, so it surfaces at runtime, in the host.
/// </para>
/// <para>
/// <see cref="Preserve"/> is the default and keeps that behaviour, because changing what an
/// existing project generates is not something a minor version should do. FAC109 reports each
/// attribute that would pin a package, so the hazard is visible without anything changing.
/// </para>
/// </remarks>
public enum AttributeCopyMode
{
    /// <summary>
    /// Copy every attribute that can be copied at all (default). Attributes Facet never copies —
    /// compiler-generated ones, the abstract <c>ValidationAttribute</c> base, and attributes that
    /// trigger another source generator — are still excluded.
    /// </summary>
    Preserve = 0,

    /// <summary>
    /// Also skip attributes whose assembly is a package rather than part of the shared framework,
    /// so the generated type stays loadable for a consumer that does not reference that package.
    /// </summary>
    /// <remarks>
    /// Covers <c>Microsoft.EntityFrameworkCore.*</c> and the equivalents for other ORMs. It does
    /// not touch attributes the shared framework carries: <c>[Column]</c>, <c>[Table]</c> and
    /// <c>[Key]</c> resolve wherever <c>[Required]</c> does, so copying them pins nothing extra.
    /// Whether a column name belongs on a contract is a judgement for the project, made through
    /// <c>CopyAttributesExclude</c>.
    /// </remarks>
    ContractSafe = 1,
}
