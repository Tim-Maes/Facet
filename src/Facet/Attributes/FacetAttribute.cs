using System;
using Facet.Generators;
using Microsoft.CodeAnalysis;

namespace Facet.Attributes;

/// <summary>
/// Indicates that this class should be generated based on a source type, optionally excluding properties or including fields.
/// </summary>
[GenerateAttribute(AttributeTargets.Class, AllowMultiple = true)]
public sealed class FacetAttribute
{
    /// <summary>
    /// The type to project from.
    /// </summary>
    public INamedTypeSymbol SourceType { get; }

    /// <summary>
    /// An array of property or field names to exclude from the generated class.
    /// </summary>
    public string[] Exclude { get; }

    /// <summary>
    /// Whether to include public fields from the source type (default: true).
    /// </summary>
    public bool IncludeFields { get; set; } = false;

    /// <summary>
    /// Whether to generate a constructor that accepts the source type and copies over matching members.
    /// </summary>
    public bool GenerateConstructor { get; set; } = true;

    /// <summary>
    /// Optional type that provides custom mapping logic via a static Map(source, target) method.
    /// Must match the signature defined in IFacetMapConfiguration&lt;TSource, TTarget&gt;.
    /// </summary>
    /// <remarks>
    /// The type must define a static method with the signature:
    /// <c>public static void Map(TSource source, TTarget target)</c>.
    /// This allows injecting custom projections, formatting, or derived values at compile time.
    /// </remarks>
    public INamedTypeSymbol? Configuration { get; set; }

    /// <summary>
    /// Whether to generate the static Expression&lt;Func&lt;TSource,TTarget&gt;&gt; Projection.
    /// Default is true so you always get a Projection by default.
    /// </summary>
    public bool GenerateProjection { get; set; } = true;

    /// <summary>
    /// The kind of facet to generate. Determines whether a class or record is created.
    /// </summary>
    public FacetKind Kind { get; set; } = FacetKind.Class;

    /// <summary>
    /// Creates a new FacetAttribute that targets a given source type and excludes specified members.
    /// </summary>
    /// <param name="sourceType">The type to generate from.</param>
    /// <param name="exclude">The names of the properties or fields to exclude.</param>
    public FacetAttribute(INamedTypeSymbol sourceType, params string[] exclude)
    {
        SourceType = sourceType;
        Exclude = exclude ?? Array.Empty<string>();
    }
}