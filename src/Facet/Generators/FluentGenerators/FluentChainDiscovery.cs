using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Facet.Generators.Fluent;

/// <summary>
/// One fluent chain as written: <c>ctx.FacetOrder().WithUser().WithItems(i =&gt; i.WithProduct())…</c>
/// yields entity <c>Order</c> with ordered steps [User, Items(Product)]. Steps keep written
/// order because intermediate states need transition methods; state identity itself is
/// order-independent (see <see cref="ChainCanon"/>).
/// </summary>
internal sealed class DiscoveredChain : IEquatable<DiscoveredChain>
{
    public DiscoveredChain(string entitySimpleName, ImmutableArray<ChainNode> steps, SourceLocationInfo location)
    {
        EntitySimpleName = entitySimpleName;
        Steps = steps;
        Location = location;
    }

    public string EntitySimpleName { get; }
    public ImmutableArray<ChainNode> Steps { get; }
    public SourceLocationInfo Location { get; }

    public bool Equals(DiscoveredChain? other)
        => other != null
            && EntitySimpleName == other.EntitySimpleName
            && Steps.SequenceEqual(other.Steps)
            && Location.Equals(other.Location);

    public override bool Equals(object? obj) => obj is DiscoveredChain other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = EntitySimpleName.GetHashCode();
            foreach (var step in Steps) hash = hash * 31 + step.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// Syntax-only discovery of fluent chain uses. Semantic information cannot exist for the
/// chains' methods on a first run (the generator itself emits them), so recognition is
/// purely structural: an argument-less <c>.Facet{Entity}()</c> invocation, followed by
/// <c>.With{Nav}(…)</c> invocations whose optional single argument is a marker lambda
/// (<c>i =&gt; i.WithProduct()</c>, recursively). Anything the walker cannot read as a pure
/// chain simply contributes fewer steps — the generated surface then lacks the method and
/// the user gets an ordinary compile error at their call site, never silent misbehavior.
/// Both benefits of usage-driven generation survive: no 2^N explosion, and manifest
/// presence alone emits only the single-step base surface.
/// </summary>
internal static class FluentChainDiscovery
{
    private const string EntryPrefix = "Facet";
    private const string StepPrefix = "With";

    public static bool IsCandidate(SyntaxNode node)
        => node is InvocationExpressionSyntax invocation
            && invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name is IdentifierNameSyntax name
            && name.Identifier.ValueText.Length > EntryPrefix.Length
            && name.Identifier.ValueText.StartsWith(EntryPrefix, StringComparison.Ordinal)
            && char.IsUpper(name.Identifier.ValueText[EntryPrefix.Length]);

    public static DiscoveredChain? Parse(GeneratorSyntaxContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var entry = (InvocationExpressionSyntax)context.Node;
        var entryName = ((MemberAccessExpressionSyntax)entry.Expression).Name.Identifier.ValueText;
        var entityName = entryName.Substring(EntryPrefix.Length);

        var steps = ImmutableArray.CreateBuilder<ChainNode>();
        SyntaxNode current = entry;
        while (current.Parent is MemberAccessExpressionSyntax parentAccess
            && parentAccess.Parent is InvocationExpressionSyntax parentInvocation
            && parentAccess.Expression == current)
        {
            var stepName = parentAccess.Name.Identifier.ValueText;
            if (!stepName.StartsWith(StepPrefix, StringComparison.Ordinal) || stepName.Length <= StepPrefix.Length)
            {
                break; // a terminal (ToListAsync, Where, …) or foreign method ends the chain
            }

            var children = ParseMarkerLambda(parentInvocation);
            steps.Add(new ChainNode(stepName.Substring(StepPrefix.Length), children));
            current = parentInvocation;
        }

        return steps.Count == 0
            ? null // bare entry point needs no more than the always-emitted base surface
            : new DiscoveredChain(entityName, steps.ToImmutable(), SourceLocationInfo.FromSyntax(entry));
    }

    /// <summary>
    /// Reads a step's marker lambda (<c>i =&gt; i.WithProduct().WithNote(...)</c>) into nested
    /// chain nodes, in written order. A lambda that is not a pure marker chain rooted at its
    /// own parameter yields no children.
    /// </summary>
    private static ImmutableArray<ChainNode> ParseMarkerLambda(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 1
            || invocation.ArgumentList.Arguments[0].Expression is not SimpleLambdaExpressionSyntax lambda
            || lambda.ExpressionBody == null)
        {
            return ImmutableArray<ChainNode>.Empty;
        }

        return ParseChainExpression(lambda.ExpressionBody, lambda.Parameter.Identifier.ValueText);
    }

    private static ImmutableArray<ChainNode> ParseChainExpression(ExpressionSyntax expression, string parameterName)
    {
        var reversed = new List<ChainNode>();
        var current = expression;
        while (current is InvocationExpressionSyntax invocation
            && invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var stepName = memberAccess.Name.Identifier.ValueText;
            if (!stepName.StartsWith(StepPrefix, StringComparison.Ordinal) || stepName.Length <= StepPrefix.Length)
            {
                return ImmutableArray<ChainNode>.Empty;
            }

            reversed.Add(new ChainNode(stepName.Substring(StepPrefix.Length), ParseMarkerLambda(invocation)));
            current = memberAccess.Expression;
        }

        if (current is not IdentifierNameSyntax identifier
            || identifier.Identifier.ValueText != parameterName)
        {
            return ImmutableArray<ChainNode>.Empty;
        }

        reversed.Reverse();
        return reversed.ToImmutableArray();
    }
}
