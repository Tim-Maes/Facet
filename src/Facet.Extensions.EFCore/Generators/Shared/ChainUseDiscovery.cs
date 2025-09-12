using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Facet.Extensions.EFCore.Generators.Emission;

namespace Facet.Extensions.EFCore.Generators.Shared;

/// <summary>
/// Discovers chain usage patterns in code for fluent navigation builders.
/// </summary>
public static class ChainUseDiscovery
{
    public static IncrementalValuesProvider<string> Configure(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax,
                transform: static (context, cancellationToken) =>
                {
                    var invocation = (InvocationExpressionSyntax)context.Node;
                    return ExtractChainPattern(invocation);
                })
            .Where(static chain => !string.IsNullOrEmpty(chain))
            .Select(static (chain, _) => chain!);
    }

    private static string? ExtractChainPattern(InvocationExpressionSyntax invocation)
    {
        // Look for patterns like .WithProviderLink().WithSomething()
        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess == null) return null;

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (!methodName.StartsWith("With")) return null;

        // Extract the chain pattern
        var chain = new List<string>();
        var current = invocation;

        while (current != null)
        {
            if (current.Expression is MemberAccessExpressionSyntax currentMember)
            {
                var currentMethodName = currentMember.Name.Identifier.ValueText;
                if (currentMethodName.StartsWith("With"))
                {
                    chain.Insert(0, currentMethodName);
                }

                // Move to the next part of the chain
                if (currentMember.Expression is InvocationExpressionSyntax nextInvocation)
                {
                    current = nextInvocation;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        return chain.Count > 0 ? string.Join(".", chain) : null;
    }

    public static ImmutableDictionary<string, ImmutableHashSet<string>> GroupAndNormalizeWithDepthCapping(
        ImmutableArray<string> chains,
        SourceProductionContext context,
        int maxDepth)
    {
        var result = new Dictionary<string, HashSet<string>>();

        foreach (var chain in chains)
        {
            if (string.IsNullOrEmpty(chain)) continue;

            var parts = chain.Split('.');
            if (parts.Length == 0) continue;

            // Extract entity name from first method (e.g., "WithProviderLink" -> "ProviderLink")
            var firstMethod = parts[0];
            if (!firstMethod.StartsWith("With")) continue;

            var entityName = firstMethod.Substring(4); // Remove "With" prefix

            if (!result.ContainsKey(entityName))
            {
                result[entityName] = new HashSet<string>();
            }

            // Cap the chain depth
            var cappedChain = string.Join(".", parts.Take(maxDepth));
            result[entityName].Add(cappedChain);

            // Report depth capping if needed
            if (parts.Length > maxDepth)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ChainDiscoveryDebug,
                    Location.None,
                    parts.Length,
                    "chain depth capped",
                    $"'{chain}' -> '{cappedChain}'"));
            }
        }

        return result.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToImmutableHashSet());
    }
}