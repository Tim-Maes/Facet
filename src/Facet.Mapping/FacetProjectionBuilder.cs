using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Facet.Mapping;

/// <summary>
/// Concrete implementation of <see cref="IFacetProjectionBuilder{TSource,TTarget}"/>.
/// Collects user-declared expression bindings that are later inlined into the generated
/// <c>Projection</c> expression by <c>BuildProjection()</c>.
/// </summary>
public sealed class FacetProjectionBuilder<TSource, TTarget>
    : IFacetProjectionBuilder<TSource, TTarget>
{
    public List<(MemberInfo Member, LambdaExpression ValueExpression)> Mappings { get; } = new();

    public IFacetProjectionBuilder<TSource, TTarget> Map<TTargetValue, TSourceValue>(
        Expression<Func<TTarget, TTargetValue>> targetMember,
        Expression<Func<TSource, TSourceValue>> valueExpression)
    {
        var member = ((MemberExpression)targetMember.Body).Member;
        Mappings.Add((member, valueExpression));
        return this;
    }

    /// <summary>
    /// Builds a LINQ projection expression from all registered mappings.
    /// Returns an Expression&lt;Func&lt;TSource, TTarget&gt;&gt; that can be used with IQueryable.Select().
    /// </summary>
    public Expression<Func<TSource, TTarget>> BuildProjectionExpression()
    {
        var sourceParam = Expression.Parameter(typeof(TSource), "source");
        var bindings = new List<MemberBinding>();

        foreach (var (member, lambdaExpr) in Mappings)
        {
            // Replace the lambda's parameter with our sourceParam
            var body = ParameterReplacer.Replace(lambdaExpr, sourceParam);
            bindings.Add(Expression.Bind(member, body));
        }

        var newExpr = Expression.New(typeof(TTarget));
        var initExpr = Expression.MemberInit(newExpr, bindings);
        return Expression.Lambda<Func<TSource, TTarget>>(initExpr, sourceParam);
    }
}
