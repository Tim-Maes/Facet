namespace Facet.Mapping;

/// <summary>
/// Provides thread-local context used by source-generated lazy projection code.
/// Tracks the current projection depth and the active max-depth limit during expression tree construction.
/// </summary>
public static class FacetProjectionContext
{
    /// <summary>
    /// The nesting depth of the current lazy projection build chain on this thread.
    /// Incremented when a lazy DTO's <c>Projection</c> property is entered and decremented on exit.
    /// </summary>
    [System.ThreadStatic]
    public static int ProjectionDepth;

    /// <summary>
    /// The max-depth limit set by the root DTO of the current build chain.
    /// Zero means unlimited. Reset to zero when <see cref="ProjectionDepth"/> returns to zero.
    /// </summary>
    [System.ThreadStatic]
    public static int ActiveMaxDepth;
}
