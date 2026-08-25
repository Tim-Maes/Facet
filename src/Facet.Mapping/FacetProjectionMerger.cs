using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Facet.Mapping;

/// <summary>
/// Merges a base projection expression (containing auto-matched member bindings)
/// with additional bindings from a <see cref="FacetProjectionBuilder{TSource,TTarget}"/>.
/// Config-provided bindings override auto-matched ones when they target the same member.
/// </summary>
public static class FacetProjectionMerger
{
    /// <summary>
    /// Merges <paramref name="baseProjection"/> (a MemberInit expression with auto-matched bindings)
    /// with the mappings from <paramref name="builder"/> (config-provided bindings).
    /// Config bindings override auto-matched bindings for the same member.
    /// </summary>
    public static Expression<Func<TSource, TTarget>> Merge<TSource, TTarget>(
        Expression<Func<TSource, TTarget>> baseProjection,
        FacetProjectionBuilder<TSource, TTarget> builder)
    {
        // Extract bindings from the base projection's MemberInit expression
        var baseParam = baseProjection.Parameters[0];
        var memberInit = (MemberInitExpression)baseProjection.Body;

        // Collect all bindings keyed by member name (auto-matched first)
        var bindings = new Dictionary<string, MemberBinding>();
        foreach (var binding in memberInit.Bindings)
        {
            bindings[binding.Member.Name] = binding;
        }

        // Create a shared source parameter for the merged expression
        var sourceParam = Expression.Parameter(typeof(TSource), "source");

        // Re-bind auto-matched bindings to use the new source parameter
        var reboundBindings = new Dictionary<string, MemberBinding>();
        foreach (var kvp in bindings)
        {
            var binding = (MemberAssignment)kvp.Value;
            var reboundBody = ParameterReplacer.Replace(
                Expression.Lambda(binding.Expression, baseParam), sourceParam);
            reboundBindings[kvp.Key] = Expression.Bind(binding.Member, reboundBody);
        }

        // Add/override with config-provided bindings
        foreach (var (member, lambdaExpr) in builder.Mappings)
        {
            var body = ParameterReplacer.Replace(lambdaExpr, sourceParam);
            reboundBindings[member.Name] = Expression.Bind(member, body);
        }

        var newExpr = Expression.New(typeof(TTarget));
        var initExpr = Expression.MemberInit(newExpr, reboundBindings.Values);
        return Expression.Lambda<Func<TSource, TTarget>>(initExpr, sourceParam);
    }
}
