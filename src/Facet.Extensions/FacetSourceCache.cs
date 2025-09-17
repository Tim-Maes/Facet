using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Facet;

/// <summary>
/// Provides a cached <see cref="Func{TFacet, TFacetSource}"/> mapping delegate used by
/// <c>BackTo&lt;TFacet, TFacetSource&gt;</c> to efficiently construct <typeparamref name="TFacetSource"/> instances
/// from <typeparamref name="TFacet"/> values.
/// </summary>
/// <typeparam name="TFacet">The facet type that is annotated with [Facet(typeof(TFacetSource))].</typeparam>
/// <typeparam name="TFacetSource">
/// The target entity type. Must expose either a public static <c>FromFacet(<typeparamref name="TFacet"/>)</c>
/// factory method, or a public constructor accepting a <typeparamref name="TFacet"/> instance.
/// </typeparam>
/// <remarks>
/// This type performs reflection only once per <typeparamref name="TFacet"/> / <typeparamref name="TFacetSource"/>
/// combination, precompiling a delegate for reuse in all subsequent mappings.
/// </remarks>
/// <exception cref="InvalidOperationException">
/// Thrown when no usable <c>FromFacet</c> factory or compatible constructor is found on <typeparamref name="TFacetSource"/>.
/// </exception>
internal static class FacetSourceCache<TFacet, TFacetSource>
    where TFacet : class
    where TFacetSource : class
{
    public static readonly Func<TFacet, TFacetSource> Mapper = CreateMapper();

    private static Func<TFacet, TFacetSource> CreateMapper()
    {
        // Look for the BackTo() method on the facet type
        var toEntityMethod = typeof(TFacet).GetMethod(
            "BackTo",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        if (toEntityMethod != null && toEntityMethod.ReturnType == typeof(TFacetSource))
        {
            // Create a delegate that calls the BackTo() method on the facet instance
            var param = Expression.Parameter(typeof(TFacet), "facet");
            var call = Expression.Call(param, toEntityMethod);
            return Expression.Lambda<Func<TFacet, TFacetSource>>(call, param).Compile();
        }

        // If no BackTo method is found, provide a helpful error message
        throw new InvalidOperationException(
            $"Unable to map {typeof(TFacet).Name} to {typeof(TFacetSource).Name}: " +
            $"no BackTo() method found on the facet type. Ensure the facet is properly generated with source generation.");
    }
}
