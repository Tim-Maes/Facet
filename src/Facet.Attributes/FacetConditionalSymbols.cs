namespace Facet;

/// <summary>
/// Preprocessor symbols that control whether Facet's declarative attributes survive into
/// the consuming assembly's metadata.
/// </summary>
/// <remarks>
/// <para>
/// <c>[Facet(typeof(TSource))]</c> is a build-time instruction to the source generator: the
/// generator reads it from the semantic model, which happens whether or not the attribute is
/// emitted. Emitting it, however, writes a <c>typeof</c> into the custom attribute blob, which
/// forces an <c>AssemblyRef</c> to the source type's assembly (and to Facet.Attributes) into the
/// compiled output — the dependency a layered architecture is usually trying to keep out of its
/// contract assembly.
/// </para>
/// <para>
/// Marking the attributes <see cref="System.Diagnostics.ConditionalAttribute"/> on
/// <see cref="KeepAttributes"/> leaves that choice to the consuming project. Facet.targets
/// defines the symbol by default, so existing projects keep emitting the attribute exactly as
/// before; setting <c>&lt;Facet_EmitAttributeMetadata&gt;false&lt;/Facet_EmitAttributeMetadata&gt;</c>
/// drops it from metadata and lets Roslyn elide the now-unused assembly references.
/// </para>
/// </remarks>
public static class FacetConditionalSymbols
{
    /// <summary>
    /// When defined, Facet's attributes are written into the compiled assembly's metadata.
    /// Facet.targets defines it unless <c>Facet_EmitAttributeMetadata</c> is <c>false</c>.
    /// </summary>
    public const string KeepAttributes = "FACET_KEEP_ATTRIBUTES";
}
