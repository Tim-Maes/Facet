using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Facet;

/// <summary>
/// Provides a cached <see cref="Func{TSource, TTarget}"/> mapping delegate used by
/// <c>ToFacet&lt;TSource, TTarget&gt;</c> to efficiently construct <typeparamref name="TTarget"/> instances
/// from <typeparamref name="TSource"/> values.
/// </summary>
/// <typeparam name="TSource">The source model type.</typeparam>
/// <typeparam name="TTarget">
/// The target DTO or facet type. Must expose either a public static <c>FromSource(<typeparamref name="TSource"/>)</c>
/// factory method, or a public constructor accepting a <typeparamref name="TSource"/> instance.
/// </typeparam>
/// <remarks>
/// This type performs reflection only once per <typeparamref name="TSource"/> / <typeparamref name="TTarget"/>
/// combination, precompiling a delegate for reuse in all subsequent mappings.
/// </remarks>
/// <exception cref="InvalidOperationException">
/// Thrown when no usable <c>FromSource</c> factory or compatible constructor is found on <typeparamref name="TTarget"/>.
/// </exception>
internal static class FacetCache<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TTarget>
    where TTarget : class
{
    public static readonly Func<TSource, TTarget> Mapper = CreateMapper();

    private static Func<TSource, TTarget> CreateMapper()
    {
        // Check for static FromSource factory method first (preferred for init-only properties)
        var fromSource = typeof(TTarget).GetMethod(
            "FromSource",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(TSource) },
            null);

        if (fromSource != null)
        {
            return (Func<TSource, TTarget>)Delegate.CreateDelegate(
                typeof(Func<TSource, TTarget>), fromSource);
        }

        var ctor = typeof(TTarget).GetConstructor(new[] { typeof(TSource) });

        if (ctor != null)
        {
            // Use compiled expression instead of DynamicMethod for AOT/trimming compatibility
            var param = Expression.Parameter(typeof(TSource), "source");
            var newExpr = Expression.New(ctor, param);
            var lambda = Expression.Lambda<Func<TSource, TTarget>>(newExpr, param);
            return lambda.Compile();
        }

        // If neither works, provide a helpful error message
        throw new InvalidOperationException(
            $"Unable to map {typeof(TSource).Name} to {typeof(TTarget).Name}: " +
            $"no compatible FromSource or ctor found.");
    }
}