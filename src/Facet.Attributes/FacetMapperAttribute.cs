using System;

namespace Facet;

/// <summary>
/// Registers a mapper between a source type and an existing facet type, to be generated into
/// <em>this</em> assembly as a static class of extension methods rather than onto the facet type.
/// </summary>
/// <remarks>
/// <para>
/// <c>[Facet]</c> generates mapping members as a partial half of the facet type, so they can only
/// ever land in the assembly that declares the facet. A C# partial type cannot span assemblies,
/// which is why a layered solution cannot simply move the mappings elsewhere: the whole facet type
/// has to live wherever its mappings do.
/// </para>
/// <para>
/// This attribute inverts the ownership. It is applied at assembly level in the project that is
/// allowed to see both layers, and the generated mapper is a free-standing static class — no
/// partial, so no same-assembly constraint:
/// </para>
/// <code>
/// // in the mappers project, which references both the application layer and the API contracts
/// [assembly: FacetMapper(typeof(UserUseCaseModel), typeof(UserApiModel))]
/// </code>
/// <para>
/// The contract project then declares the facet shape with <c>[Facet]</c> and
/// <c>&lt;Facet_GenerateMappings&gt;false&lt;/Facet_GenerateMappings&gt;</c>, and (with
/// <c>&lt;Facet_EmitAttributeMetadata&gt;false&lt;/Facet_EmitAttributeMetadata&gt;</c>) compiles to
/// an assembly with no reference to the layer it was shaped from.
/// </para>
/// <para>
/// Unlike <see cref="FacetAttribute"/>, this attribute is deliberately <em>not</em>
/// <see cref="System.Diagnostics.ConditionalAttribute"/>: it is applied in an assembly that
/// references both types by definition, so there is no dependency to keep out of its metadata.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class FacetMapperAttribute : Attribute
{
    /// <summary>
    /// Registers a mapper from <paramref name="sourceType"/> to <paramref name="facetType"/>.
    /// </summary>
    /// <param name="sourceType">The type being mapped from, e.g. an application-layer model.</param>
    /// <param name="facetType">
    /// The facet type being mapped to. Normally a type declared with <c>[Facet]</c> in a contract
    /// assembly, though any type whose properties are publicly settable works — the generator
    /// matches members by name against <paramref name="sourceType"/> and never reads
    /// <c>[Facet]</c> itself.
    /// </param>
    /// <param name="exclude">Property names on the facet type to leave unmapped.</param>
    public FacetMapperAttribute(Type sourceType, Type facetType, params string[] exclude)
    {
        SourceType = sourceType;
        FacetType = facetType;
        Exclude = exclude ?? Array.Empty<string>();
    }

    /// <summary>The type to map from.</summary>
    public Type SourceType { get; }

    /// <summary>The facet type to map to.</summary>
    public Type FacetType { get; }

    /// <summary>Property names on the facet type to leave unmapped.</summary>
    public string[] Exclude { get; }

    /// <summary>
    /// Name of the generated static class. Defaults to <c>{FacetTypeName}Mapper</c>.
    /// Set this when one facet type is mapped from more than one source type, since the default
    /// name would otherwise collide.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Namespace for the generated static class. Defaults to the facet type's own namespace, so
    /// that the extension methods are in scope wherever the facet type already is. Set this to
    /// keep the mappers in a namespace of their own, at the cost of an extra <c>using</c>.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Whether to generate the <c>Projection</c> expression and the <c>IQueryable</c> overload
    /// (default: true). These are what let a query project straight to the facet in the database.
    /// </summary>
    public bool GenerateProjection { get; set; } = true;

    /// <summary>
    /// Whether to generate the reverse <c>To{SourceType}</c> mapping (default: false).
    /// Requires the source type to have an accessible parameterless constructor and publicly
    /// settable properties; otherwise FAC112 is reported and the method is omitted.
    /// </summary>
    public bool GenerateToSource { get; set; }

    /// <summary>
    /// Optional type carrying custom mapping logic for this pair.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implement <c>IFacetMapConfiguration&lt;TSource, TFacet&gt;</c> for imperative mapping. Its
    /// static <c>Map(source, target)</c> runs inside the generated <c>To{Facet}</c> method, after
    /// the automatically mapped members are assigned. Imperative code cannot appear in an
    /// expression tree, so it is <em>not</em> applied to <c>Projection</c>.
    /// </para>
    /// <para>
    /// Implement <c>IFacetProjectionMapConfiguration&lt;TSource, TFacet&gt;</c> instead (or as
    /// well) for mappings that must survive into SQL. Its static
    /// <c>ConfigureProjection(builder)</c> declares each rule as an expression, and those are
    /// merged into <c>Projection</c>'s object initializer — so the query provider still translates
    /// the whole thing. A rule declared there also applies to <c>To{Facet}</c>, which keeps one
    /// definition for both paths.
    /// </para>
    /// <para>
    /// Unlike <see cref="FacetAttribute.Configuration"/>, this type lives in the mapper assembly,
    /// which already references both layers — so it can see the source type and the facet without
    /// putting a dependency anywhere it does not belong.
    /// </para>
    /// </remarks>
    public Type? Configuration { get; set; }

    /// <summary>
    /// Optional type whose static <c>BeforeMap(source, target)</c> runs before the automatically
    /// mapped members are assigned. Must implement
    /// <c>IFacetBeforeMapConfiguration&lt;TSource, TFacet&gt;</c>. Applies to
    /// <c>To{Facet}</c> only, never to <c>Projection</c>.
    /// </summary>
    public Type? BeforeMapConfiguration { get; set; }

    /// <summary>
    /// Optional type whose static <c>AfterMap(source, target)</c> runs after everything else,
    /// <see cref="Configuration"/> included. Must implement
    /// <c>IFacetAfterMapConfiguration&lt;TSource, TFacet&gt;</c>. Applies to <c>To{Facet}</c>
    /// only, never to <c>Projection</c>.
    /// </summary>
    public Type? AfterMapConfiguration { get; set; }

    /// <summary>
    /// Optional type whose static <c>Map(facet, target)</c> runs inside the generated reverse
    /// mapping, after its automatically mapped members are assigned. Must implement
    /// <c>IFacetToSourceConfiguration&lt;TFacet, TSource&gt;</c>. Requires
    /// <see cref="GenerateToSource"/>.
    /// </summary>
    public Type? ToSourceConfiguration { get; set; }
}
