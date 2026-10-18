using System;

namespace Facet;

/// <summary>
/// Generates a mapper into this assembly for every facet type a referenced assembly declares,
/// without restating each pair.
/// </summary>
/// <remarks>
/// <para>
/// <c>[assembly: FacetMapper(typeof(TSource), typeof(TFacet))]</c> names one pair, which means the
/// pair is written down twice — once as the <c>[Facet]</c> in the contract assembly and again as
/// the registration here. Two places to keep in step is one too many: add a facet and forget the
/// registration and nothing complains, because a contract assembly's facets carry no mapping
/// members of their own to go missing.
/// </para>
/// <para>
/// This attribute removes the second copy. A contract assembly records its own (facet, source)
/// pairs as <c>AssemblyMetadata</c> entries — plain strings, so they survive
/// <c>Facet_EmitAttributeMetadata=false</c> — and this reads them:
/// </para>
/// <code>
/// // one line, however many facets ApiModels declares
/// [assembly: FacetMappersForAssembly(typeof(ApiModels.UserApiModel))]
/// </code>
/// <para>
/// Any <c>[assembly: FacetMapper]</c> for a pair this would have covered wins, so a single pair
/// needing its own <c>Name</c>, <c>Exclude</c> or <c>Configuration</c> is stated explicitly while
/// the rest are picked up here.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class FacetMappersForAssemblyAttribute : Attribute
{
    /// <summary>
    /// Registers a mapper for every facet declared by the assembly containing
    /// <paramref name="contractAssemblyMarker"/>.
    /// </summary>
    /// <param name="contractAssemblyMarker">
    /// Any type from the contract assembly — it identifies the assembly and is not itself
    /// required to be a facet. A <c>typeof</c> here costs nothing, because this assembly
    /// references the contract assembly by definition.
    /// </param>
    public FacetMappersForAssemblyAttribute(Type contractAssemblyMarker)
    {
        ContractAssemblyMarker = contractAssemblyMarker;
    }

    /// <summary>A type identifying the contract assembly to read.</summary>
    public Type ContractAssemblyMarker { get; }

    /// <summary>
    /// Facet type names to skip — either the simple name or the fully qualified name. Use this for
    /// a facet that is mapped by hand, or not at all.
    /// </summary>
    public string[]? Exclude { get; set; }

    /// <summary>
    /// Whether the generated mappers include the <c>Projection</c> expression and its
    /// <c>IQueryable</c> overload (default: true).
    /// </summary>
    public bool GenerateProjection { get; set; } = true;

    /// <summary>
    /// Whether the generated mappers include the reverse <c>To{Source}</c> mapping
    /// (default: false).
    /// </summary>
    public bool GenerateToSource { get; set; }

    /// <summary>
    /// Namespace for the generated mapper classes. Defaults to each facet type's own namespace.
    /// </summary>
    public string? Namespace { get; set; }
}
