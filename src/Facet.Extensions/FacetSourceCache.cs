using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Facet;

/// <summary>
/// Provides a cached <see cref="Func{TFacet, TFacetSource}"/> mapping delegate used by
/// <c>ToSource&lt;TFacet, TFacetSource&gt;</c> to efficiently construct <typeparamref name="TFacetSource"/> instances
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
        // Look for the ToSource() method first (new name), then BackTo() for backwards compatibility
        var toEntityMethod = typeof(TFacet).GetMethod(
            "ToSource",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        // Fall back to BackTo for backwards compatibility with older generated code
        toEntityMethod ??= typeof(TFacet).GetMethod(
            "BackTo",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        if (toEntityMethod != null && toEntityMethod.ReturnType == typeof(TFacetSource))
        {
            var method = new DynamicMethod(
                name: $"Call_{typeof(TFacet).Name}_ToSource",
                returnType: typeof(TFacetSource),
                parameterTypes: new[] { typeof(TFacet) },
                m: typeof(FacetSourceCache<TFacet, TFacetSource>).Module,
                skipVisibility: true);

            var il = method.GetILGenerator();

            // Load the facet parameter onto the stack
            il.Emit(OpCodes.Ldarg_0);

            // Call the ToSource/BackTo method
            il.Emit(OpCodes.Callvirt, toEntityMethod);

            // Return the result
            il.Emit(OpCodes.Ret);

            return (Func<TFacet, TFacetSource>)method.CreateDelegate(typeof(Func<TFacet, TFacetSource>));
        }

        // If no ToSource/BackTo method is found, provide a helpful error message
        throw new InvalidOperationException(
            $"Unable to map {typeof(TFacet).Name} to {typeof(TFacetSource).Name}: " +
            $"no ToSource() method found on the facet type. Ensure the facet is properly generated with source generation.");
    }
}
