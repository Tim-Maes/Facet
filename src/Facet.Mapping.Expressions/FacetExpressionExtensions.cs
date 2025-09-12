using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Facet.Mapping.Expressions;

/// <summary>
/// Extension methods for transforming LINQ expressions between source types and their Facet projections.
/// Enables reuse of business logic expressions across entities and DTOs.
/// </summary>
public static class FacetExpressionExtensions
{
    /// <summary>
    /// Transforms a predicate expression from a source type to work with a Facet target type.
    /// </summary>
    /// <typeparam name="TTarget">The Facet target type</typeparam>
    /// <param name="sourcePredicate">The original predicate expression</param>
    /// <returns>A transformed predicate that works with the target type</returns>
    /// <example>
    /// <code>
    /// Expression&lt;Func&lt;User, bool&gt;&gt; sourcePredicate = u => u.IsActive && u.Age > 18;
    /// Expression&lt;Func&lt;UserDto, bool&gt;&gt; targetPredicate = sourcePredicate.MapToFacet&lt;UserDto&gt;();
    /// </code>
    /// </example>
    public static Expression<Func<TTarget, bool>> MapToFacet<TTarget>(
        this LambdaExpression sourcePredicate)
        where TTarget : class
    {
        if (sourcePredicate == null) throw new ArgumentNullException(nameof(sourcePredicate));
        
        var sourceType = sourcePredicate.Parameters[0].Type;
        var targetType = typeof(TTarget);
        
        var mapper = new ExpressionMapper(sourceType, targetType);
        var transformedBody = mapper.Transform(sourcePredicate.Body);
        var targetParameter = Expression.Parameter(targetType, sourcePredicate.Parameters[0].Name);
        
        var transformedBodyWithParameter = new ParameterReplacer(
            sourcePredicate.Parameters[0], targetParameter).Visit(transformedBody);
            
        return Expression.Lambda<Func<TTarget, bool>>(transformedBodyWithParameter!, targetParameter);
    }

    /// <summary>
    /// Transforms a selector expression from a source type to work with a Facet target type.
    /// </summary>
    /// <typeparam name="TTarget">The Facet target type</typeparam>
    /// <typeparam name="TResult">The result type of the selector</typeparam>
    /// <param name="sourceSelector">The original selector expression</param>
    /// <returns>A transformed selector that works with the target type</returns>
    /// <example>
    /// <code>
    /// Expression&lt;Func&lt;User, string&gt;&gt; sourceSelector = u => u.LastName;
    /// Expression&lt;Func&lt;UserDto, string&gt;&gt; targetSelector = sourceSelector.MapToFacet&lt;UserDto, string&gt;();
    /// </code>
    /// </example>
    public static Expression<Func<TTarget, TResult>> MapToFacet<TTarget, TResult>(
        this LambdaExpression sourceSelector)
        where TTarget : class
    {
        if (sourceSelector == null) throw new ArgumentNullException(nameof(sourceSelector));
        
        var sourceType = sourceSelector.Parameters[0].Type;
        var targetType = typeof(TTarget);
        
        var mapper = new ExpressionMapper(sourceType, targetType);
        var transformedBody = mapper.Transform(sourceSelector.Body);
        var targetParameter = Expression.Parameter(targetType, sourceSelector.Parameters[0].Name);
        
        var transformedBodyWithParameter = new ParameterReplacer(
            sourceSelector.Parameters[0], targetParameter).Visit(transformedBody);
            
        return Expression.Lambda<Func<TTarget, TResult>>(transformedBodyWithParameter!, targetParameter);
    }

    /// <summary>
    /// Transforms any lambda expression from a source type to work with a Facet target type.
    /// This is the most general form that can handle any expression shape.
    /// </summary>
    /// <typeparam name="TTarget">The Facet target type</typeparam>
    /// <param name="sourceExpression">The original lambda expression</param>
    /// <returns>A transformed lambda expression that works with the target type</returns>
    /// <example>
    /// <code>
    /// Expression&lt;Func&lt;User, object&gt;&gt; sourceExpr = u => new { u.FirstName, u.Age };
    /// Expression&lt;Func&lt;UserDto, object&gt;&gt; targetExpr = sourceExpr.MapToFacetGeneric&lt;UserDto&gt;();
    /// </code>
    /// </example>
    public static LambdaExpression MapToFacetGeneric<TTarget>(
        this LambdaExpression sourceExpression)
        where TTarget : class
    {
        if (sourceExpression == null) throw new ArgumentNullException(nameof(sourceExpression));
        
        var sourceType = sourceExpression.Parameters[0].Type;
        var targetType = typeof(TTarget);
        
        var mapper = new ExpressionMapper(sourceType, targetType);
        var transformedBody = mapper.Transform(sourceExpression.Body);
        var targetParameter = Expression.Parameter(targetType, sourceExpression.Parameters[0].Name);
        
        var transformedBodyWithParameter = new ParameterReplacer(
            sourceExpression.Parameters[0], targetParameter).Visit(transformedBody);
            
        return Expression.Lambda(transformedBodyWithParameter, targetParameter);
    }

