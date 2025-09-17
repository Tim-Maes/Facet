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
internal class ExpressionMapper
{
    private readonly Type _sourceType;
    private readonly Type _targetType;
    private readonly PropertyPathMapper _propertyMapper;

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
    }

    /// <summary>
    /// Transforms an expression from the source type context to the target type context.
    /// </summary>
    /// <param name="expression">The expression to transform</param>
    /// <returns>The transformed expression</returns>
    public Expression Transform(Expression expression)
    {
        if (expression is LambdaExpression lambda)
        {
            return TransformLambda(lambda);
        }
        
        return TransformExpression(expression, new Dictionary<ParameterExpression, ParameterExpression>());
    }

    /// <summary>
    /// Transforms a lambda expression by replacing parameters and transforming the body.
    /// </summary>
    private LambdaExpression TransformLambda(LambdaExpression lambda)
    {
        var parameterMap = new Dictionary<ParameterExpression, ParameterExpression>();
        var newParameters = new ParameterExpression[lambda.Parameters.Count];

        // Create new parameters and build the mapping
        for (int i = 0; i < lambda.Parameters.Count; i++)
        {
            var oldParam = lambda.Parameters[i];
            if (oldParam.Type == _sourceType)
            {
                var newParam = Expression.Parameter(_targetType, oldParam.Name);
                newParameters[i] = newParam;
                parameterMap[oldParam] = newParam;
            }
            else
            {
                newParameters[i] = oldParam;
            }
        }

        // Transform the lambda body
        var newBody = TransformExpression(lambda.Body, parameterMap);
        
        // Create the new lambda
        return Expression.Lambda(newBody, newParameters);
    }

    /// <summary>
    /// Transforms an expression tree recursively.
    /// </summary>
    private Expression TransformExpression(Expression expression, Dictionary<ParameterExpression, ParameterExpression> parameterMap)
    {
        if (expression == null)
            return null!;

        switch (expression.NodeType)
        {
            case ExpressionType.Parameter:
                return TransformParameter((ParameterExpression)expression, parameterMap);
                
            case ExpressionType.MemberAccess:
                return TransformMemberAccess((MemberExpression)expression, parameterMap);
                
            case ExpressionType.Call:
                return TransformMethodCall((MethodCallExpression)expression, parameterMap);
                
            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
            case ExpressionType.AndAlso:
            case ExpressionType.OrElse:
            case ExpressionType.Add:
            case ExpressionType.Subtract:
            case ExpressionType.Multiply:
            case ExpressionType.Divide:
                return TransformBinary((BinaryExpression)expression, parameterMap);
                
            case ExpressionType.Not:
            case ExpressionType.Convert:
            case ExpressionType.ConvertChecked:
                return TransformUnary((UnaryExpression)expression, parameterMap);
                
            case ExpressionType.Constant:
                return expression; // Constants don't need transformation
                
            case ExpressionType.New:
                return TransformNew((NewExpression)expression, parameterMap);
                
            case ExpressionType.MemberInit:
                return TransformMemberInit((MemberInitExpression)expression, parameterMap);
                
            case ExpressionType.Conditional:
                return TransformConditional((ConditionalExpression)expression, parameterMap);
                
            default:
                // For any other expression type, try to handle it generically
                return expression;
        }
    }

    private Expression TransformParameter(ParameterExpression parameter, Dictionary<ParameterExpression, ParameterExpression> parameterMap)
    {
        if (parameterMap.TryGetValue(parameter, out var mapped))
        {
            return mapped;
        }
        return parameter;
    }

    private Expression TransformMemberAccess(MemberExpression memberExpression, Dictionary<ParameterExpression, ParameterExpression> parameterMap)
    {
        var transformedExpression = TransformExpression(memberExpression.Expression, parameterMap);
        
        // If the member access is on a source type parameter, map to target type
        if (memberExpression.Expression?.Type == _sourceType && transformedExpression?.Type == _targetType)
        {
            var sourceMember = memberExpression.Member;
            
            // Always look for the property directly on the target type by name
            // This ensures we get the correct PropertyInfo that belongs to the target type
            var targetProperty = _targetType.GetProperty(sourceMember.Name, BindingFlags.Public | BindingFlags.Instance);
            var targetField = _targetType.GetField(sourceMember.Name, BindingFlags.Public | BindingFlags.Instance);
            
            MemberInfo? targetMember = targetProperty ?? (MemberInfo?)targetField;
            
            if (targetMember != null)
            {
                // Verify type compatibility
                var sourceType = GetMemberType(sourceMember);
                var targetType = GetMemberType(targetMember);
                
                if (sourceType != null && targetType != null && IsCompatibleType(sourceType, targetType))
                {
                    return Expression.MakeMemberAccess(transformedExpression, targetMember);
                }
            }
            
            throw new InvalidOperationException(
                $"Property '{sourceMember.Name}' on type '{_sourceType.Name}' " +
                $"could not be mapped to type '{_targetType.Name}'. " +
                $"Ensure the property exists in the Facet projection.");
        }
        
        // If the expression changed, create new member access
        if (transformedExpression != memberExpression.Expression)
        {
            return Expression.MakeMemberAccess(transformedExpression, memberExpression.Member);
        }
        
        return memberExpression;
    }

    private Expression TransformMethodCall(MethodCallExpression methodCall, Dictionary<ParameterExpression, ParameterExpression> parameterMap)
    {
        var transformedObject = methodCall.Object != null 
            ? TransformExpression(methodCall.Object, parameterMap) 
            : null;
            
        var transformedArguments = methodCall.Arguments
            .Select(arg => TransformExpression(arg, parameterMap))
            .ToArray();

        // Handle static method calls
        if (methodCall.Object == null)
        {
            if (transformedArguments.SequenceEqual(methodCall.Arguments))
                return methodCall;
            return Expression.Call(methodCall.Method, transformedArguments);
        }

        // Handle instance method calls
        if (transformedObject!.Type != methodCall.Object!.Type)
        {
            // Try to find equivalent method on the new type
            var equivalentMethod = FindEquivalentMethod(methodCall.Method, transformedObject.Type);
            if (equivalentMethod != null)
            {
                return Expression.Call(transformedObject, equivalentMethod, transformedArguments);
            }
        }

        if (transformedObject != methodCall.Object || !transformedArguments.SequenceEqual(methodCall.Arguments))
        {
            return Expression.Call(transformedObject, methodCall.Method, transformedArguments);
        }

        return methodCall;
    }

    private Expression TransformBinary(BinaryExpression binary, Dictionary<ParameterExpression, ParameterExpression> parameterMap)
    {
        var left = TransformExpression(binary.Left, parameterMap);
        var right = TransformExpression(binary.Right, parameterMap);

        if (left != binary.Left || right != binary.Right)
        {
            return Expression.MakeBinary(binary.NodeType, left, right, binary.IsLiftedToNull, binary.Method);
        }

        return binary;
    }

    private Expression TransformUnary(UnaryExpression unary, Dictionary<ParameterExpression, ParameterExpression> parameterMap)
    {
        var operand = TransformExpression(unary.Operand, parameterMap);
        
        if (operand != unary.Operand)
        {
            return Expression.MakeUnary(unary.NodeType, operand, unary.Type, unary.Method);
        }

        return unary;
    }

    private Expression TransformNew(NewExpression newExpression, Dictionary<ParameterExpression, ParameterExpression> parameterMap)
    {
        var transformedArguments = newExpression.Arguments
            .Select(arg => TransformExpression(arg, parameterMap))
            .ToArray();

        if (!transformedArguments.SequenceEqual(newExpression.Arguments))
        {
            return newExpression.Members != null
                ? Expression.New(newExpression.Constructor!, transformedArguments, newExpression.Members)
                : Expression.New(newExpression.Constructor!, transformedArguments);
        }

        return newExpression;
    }

    private Expression TransformMemberInit(MemberInitExpression memberInit, Dictionary<ParameterExpression, ParameterExpression> parameterMap)
    {
        var transformedNew = (NewExpression)TransformExpression(memberInit.NewExpression, parameterMap);
        var transformedBindings = memberInit.Bindings.Select(binding =>
        {
            if (binding is MemberAssignment assignment)
            {
                var transformedValue = TransformExpression(assignment.Expression, parameterMap);
                return transformedValue != assignment.Expression
                    ? Expression.Bind(assignment.Member, transformedValue)
                    : assignment;
            }
            return binding;
        });

        return Expression.MemberInit(transformedNew, transformedBindings);
    }

    private Expression TransformConditional(ConditionalExpression conditional, Dictionary<ParameterExpression, ParameterExpression> parameterMap)
    {
        var test = TransformExpression(conditional.Test, parameterMap);
        var ifTrue = TransformExpression(conditional.IfTrue, parameterMap);
        var ifFalse = TransformExpression(conditional.IfFalse, parameterMap);

        if (test != conditional.Test || ifTrue != conditional.IfTrue || ifFalse != conditional.IfFalse)
        {
            return Expression.Condition(test, ifTrue, ifFalse);
        }

        return conditional;
    }

    /// <summary>
    /// Gets the type of a member (property or field).
    /// </summary>
    private static Type? GetMemberType(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => null
        };
    }

    /// <summary>
    /// Checks if two types are compatible for expression mapping.
    /// </summary>
    private static bool IsCompatibleType(Type sourceType, Type targetType)
    {
        // Exact type match
        if (sourceType == targetType) return true;
        
        // Check if types are assignable
        if (sourceType.IsAssignableFrom(targetType) || targetType.IsAssignableFrom(sourceType))
            return true;
            
        // Handle nullable/non-nullable variations
        var sourceNonNullable = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
        var targetNonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
        
        return sourceNonNullable == targetNonNullable;
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
}