using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Facet.Generators;

/// <summary>
/// A cache-safe capture of a syntax location (file path + spans) for incremental generator
/// models. <see cref="Location"/> itself holds a syntax tree reference, which must not be
/// stored in cached model objects; this carries only value data and reconstructs an equal
/// <see cref="Location"/> at diagnostic-report time — so diagnostics like FAC101–FAC106 can
/// point at the offending attribute instead of Location.None, which also makes them
/// suppressible with a targeted <c>#pragma warning disable</c>.
/// </summary>
internal readonly struct SourceLocationInfo : IEquatable<SourceLocationInfo>
{
    public string FilePath { get; }
    private readonly TextSpan _span;
    private readonly LinePositionSpan _lineSpan;

    private SourceLocationInfo(string filePath, TextSpan span, LinePositionSpan lineSpan)
    {
        FilePath = filePath;
        _span = span;
        _lineSpan = lineSpan;
    }

    /// <summary>Captures the location of an attribute application, or null when unavailable.</summary>
    public static SourceLocationInfo? FromAttribute(AttributeData attribute)
    {
        var reference = attribute.ApplicationSyntaxReference;
        if (reference == null) return null;

        var tree = reference.SyntaxTree;
        return new SourceLocationInfo(tree.FilePath, reference.Span, tree.GetLineSpan(reference.Span).Span);
    }

    public Location ToLocation() => Location.Create(FilePath, _span, _lineSpan);

    public bool Equals(SourceLocationInfo other)
        => FilePath == other.FilePath && _span == other._span && _lineSpan == other._lineSpan;

    public override bool Equals(object? obj) => obj is SourceLocationInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = FilePath?.GetHashCode() ?? 0;
            hash = hash * 31 + _span.GetHashCode();
            hash = hash * 31 + _lineSpan.GetHashCode();
            return hash;
        }
    }
}
