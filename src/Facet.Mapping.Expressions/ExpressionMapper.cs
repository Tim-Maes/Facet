using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Facet.Mapping.Expressions;

/// <summary>
/// Core engine for transforming expression trees between source types and their Facet projections.
/// Handles property mapping, method calls, and complex expression patterns.
/// </summary>
internal class ExpressionMapper : ExpressionVisitor
{
    private readonly Type _sourceType;
    private readonly Type _targetType;
    private readonly PropertyPathMapper _propertyMapper;
    private readonly Dictionary<ParameterExpression, ParameterExpression> _parameterMap;

    // Cache for reflected property information to improve performance
    private static readonly ConcurrentDictionary<(Type Source, Type Target), PropertyPathMapper> 
        _propertyMapperCache = new();

    public ExpressionMapper(Type sourceType, Type targetType)
    {
        _sourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        _targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        _propertyMapper = _propertyMapperCache.GetOrAdd(
            (sourceType, targetType), 
            key => new PropertyPathMapper(key.Source, key.Target));
        _parameterMap = new Dictionary<ParameterExpression, ParameterExpression>();
    }

    /// <summary>
    /// Transforms an expression from the source type context to the target type context.
    /// </summary>
    /// <param name="expression">The expression to transform</param>
    /// <returns>The transformed expression</returns>
    public Expression Transform(Expression expression)
    {
        return Visit(expression);
    }

    /// <summary>
    /// Handles parameter expressions by mapping them to the target type.
    /// </summary>
    protected override Expression VisitParameter(ParameterExpression node)
    {
        // If we already have a mapping for this parameter, use it
        if (_parameterMap.TryGetValue(node, out var mappedParameter))
        {
            return mappedParameter;
        }

        // If this parameter is of the source type, map it to the target type
        if (node.Type == _sourceType)
        {
            var targetParameter = Expression.Parameter(_targetType, node.Name);
            _parameterMap[node] = targetParameter;
            return targetParameter;
        }

        return base.VisitParameter(node);
    }

    /// <summary>
    /// Handles member access expressions (property access) by mapping between source and target properties.
    /// </summary>
    protected override Expression VisitMember(MemberExpression node)
    {
        // Visit the expression that the member is accessed on
        var expression = Visit(node.Expression);
        
        // If we're accessing a member on the source type, map it to the target type
        if (node.Expression?.Type == _sourceType && expression?.Type == _targetType)
        {
            var sourceMember = node.Member;
            var targetMember = _propertyMapper.MapProperty(sourceMember);
            
            if (targetMember != null)
            {
                return Expression.MakeMemberAccess(expression, targetMember);
            }
            
            // If we can't map the property, throw an informative error
            throw new InvalidOperationException(
                $"Property '{sourceMember.Name}' on type '{_sourceType.Name}' " +
                $"could not be mapped to type '{_targetType.Name}'. " +
                $"Ensure the property exists in the Facet projection.");
        }

        // For nested member access, handle recursively
        if (expression != node.Expression)
        {
            return Expression.MakeMemberAccess(expression, node.Member);
        }

        return base.VisitMember(node);
    }

    /// <summary>
    /// Handles method call expressions, attempting to preserve method calls where possible.
    /// </summary>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Handle static method calls
        if (node.Object == null)
        {
            var args = node.Arguments.Select(Visit).Where(a => a != null).ToArray();
            if (args.SequenceEqual(node.Arguments))
            {
                return node; // No change needed
            }
            return Expression.Call(node.Method, args);
        }

        // Handle instance method calls
        var obj = Visit(node.Object);
        var arguments = node.Arguments.Select(Visit).Where(a => a != null).ToArray();

        // If the object type changed, we need to find the equivalent method on the new type
        if (obj.Type != node.Object.Type)
        {
            var equivalentMethod = FindEquivalentMethod(node.Method, obj.Type);
            if (equivalentMethod != null)
            {
                return Expression.Call(obj, equivalentMethod, arguments);
            }
        }

        if (obj != node.Object || !arguments.SequenceEqual(node.Arguments))
        {
            return Expression.Call(obj, node.Method, arguments);
        }

        return base.VisitMethodCall(node);
    }

    /// <summary>
    /// Handles lambda expressions by creating new parameters and mapping the body.
    /// </summary>
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        var parameters = new List<ParameterExpression>();
        
        foreach (var param in node.Parameters)
        {
            if (param.Type == _sourceType)
            {
                var targetParam = Expression.Parameter(_targetType, param.Name);
                _parameterMap[param] = targetParam;
                parameters.Add(targetParam);
            }
            else
            {
                parameters.Add(param);
            }
        }

        var body = Visit(node.Body);
        return Expression.Lambda(body, parameters);
    }

    /// <summary>
    /// Handles binary expressions (comparisons, arithmetic, logical operations).
    /// </summary>
    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);

        if (left != node.Left || right != node.Right)
        {
            // Handle type mismatches that might occur during transformation
            if (left.Type != node.Left.Type || right.Type != node.Right.Type)
            {
                return CreateCompatibleBinaryExpression(node.NodeType, left, right);
            }
            return Expression.MakeBinary(node.NodeType, left, right);
        }

        return base.VisitBinary(node);
    }

    /// <summary>
    /// Handles unary expressions (negation, conversion, etc.).
    /// </summary>
    protected override Expression VisitUnary(UnaryExpression node)
    {
        var operand = Visit(node.Operand);
        
        if (operand != node.Operand)
        {
            return Expression.MakeUnary(node.NodeType, operand, node.Type);
        }

        return base.VisitUnary(node);
    }

    /// <summary>
    /// Attempts to find an equivalent method on the target type.
    /// </summary>
    private static MethodInfo? FindEquivalentMethod(MethodInfo originalMethod, Type targetType)
    {
        try
        {
            return targetType.GetMethod(
                originalMethod.Name,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                originalMethod.GetParameters().Select(p => p.ParameterType).ToArray(),
                null);
        }
        catch
        {
            // If we can't find an exact match, try by name only
            return targetType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == originalMethod.Name && 
                                   m.GetParameters().Length == originalMethod.GetParameters().Length);
        }
    }

    /// <summary>
    /// Creates a binary expression handling type compatibility issues.
    /// </summary>
    private static Expression CreateCompatibleBinaryExpression(ExpressionType nodeType, Expression left, Expression right)
    {
        // Handle common type mismatches by inserting appropriate conversions
        if (left.Type != right.Type)
        {
            // Try to convert to a common type
            if (left.Type.IsAssignableFrom(right.Type))
            {
                right = Expression.Convert(right, left.Type);
            }
            else if (right.Type.IsAssignableFrom(left.Type))
            {
                left = Expression.Convert(left, right.Type);
            }
        }

        return Expression.MakeBinary(nodeType, left, right);
    }
}