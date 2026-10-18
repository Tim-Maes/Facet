using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace Facet.Mapping;

/// <summary>
/// Merges the rules an <see cref="IFacetProjectionMapConfiguration{TSource, TTarget}"/> declares
/// into a generated projection, keeping the result a single translatable expression.
/// </summary>
/// <remarks>
/// <para>
/// A generated projection is an object initializer — <c>source =&gt; new Target { A = source.A }</c>
/// — which is exactly the shape a query provider turns into a <c>SELECT</c> list. A custom rule
/// must therefore be folded into that initializer rather than applied afterwards: calling a method
/// on the result, however small, is the thing that makes a query fall back to loading rows and
/// mapping them in memory.
/// </para>
/// <para>
/// So this takes the bindings apart, substitutes the configuration's own lambda parameter for the
/// projection's, and rebuilds one <see cref="MemberInitExpression"/>. A rule for a member the
/// generated projection already binds replaces it, which is what makes a configuration an override
/// rather than a conflict.
/// </para>
/// </remarks>
public static class FacetProjectionMerge
{
    /// <summary>
    /// Returns <paramref name="projection"/> with <paramref name="builder"/>'s rules applied.
    /// </summary>
    /// <param name="projection">
    /// The generated projection. Its body must be an object initializer; anything else is returned
    /// unchanged, since there are no bindings to merge into.
    /// </param>
    /// <param name="builder">The builder a <c>ConfigureProjection</c> implementation wrote to.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public static Expression<Func<TSource, TTarget>> Apply<TSource, TTarget>(
        Expression<Func<TSource, TTarget>> projection,
        FacetProjectionBuilder<TSource, TTarget> builder)
    {
        if (projection is null) throw new ArgumentNullException(nameof(projection));
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        if (builder.Mappings.Count == 0)
            return projection;

        if (projection.Body is not MemberInitExpression init)
            return projection;

        var parameter = projection.Parameters[0];
        var bindings = init.Bindings.ToList();

        foreach (var (member, valueExpression) in builder.Mappings)
        {
            // The rule was written against its own parameter; rebind it to the projection's so the
            // whole thing stays one lambda over one parameter.
            var value = ParameterReplacer.Replace(valueExpression, parameter);

            // A member the generated projection already binds: the configuration is the override.
            bindings.RemoveAll(b => MemberMatches(b, member));
            bindings.Add(Expression.Bind(member, Coerce(value, member)));
        }

        return Expression.Lambda<Func<TSource, TTarget>>(
            Expression.MemberInit(init.NewExpression, bindings),
            parameter);
    }

    /// <summary>
    /// Compiles <paramref name="builder"/>'s rules into assignments against an already-constructed
    /// target, for the runtime mapper.
    /// </summary>
    /// <remarks>
    /// The same rules have to apply on both paths, or a value would depend on whether it arrived
    /// through a query or through a method call. The projection folds them into its initializer;
    /// here they become plain assignments, compiled once and cached by the caller.
    /// </remarks>
    [RequiresDynamicCode("Compiling an expression tree requires dynamic code generation. The Projection built by Apply does not; only applying its rules inside the generated To{Facet} method does. Declare imperative rules in IFacetMapConfiguration.Map instead to avoid it.")]
    public static Action<TSource, TTarget> CompileAssignments<TSource, TTarget>(
        FacetProjectionBuilder<TSource, TTarget> builder)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        if (builder.Mappings.Count == 0)
            return static (_, _) => { };

        var source = Expression.Parameter(typeof(TSource), "source");
        var target = Expression.Parameter(typeof(TTarget), "target");
        var assignments = new List<Expression>(builder.Mappings.Count);

        foreach (var (member, valueExpression) in builder.Mappings)
        {
            var value = ParameterReplacer.Replace(valueExpression, source);

            Expression destination = member switch
            {
                System.Reflection.PropertyInfo property => Expression.Property(target, property),
                System.Reflection.FieldInfo field => Expression.Field(target, field),
                _ => throw new NotSupportedException(
                    $"A projection rule can only target a property or field; '{member.Name}' is a {member.MemberType}."),
            };

            assignments.Add(Expression.Assign(destination, Coerce(value, member)));
        }

        return Expression.Lambda<Action<TSource, TTarget>>(
            Expression.Block(assignments), source, target).Compile();
    }

    private static bool MemberMatches(MemberBinding binding, System.Reflection.MemberInfo member) =>
        binding.Member.Name == member.Name;

    /// <summary>
    /// Adds the conversion a hand-written rule may leave implicit — <c>int</c> to <c>long?</c>, say.
    /// <c>Expression.Bind</c> demands an exact assignable match, where C# would have
    /// inserted the conversion itself.
    /// </summary>
    private static Expression Coerce(Expression value, System.Reflection.MemberInfo member)
    {
        var targetType = member switch
        {
            System.Reflection.PropertyInfo property => property.PropertyType,
            System.Reflection.FieldInfo field => field.FieldType,
            _ => null,
        };

        if (targetType is null || targetType == value.Type)
            return value;

        return targetType.IsAssignableFrom(value.Type)
            ? value
            : Expression.Convert(value, targetType);
    }
}
