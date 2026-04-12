namespace Facet.Mapping;

/// <summary>
/// Allows declaring custom property mappings that are inlined into the generated
/// <c>Projection</c> expression, making them available to EF Core query translation.
/// Implement this alongside <see cref="IFacetMapConfiguration{TSource,TTarget}"/> when
/// some of your <c>Map()</c> logic can be expressed as simple LINQ expressions.
/// </summary>
/// <remarks>
/// <para>
/// Expressions passed to <see cref="IFacetProjectionBuilder{TSource,TTarget}.Map{TValue}"/>
/// must be EF Core-translatable. If they are not, EF Core will throw at query execution time.
/// </para>
/// <para>
/// <c>ConfigureProjection</c> is called once during lazy initialization. Do not reference
/// instance state or external services.
/// </para>
/// <para>
/// Properties omitted from <c>ConfigureProjection</c> but present in <c>Map()</c> will not
/// appear in <c>Projection</c>. This is intentional — use <c>ConfigureProjection</c> only for
/// SQL-translatable expressions.
/// </para>
/// </remarks>
/// <typeparam name="TSource">The source entity type.</typeparam>
/// <typeparam name="TTarget">The target Facet-generated DTO type.</typeparam>
public interface IFacetProjectionMapConfiguration<TSource, TTarget>
{
    /// <summary>
    /// Declare additional property bindings to include in the generated Projection.
    /// Only use expressions that EF Core can translate to SQL (property access,
    /// arithmetic, string concatenation, ternary operators, etc.).
    /// Do NOT reference services or call arbitrary methods here.
    /// </summary>
    /// <param name="builder">The builder used to register expression bindings.</param>
    static abstract void ConfigureProjection(IFacetProjectionBuilder<TSource, TTarget> builder);
}
