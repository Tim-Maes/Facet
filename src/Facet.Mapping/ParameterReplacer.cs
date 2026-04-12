using System.Linq.Expressions;

namespace Facet.Mapping;

/// <summary>
/// An <see cref="ExpressionVisitor"/> that substitutes every occurrence of one
/// <see cref="ParameterExpression"/> with another.  Used by <c>BuildProjection()</c>
/// to inline user-declared lambda bodies into the outer projection parameter so that
/// the final expression tree contains no <c>Invoke</c> nodes — which EF Core cannot translate.
/// </summary>
public sealed class ParameterReplacer : ExpressionVisitor
{
    private readonly ParameterExpression _from, _to;

    private ParameterReplacer(ParameterExpression from, ParameterExpression to)
        => (_from, _to) = (from, to);

    /// <summary>
    /// Returns <paramref name="expr"/>.Body with every occurrence of its first parameter
    /// replaced by <paramref name="newParam"/>.
    /// </summary>
    public static Expression Replace(LambdaExpression expr, ParameterExpression newParam)
        => new ParameterReplacer(expr.Parameters[0], newParam).Visit(expr.Body)!;

    protected override Expression VisitParameter(ParameterExpression node)
        => node == _from ? _to : base.VisitParameter(node);
}
