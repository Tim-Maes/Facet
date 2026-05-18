using System;
using System.Linq.Expressions;

namespace Facet.Mapping;

/// <summary>
/// Builder interface used inside <see cref="IFacetProjectionMapConfiguration{TSource,TTarget}.ConfigureProjection"/>
/// to declare property bindings that will be inlined into the generated <c>Projection</c> expression.
/// </summary>
/// <typeparam name="TSource">The source entity type.</typeparam>
/// <typeparam name="TTarget">The target Facet-generated DTO type.</typeparam>
public interface IFacetProjectionBuilder<TSource, TTarget>
{
    /// <summary>
    /// Maps <paramref name="targetMember"/> on the target DTO to the value produced by
    /// <paramref name="valueExpression"/> applied to the source entity.
    /// If the property already has an auto-generated binding, this overwrites it.
    /// </summary>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="targetMember">Expression selecting the target property (e.g. <c>d => d.FullName</c>).</param>
    /// <param name="valueExpression">Expression that computes the value from the source (e.g. <c>s => s.FirstName + " " + s.LastName</c>).</param>
    /// <returns>The builder, for fluent chaining.</returns>
    IFacetProjectionBuilder<TSource, TTarget> Map<TValue>(
        Expression<Func<TTarget, TValue>> targetMember,
        Expression<Func<TSource, TValue>> valueExpression);
}
