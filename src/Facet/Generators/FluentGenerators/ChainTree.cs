using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Facet.Generators.Fluent;

/// <summary>
/// One included navigation in a fluent chain, with the nested inclusions applied to its
/// target (from the marker lambda: <c>WithItems(i =&gt; i.WithProduct())</c> yields the node
/// Items with child Product). Children are kept in written order at discovery and sorted
/// only when a canonical SET identity is needed — states are sets, transitions are ordered.
/// </summary>
internal sealed class ChainNode : IEquatable<ChainNode>
{
    public ChainNode(string nav, ImmutableArray<ChainNode> children)
    {
        Nav = nav;
        Children = children.IsDefault ? ImmutableArray<ChainNode>.Empty : children;
    }

    public string Nav { get; }
    public ImmutableArray<ChainNode> Children { get; }

    public int Depth => 1 + (Children.Length == 0 ? 0 : Children.Max(c => c.Depth));

    public bool Equals(ChainNode? other)
        => other != null && Nav == other.Nav && Children.SequenceEqual(other.Children);

    public override bool Equals(object? obj) => obj is ChainNode other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Nav.GetHashCode();
            foreach (var child in Children) hash = hash * 31 + child.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// Canonical identity and naming for chain states. A state is a SET of nodes (order the
/// user chained them in must not matter — both orders of the same inclusions land on the
/// same generated types), so every canonical form sorts nodes by nav name first.
/// </summary>
internal static class ChainCanon
{
    public static ImmutableArray<ChainNode> Sort(ImmutableArray<ChainNode> nodes)
        => nodes.Sort((a, b) => string.CompareOrdinal(a.Nav, b.Nav));

    /// <summary>Structural canon: <c>Items(Product),User</c>. Unique per state.</summary>
    public static string Canon(ImmutableArray<ChainNode> nodes)
    {
        var sorted = Sort(nodes);
        var sb = new StringBuilder();
        for (var i = 0; i < sorted.Length; i++)
        {
            if (i > 0) sb.Append(',');
            AppendNode(sb, sorted[i]);
        }

        return sb.ToString();
    }

    private static void AppendNode(StringBuilder sb, ChainNode node)
    {
        sb.Append(node.Nav);
        if (node.Children.Length == 0) return;
        sb.Append('(');
        var sorted = Sort(node.Children);
        for (var i = 0; i < sorted.Length; i++)
        {
            if (i > 0) sb.Append(',');
            AppendNode(sb, sorted[i]);
        }

        sb.Append(')');
    }

    /// <summary>
    /// Valid-identifier mangling of <see cref="Canon"/>, used for selector properties and
    /// marker type names: <c>(</c> → <c>_With_</c>, <c>)</c> → <c>_End</c>, <c>,</c> →
    /// <c>_And_</c>. <c>Items(Product),User</c> → <c>Items_With_Product_End_And_User</c>.
    /// The grammar keeps sibling and nesting separators distinct, so names cannot collide
    /// across different trees (nav names containing these exact separator substrings would
    /// be pathological).
    /// </summary>
    public static string Identifier(ImmutableArray<ChainNode> nodes)
        => Canon(nodes).Replace("(", "_With_").Replace(")", "_End").Replace(",", "_And_");
}
