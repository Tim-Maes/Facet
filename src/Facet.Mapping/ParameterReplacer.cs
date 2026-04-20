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

    /// <summary>
    /// Returns <paramref name="expr"/>.Body with every occurrence of its first parameter
    /// replaced by <paramref name="replacement"/> (any expression, not just a parameter).
    /// Used to inline nested facet projections into a parent expression tree by replacing
    /// the nested lambda's source parameter with a property access on the outer source.
    /// </summary>
    public static Expression ReplaceParameter(LambdaExpression expr, Expression replacement)
        => new GeneralReplacer(expr.Parameters[0], replacement).Visit(expr.Body)!;

    protected override Expression VisitParameter(ParameterExpression node)
        => node == _from ? _to : base.VisitParameter(node);

    private sealed class GeneralReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly Expression _to;

        public GeneralReplacer(ParameterExpression from, Expression to)
            => (_from, _to) = (from, to);

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _from ? _to : base.VisitParameter(node);
    }
}
