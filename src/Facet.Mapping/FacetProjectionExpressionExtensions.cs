using System;
using System.Linq.Expressions;

namespace Facet.Mapping;

/// <summary>
/// Extension methods for working with Facet projection expressions.
/// </summary>
public static class FacetProjectionExpressionExtensions
{
    /// <summary>
    /// Reinterprets a non-nullable projection expression as returning a nullable result.
    /// Use this when passing a <c>ProjectionFromX</c> to <see cref="IFacetProjectionBuilder{TSource,TTarget}.Map{TValue}"/>
    /// for a nullable target property, to avoid CS8620 nullability warnings.
    /// </summary>
    /// <remarks>
    /// This is a zero-cost operation at runtime. Nullable reference type annotations are compiler-only;
    /// <c>Expression&lt;Func&lt;TSource, TResult&gt;&gt;</c> and <c>Expression&lt;Func&lt;TSource, TResult?&gt;&gt;</c>
    /// are the same CLR type for reference types.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Map(target => target.NullableProperty, SomeDto.ProjectionFromEntity.AsNullable());
    /// </code>
    /// </example>
    public static Expression<Func<TSource, TResult?>> AsNullable<TSource, TResult>(
        this Expression<Func<TSource, TResult>> expression)
        where TResult : class
        => (Expression<Func<TSource, TResult?>>)(object)expression;
}
