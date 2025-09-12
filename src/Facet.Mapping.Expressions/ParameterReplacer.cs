using System.Collections.Generic;
using System.Linq.Expressions;

namespace Facet.Mapping.Expressions;

/// <summary>
/// Expression visitor that replaces parameter expressions with new parameter expressions.
/// Used to substitute parameters when transforming lambda expressions between different types.
/// </summary>
internal class ParameterReplacer : ExpressionVisitor
{
    private readonly Dictionary<ParameterExpression, ParameterExpression> _parameterMap;

    /// <summary>
    /// Initializes a new instance that replaces a single parameter.
    /// </summary>
    /// <param name="oldParameter">The parameter to replace</param>
    /// <param name="newParameter">The parameter to replace it with</param>
    public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
    {
        _parameterMap = new Dictionary<ParameterExpression, ParameterExpression>
        {
            { oldParameter, newParameter }
        };
    }

    /// <summary>
    /// Initializes a new instance that can replace multiple parameters.
    /// </summary>
    /// <param name="parameterMap">Dictionary mapping old parameters to new parameters</param>
    public ParameterReplacer(Dictionary<ParameterExpression, ParameterExpression> parameterMap)
    {
        _parameterMap = parameterMap ?? new Dictionary<ParameterExpression, ParameterExpression>();
    }

    /// <summary>
    /// Visits parameter expressions and replaces them if they're in the mapping.
    /// </summary>
    /// <param name="node">The parameter expression to visit</param>
    /// <returns>The replacement parameter if found, otherwise the original parameter</returns>
    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (_parameterMap.TryGetValue(node, out var replacement))
        {
            return replacement;
        }
        
        return base.VisitParameter(node);
    }
}