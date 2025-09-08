using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Facet.Extensions.EFCore.Generators.Emission;

namespace Facet.Extensions.EFCore.Generators;

/// <summary>
/// Discovers actual .WithX().WithY() chains used in code to emit only needed composite types.
/// </summary>
internal static class ChainUseDiscovery
{
    /// <summary>
    /// Default maximum depth allowed for navigation chains to prevent infinite expansion.
    /// </summary>
    private const int DefaultMaxChainDepth = 3;
    /// <summary>
    /// Configures the incremental provider for chain discovery.
    /// </summary>
    public static IncrementalValuesProvider<ChainUse> Configure(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => IsTerminalInvocation(node),
            static (ctx, ct) => AnalyzeChain(ctx, ct))
            .Where(static x => x != null)
            .Select(static (x, _) => x!);
    }

    private static bool IsTerminalInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.ValueText;
        return IsTerminalMethod(methodName);
    }

    private static bool IsTerminalMethod(string methodName)
    {
        return methodName is "GetByIdAsync" or "GetByKeyAsync" or "FirstOrDefaultAsync" or "ToListAsync" or "ToArrayAsync" or "SingleOrDefaultAsync" or "FirstAsync" or "SingleAsync";
    }

    private static ChainUse? AnalyzeChain(GeneratorSyntaxContext context, System.Threading.CancellationToken cancellationToken)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax terminalAccess)
            return null;

        // Walk back through the fluent chain to find the entity and path
        var (entityName, pathList) = WalkFluentChain(terminalAccess.Expression, context.SemanticModel, cancellationToken);

        if (entityName == null)
            return null;

        // Also scan nested lambda arguments for additional paths
        var nestedPaths = CollectNestedPaths(invocation.ArgumentList, cancellationToken);

        // Merge paths
        var allPaths = MergePaths(pathList, nestedPaths);

        return new ChainUse(entityName, allPaths);
    }

    private static (string? entityName, ImmutableArray<string> paths) WalkFluentChain(
        ExpressionSyntax? expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        var paths = ImmutableArray.CreateBuilder<string>();
        var current = expression;
        string? entityName = null;

        while (current != null)
        {
            if (current is InvocationExpressionSyntax invExpr)
            {
                if (invExpr.Expression is MemberAccessExpressionSyntax memberExpr)
                {
                    var methodName = memberExpr.Name.Identifier.ValueText;

                    // Check if this is a With* method
                    if (methodName.StartsWith("With", StringComparison.Ordinal) && methodName.Length > 4)
                    {
                        var navProperty = methodName.Substring(4); // Remove "With" prefix
                        paths.Insert(0, navProperty); // Insert at beginning to maintain order
                    }

                    current = memberExpr.Expression;
                }
                else
                {
                    current = null;
                }
            }
            else if (current is MemberAccessExpressionSyntax directMember)
            {
                // Check if this is a Facet<TEntity, TDto>() call
                if (directMember.Name.Identifier.ValueText == "Facet")
                {
                    entityName = ExtractEntityTypeFromFacetCall(directMember, semanticModel, cancellationToken);
                    break;
                }
                current = directMember.Expression;
            }
            else
            {
                // Check if this is the start of a Facet chain (look for FacetQuery_* or similar patterns)
                var typeInfo = semanticModel.GetTypeInfo(current, cancellationToken);
                if (typeInfo.Type?.Name.StartsWith("FacetQuery_", StringComparison.Ordinal) == true ||
                    typeInfo.Type?.Name.StartsWith("Facet", StringComparison.Ordinal) == true)
                {
                    entityName = ExtractEntityTypeFromTypeInfo(typeInfo.Type);
                    break;
                }
                current = null;
            }
        }

        return (entityName, paths.ToImmutable());
    }

    private static string? ExtractEntityTypeFromFacetCall(
        MemberAccessExpressionSyntax facetCall,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        // Look for generic arguments in Facet<TEntity, TDto>()
        if (facetCall.Parent is InvocationExpressionSyntax invocation)
        {
            var methodSymbol = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            if (methodSymbol?.TypeArguments.Length > 0)
            {
                var entityType = methodSymbol.TypeArguments[0];
                return entityType.Name;
            }
        }

        return null;
    }

    private static string? ExtractEntityTypeFromTypeInfo(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
        {
            var entityType = namedType.TypeArguments[0];
            return entityType.Name;
        }

        return null;
    }

    private static ImmutableArray<string> CollectNestedPaths(
        ArgumentListSyntax? argumentList,
        System.Threading.CancellationToken cancellationToken)
    {
        if (argumentList == null)
            return ImmutableArray<string>.Empty;

        var nestedPaths = ImmutableArray.CreateBuilder<string>();

        foreach (var argument in argumentList.Arguments)
        {
            if (argument.Expression is SimpleLambdaExpressionSyntax lambda)
            {
                var lambdaPaths = AnalyzeLambdaExpression(lambda.Body, cancellationToken);
                nestedPaths.AddRange(lambdaPaths);
            }
        }

        return nestedPaths.ToImmutable();
    }

    private static ImmutableArray<string> AnalyzeLambdaExpression(
        SyntaxNode body,
        System.Threading.CancellationToken cancellationToken)
    {
        var paths = ImmutableArray.CreateBuilder<string>();

        // Look for With* method calls in the lambda body
        var withInvocations = body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax member &&
                         member.Name.Identifier.ValueText.StartsWith("With", StringComparison.Ordinal));

        foreach (var invocation in withInvocations)
        {
          if (cancellationToken.IsCancellationRequested)
          {
            break;
          }

          if (invocation.Expression is MemberAccessExpressionSyntax member)
          {
            var methodName = member.Name.Identifier.ValueText;
            if (methodName.StartsWith("With", StringComparison.Ordinal) && methodName.Length > 4)
            {
              var navProperty = methodName.Substring(4);
              paths.Add(navProperty);
            }
          }
        }

        return paths.ToImmutable();
    }

    private static ImmutableArray<string> MergePaths(
        ImmutableArray<string> mainChain,
        ImmutableArray<string> nestedPaths)
    {
        var result = ImmutableArray.CreateBuilder<string>();

        // Add the main chain as-is if it exists
        if (!mainChain.IsEmpty)
        {
            result.Add(string.Join("/", mainChain));
        }

        // Add individual nested paths
        result.AddRange(nestedPaths);

        // Combine main chain with nested paths for composite paths
        if (!mainChain.IsEmpty && !nestedPaths.IsEmpty)
        {
            var mainChainString = string.Join("/", mainChain);
            foreach (var nestedPath in nestedPaths)
            {
                result.Add($"{mainChainString}/{nestedPath}");
            }
        }

        return result.ToImmutable();
    }

    /// <summary>
    /// Groups chain uses by entity name and normalizes paths.
    /// </summary>
    public static IncrementalValueProvider<ImmutableDictionary<string, ImmutableHashSet<string>>> GroupAndNormalize(
        IncrementalValuesProvider<ChainUse> chainUses)
    {
        return chainUses.Collect().Select(static (chains, _) =>
        {
            var grouped = chains
                .GroupBy(c => c.EntityName)
                .ToImmutableDictionary(
                    g => g.Key,
                    g => g.SelectMany(c => c.Paths)
                          .Where(p => !string.IsNullOrEmpty(p))
                          .ToImmutableHashSet());

            return grouped;
        });
    }

    /// <summary>
    /// Groups chain uses by entity name, normalizes paths, and applies depth capping with diagnostics.
    /// </summary>
    public static ImmutableDictionary<string, ImmutableHashSet<string>> GroupAndNormalizeWithDepthCapping(
        ImmutableArray<ChainUse> chains, SourceProductionContext context, int maxChainDepth = DefaultMaxChainDepth)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>();

        foreach (var entityGroup in chains.GroupBy(c => c.EntityName))
        {
            var entityName = entityGroup.Key;
            var pathBuilder = ImmutableHashSet.CreateBuilder<string>();

            foreach (var chainUse in entityGroup)
            {
                foreach (var path in chainUse.Paths)
                {
                    if (string.IsNullOrEmpty(path))
                        continue;

                    // Count depth by counting path separators - optimized version
                    var depth = CountPathSeparators(path) + 1;

                    if (depth > maxChainDepth)
                    {
                        // Report diagnostic for excessive depth
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.ChainDepthExceeded,
                            Location.None,
                            path,
                            entityName,
                            maxChainDepth));

                        // Truncate the chain to max depth
                        var parts = path.Split('/');
                        var truncatedPath = string.Join("/", parts.Take(maxChainDepth));
                        pathBuilder.Add(truncatedPath);
                    }
                    else
                    {
                        pathBuilder.Add(path);
                    }
                }
            }

            builder.Add(entityName, pathBuilder.ToImmutable());
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Optimized method to count path separators in a string.
    /// More efficient than LINQ Count() for simple character counting.
    /// </summary>
    /// <param name="path">The path string to count separators in</param>
    /// <returns>The number of '/' characters found</returns>
    private static int CountPathSeparators(string path)
    {
        var count = 0;
        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] == '/')
                count++;
        }
        return count;
    }
}

/// <summary>
/// Represents a discovered chain usage.
/// </summary>
/// <param name="EntityName">The entity name (e.g., "Order")</param>
/// <param name="Paths">The navigation paths used (e.g., ["Customer", "Customer/ShippingAddress", "Lines"])</param>
internal sealed record ChainUse(string EntityName, ImmutableArray<string> Paths);
