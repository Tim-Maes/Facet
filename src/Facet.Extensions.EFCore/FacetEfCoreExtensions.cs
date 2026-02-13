using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Facet.Extensions.EFCore;
/// <summary>
/// Provides EF Core async extension methods for mapping source entities or sequences
/// to Facet-generated types.
/// </summary>
public static class FacetEfCoreExtensions
{
    /// <summary>
    /// Asynchronously projects an IQueryable&lt;TSource&gt; to a List&lt;TTarget&gt;
    /// using the generated Projection expression and Entity Framework Core's ToListAsync.
    /// </summary>
    public static Task<List<TTarget>> ToFacetsAsync<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TTarget>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return Facet.Extensions.FacetExtensions.SelectFacet<TSource, TTarget>(source)
                     .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously projects an IQueryable&lt;TSource&gt; to a List&lt;TTarget&gt;
    /// using the generated Projection expression and Entity Framework Core's ToListAsync.
    /// The source type is inferred from the query.
    /// </summary>
    public static Task<List<TTarget>> ToFacetsAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TTarget>(
        this IQueryable source,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return Facet.Extensions.FacetExtensions.SelectFacet<TTarget>(source)
                     .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously projects the first element of an IQueryable&lt;TSource&gt;
    /// to a facet, or returns null if none found, using Entity Framework Core's FirstOrDefaultAsync.
    /// </summary>
    public static Task<TTarget?> FirstFacetAsync<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TTarget>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return Facet.Extensions.FacetExtensions.SelectFacet<TSource, TTarget>(source)
                     .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously projects the first element of an IQueryable&lt;TSource&gt;
    /// to a facet, or returns null if none found, using Entity Framework Core's FirstOrDefaultAsync.
    /// The source type is inferred from the query.
    /// </summary>
    public static Task<TTarget?> FirstFacetAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TTarget>(
        this IQueryable source,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return Facet.Extensions.FacetExtensions.SelectFacet<TTarget>(source)
                     .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously projects a single element of an IQueryable&lt;TSource&gt;
    /// to a facet, throwing if not exactly one element exists, using Entity Framework Core's SingleAsync.
    /// </summary>
    public static Task<TTarget> SingleFacetAsync<TSource, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TTarget>(
        this IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return Facet.Extensions.FacetExtensions.SelectFacet<TSource, TTarget>(source)
                     .SingleAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously projects a single element of an IQueryable&lt;TSource&gt;
    /// to a facet, throwing if not exactly one element exists, using Entity Framework Core's SingleAsync.
    /// The source type is inferred from the query.
    /// </summary>
    public static Task<TTarget> SingleFacetAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TTarget>(
        this IQueryable source,
        CancellationToken cancellationToken = default)
        where TTarget : class
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return Facet.Extensions.FacetExtensions.SelectFacet<TTarget>(source)
                     .SingleAsync(cancellationToken);
    }

    /// <summary>
    /// Updates an entity with changed properties from a Facet DTO, using EF Core change tracking 
    /// to selectively update only properties that have different values.
    /// Only maps properties that exist in both the facet and the entity.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being updated</typeparam>
    /// <typeparam name="TFacet">The facet DTO type containing the new values</typeparam>
    /// <param name="entity">The entity instance to update</param>
    /// <param name="facet">The facet DTO containing the new property values</param>
    /// <param name="context">The EF Core DbContext for change tracking</param>
    /// <returns>The updated entity instance (for fluent chaining)</returns>
    /// <exception cref="ArgumentNullException">Thrown when entity, facet, or context is null</exception>
    /// <example>
    /// <code>
    /// [HttpPut("{id}")]
    /// public async Task&lt;IActionResult&gt; UpdateUser(int id, UpdateUserDto dto)
    /// {
    ///     var user = await context.Users.FindAsync(id);
    ///     if (user == null) return NotFound();
    ///     
    ///     user.UpdateFromFacet(dto, context);
    ///     await context.SaveChangesAsync();
    ///     
    ///     return Ok();
    /// }
    /// </code>
    /// </example>
    public static TEntity UpdateFromFacet<TEntity, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TFacet>(
        this TEntity entity,
        TFacet facet,
        DbContext context)
        where TEntity : class
    {
        if (entity is null) throw new ArgumentNullException(nameof(entity));
        if (facet is null) throw new ArgumentNullException(nameof(facet));
        if (context is null) throw new ArgumentNullException(nameof(context));

        var entry = context.Entry(entity);
        var changedProperties = new List<string>();

        // Get properties that exist in both Facet and Entity
        var facetProperties = typeof(TFacet).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, p => p);

        // Iterate through entity properties that can be modified
        foreach (var entityProperty in entry.Properties)
        {
            var propertyName = entityProperty.Metadata.Name;
            
            if (facetProperties.TryGetValue(propertyName, out var facetProperty))
            {
                var facetValue = facetProperty.GetValue(facet);
                var entityValue = entityProperty.CurrentValue;

                // Only update if values are different
                if (!Equals(facetValue, entityValue))
                {
                    entityProperty.CurrentValue = facetValue;
                    entityProperty.IsModified = true;
                    changedProperties.Add(propertyName);
                }
            }
        }

        return entity;
    }

    /// <summary>
    /// Asynchronously updates an entity with changed properties from a Facet DTO, using EF Core change tracking 
    /// to selectively update only properties that have different values.
    /// This method is useful when you need to perform additional async operations during the update process.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being updated</typeparam>
    /// <typeparam name="TFacet">The facet DTO type containing the new values</typeparam>
    /// <param name="entity">The entity instance to update</param>
    /// <param name="facet">The facet DTO containing the new property values</param>
    /// <param name="context">The EF Core DbContext for change tracking</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete</param>
    /// <returns>A task that represents the asynchronous update operation. The task result contains the updated entity instance</returns>
    /// <exception cref="ArgumentNullException">Thrown when entity, facet, or context is null</exception>
    /// <example>
    /// <code>
    /// [HttpPut("{id}")]
    /// public async Task&lt;IActionResult&gt; UpdateUser(int id, UpdateUserDto dto)
    /// {
    ///     var user = await context.Users.FindAsync(id);
    ///     if (user == null) return NotFound();
    ///     
    ///     await user.UpdateFromFacetAsync(dto, context);
    ///     await context.SaveChangesAsync();
    ///     
    ///     return Ok();
    /// }
    /// </code>
    /// </example>
    public static Task<TEntity> UpdateFromFacetAsync<TEntity, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TFacet>(
        this TEntity entity,
        TFacet facet,
        DbContext context,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        // For now, this is just a synchronous operation wrapped in a completed task
        // In the future, this could be enhanced to support async validation, logging, etc.
        var result = entity.UpdateFromFacet(facet, context);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Updates an entity from a facet DTO and returns information about which properties were changed.
    /// This is useful for auditing, logging, or conditional logic based on what actually changed.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being updated</typeparam>
    /// <typeparam name="TFacet">The facet DTO type containing the new values</typeparam>
    /// <param name="entity">The entity instance to update</param>
    /// <param name="facet">The facet DTO containing the new property values</param>
    /// <param name="context">The EF Core DbContext for change tracking</param>
    /// <returns>A result containing the updated entity and a list of property names that were changed</returns>
    /// <exception cref="ArgumentNullException">Thrown when entity, facet, or context is null</exception>
    /// <example>
    /// <code>
    /// var result = user.UpdateFromFacetWithChanges(dto, context);
    /// if (result.ChangedProperties.Any())
    /// {
    ///     logger.LogInformation("User {UserId} updated. Changed: {Properties}", 
    ///         user.Id, string.Join(", ", result.ChangedProperties));
    /// }
    /// </code>
    /// </example>
    public static FacetUpdateResult<TEntity> UpdateFromFacetWithChanges<TEntity, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TFacet>(
        this TEntity entity,
        TFacet facet,
        DbContext context)
        where TEntity : class
    {
        if (entity is null) throw new ArgumentNullException(nameof(entity));
        if (facet is null) throw new ArgumentNullException(nameof(facet));
        if (context is null) throw new ArgumentNullException(nameof(context));

        var entry = context.Entry(entity);
        var changedProperties = new List<string>();

        // Get properties that exist in both Facet and Entity
        var facetProperties = typeof(TFacet).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, p => p);

        // Iterate through entity properties that can be modified
        foreach (var entityProperty in entry.Properties)
        {
            var propertyName = entityProperty.Metadata.Name;
            
            if (facetProperties.TryGetValue(propertyName, out var facetProperty))
            {
                var facetValue = facetProperty.GetValue(facet);
                var entityValue = entityProperty.CurrentValue;

                // Only update if values are different
                if (!Equals(facetValue, entityValue))
                {
                    entityProperty.CurrentValue = facetValue;
                    entityProperty.IsModified = true;
                    changedProperties.Add(propertyName);
                }
            }
        }

        return new FacetUpdateResult<TEntity>(entity, changedProperties);
    }
}

/// <summary>
/// Represents the result of a facet update operation, containing the updated entity and information about what changed.
/// </summary>
/// <typeparam name="TEntity">The type of entity that was updated</typeparam>
public readonly record struct FacetUpdateResult<TEntity>(
    TEntity Entity,
    IReadOnlyList<string> ChangedProperties)
    where TEntity : class
{
    /// <summary>
    /// Gets a value indicating whether any properties were changed during the update.
    /// </summary>
    public bool HasChanges => ChangedProperties.Count > 0;
}
