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

    public IFacetProjectionBuilder<TSource, TTarget> Map<TValue>(
        Expression<Func<TTarget, TValue>> targetMember,
        Expression<Func<TSource, TValue>> valueExpression)
    {
        var member = ((MemberExpression)targetMember.Body).Member;
        Mappings.Add((member, valueExpression));
        return this;
    }
}
