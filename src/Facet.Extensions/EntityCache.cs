using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Facet;

/// <summary>
/// Provides a cached <see cref="Func{TFacet, TEntity}"/> mapping delegate used by
/// <c>ToEntity&lt;TFacet, TEntity&gt;</c> to efficiently construct <typeparamref name="TEntity"/> instances
/// from <typeparamref name="TFacet"/> values.
/// </summary>
/// <typeparam name="TFacet">The facet type that is annotated with [Facet(typeof(TEntity))].</typeparam>
/// <typeparam name="TEntity">
/// The target entity type. Must expose either a public static <c>FromFacet(<typeparamref name="TFacet"/>)</c>
/// factory method, or a public constructor accepting a <typeparamref name="TFacet"/> instance.
/// </typeparam>
/// <remarks>
/// This type performs reflection only once per <typeparamref name="TFacet"/> / <typeparamref name="TEntity"/>
/// combination, precompiling a delegate for reuse in all subsequent mappings.
/// </remarks>
/// <exception cref="InvalidOperationException">
/// Thrown when no usable <c>FromFacet</c> factory or compatible constructor is found on <typeparamref name="TEntity"/>.
/// </exception>
internal static class EntityCache<TFacet, TEntity>
    where TFacet : class
    where TEntity : class
{
    public static readonly Func<TFacet, TEntity> Mapper = CreateMapper();

    private static Func<TFacet, TEntity> CreateMapper()
    {
        // Look for the ToEntity() method on the facet type
        var toEntityMethod = typeof(TFacet).GetMethod(
            "ToEntity",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        if (toEntityMethod != null && toEntityMethod.ReturnType == typeof(TEntity))
        {
            // Create a delegate that calls the ToEntity() method on the facet instance
            var param = Expression.Parameter(typeof(TFacet), "facet");
            var call = Expression.Call(param, toEntityMethod);
            return Expression.Lambda<Func<TFacet, TEntity>>(call, param).Compile();
        }

        // If no ToEntity method is found, provide a helpful error message
        throw new InvalidOperationException(
            $"Unable to map {typeof(TFacet).Name} to {typeof(TEntity).Name}: " +
            $"no ToEntity() method found on the facet type. Ensure the facet is properly generated with source generation.");
    }
}