    /// <summary>
    /// Combines multiple predicate expressions with AND logic.
    /// </summary>
    /// <typeparam name="T">The input type for the predicates</typeparam>
    /// <param name="predicates">The predicates to combine</param>
    /// <returns>A single predicate expression that represents the AND of all input predicates</returns>
    /// <example>
    /// <code>
    /// var predicate1 = (Expression&lt;Func&lt;User, bool&gt;&gt;)(u => u.IsActive);
    /// var predicate2 = (Expression&lt;Func&lt;User, bool&gt;&gt;)(u => u.Age > 18);
    /// var combined = FacetExpressionExtensions.CombineWithAnd(predicate1, predicate2);
    /// // Result: u => u.IsActive && u.Age > 18
    /// </code>
    /// </example>
    public static Expression<Func<T, bool>> CombineWithAnd<T>(
        params Expression<Func<T, bool>>[] predicates)
    {
        if (predicates == null) throw new ArgumentNullException(nameof(predicates));
        if (predicates.Length == 0) return x => true;
        if (predicates.Length == 1) return predicates[0];

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression body = null;

        foreach (var predicate in predicates)
        {
            var predicateBody = new ParameterReplacer(predicate.Parameters[0], parameter)
                .Visit(predicate.Body);
            body = body == null ? predicateBody : Expression.AndAlso(body, predicateBody);
        }

        return Expression.Lambda<Func<T, bool>>(body!, parameter);
    }

    /// <summary>
    /// Combines multiple predicate expressions with OR logic.
    /// </summary>
    /// <typeparam name="T">The input type for the predicates</typeparam>
    /// <param name="predicates">The predicates to combine</param>
    /// <returns>A single predicate expression that represents the OR of all input predicates</returns>
    /// <example>
    /// <code>
    /// var predicate1 = (Expression&lt;Func&lt;User, bool&gt;&gt;)(u => u.IsVip);
    /// var predicate2 = (Expression&lt;Func&lt;User, bool&gt;&gt;)(u => u.Age > 65);
    /// var combined = FacetExpressionExtensions.CombineWithOr(predicate1, predicate2);
    /// // Result: u => u.IsVip || u.Age > 65
    /// </code>
    /// </example>
    public static Expression<Func<T, bool>> CombineWithOr<T>(
        params Expression<Func<T, bool>>[] predicates)
    {
        if (predicates == null) throw new ArgumentNullException(nameof(predicates));
        if (predicates.Length == 0) return x => false;
        if (predicates.Length == 1) return predicates[0];

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression body = null;

        foreach (var predicate in predicates)
        {
            var predicateBody = new ParameterReplacer(predicate.Parameters[0], parameter)
                .Visit(predicate.Body);
            body = body == null ? predicateBody : Expression.OrElse(body, predicateBody);
        }

        return Expression.Lambda<Func<T, bool>>(body!, parameter);
    }

    /// <summary>
    /// Creates a negated version of the given predicate expression.
    /// </summary>
    /// <typeparam name="T">The input type for the predicate</typeparam>
    /// <param name="predicate">The predicate to negate</param>
    /// <returns>A predicate expression that represents the logical NOT of the input</returns>
    /// <example>
    /// <code>
    /// Expression&lt;Func&lt;User, bool&gt;&gt; predicate = u => u.IsActive;
    /// Expression&lt;Func&lt;User, bool&gt;&gt; negated = predicate.Negate();
    /// // Result: u => !u.IsActive
    /// </code>
    /// </example>
    public static Expression<Func<T, bool>> Negate<T>(
        this Expression<Func<T, bool>> predicate)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));

        var parameter = predicate.Parameters[0];
        var negatedBody = Expression.Not(predicate.Body);
        
        return Expression.Lambda<Func<T, bool>>(negatedBody, parameter);
    }
}