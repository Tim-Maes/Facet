using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Facet.Extensions.EFCore;
/// <summary>
/// Provides EF Core async extension methods for mapping source entities or sequences
/// to Facet-generated types.
/// </summary>
public static class FacetEfCoreExtensions
{
    /// <summary>
    /// Attribute to mark DTO properties that should be ignored by Facet UpdateFromFacet helpers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class FacetUpdateIgnoreAttribute : Attribute { }

    private sealed record PropertyMap(Type EntityType, Type FacetType, IReadOnlyList<PropertyInfo> SharedFacetProps);
    private static readonly Dictionary<(Type Entity, Type Facet), PropertyMap> _propertyMapCache = new();
    private static readonly object _cacheLock = new();

    private static PropertyMap GetOrAddPropertyMap<TEntity, TFacet>(DbContext context)
        where TEntity : class
    {
        var key = (typeof(TEntity), typeof(TFacet));
        lock (_cacheLock)
        {
            if (_propertyMapCache.TryGetValue(key, out var existing)) return existing;

            // Build dictionary of facet props once
            var facetProps = typeof(TFacet)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetCustomAttribute<FacetUpdateIgnoreAttribute>() == null)
                .ToDictionary(p => p.Name, p => p, StringComparer.Ordinal);

            // Determine intersection using EF metadata (so we only consider mapped scalar properties)
            var entityType = context.Model.FindEntityType(typeof(TEntity));
            var scalarProps = entityType?.GetProperties() ?? Array.Empty<IProperty>();
            var sharedFacetProps = new List<PropertyInfo>(capacity: facetProps.Count);
            foreach (var sp in scalarProps)
            {
                if (facetProps.TryGetValue(sp.Name, out var pi))
                {
                    sharedFacetProps.Add(pi);
                }
            }

            var map = new PropertyMap(typeof(TEntity), typeof(TFacet), sharedFacetProps);
            _propertyMapCache[key] = map;
            return map;
        }
    }

    private static IReadOnlyList<string> ApplyFacetValues<TEntity, TFacet>(
        EntityEntry<TEntity> entry,
        TFacet facet,
        PropertyMap map,
        bool skipKeys,
        bool skipConcurrency,
        bool skipNavigations,
        ISet<string>? excluded,
        Func<PropertyInfo, bool>? predicate)
        where TEntity : class
    {
        var changed = new List<string>();
        var entityType = entry.Context.Model.FindEntityType(typeof(TEntity));
        var keyNames = skipKeys && entityType != null ? entityType.FindPrimaryKey()?.Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal) : null;
        var concurrencyNames = skipConcurrency && entityType != null ? entityType.GetProperties().Where(p => p.IsConcurrencyToken).Select(p => p.Name).ToHashSet(StringComparer.Ordinal) : null;
        var navigationNames = skipNavigations && entityType != null ? entityType.GetNavigations().Select(n => n.Name).ToHashSet(StringComparer.Ordinal) : null;

        foreach (var facetProp in map.SharedFacetProps)
        {
            var name = facetProp.Name;
            if (excluded?.Contains(name) == true) continue;
            if (predicate != null && !predicate(facetProp)) continue;
            if (keyNames != null && keyNames.Contains(name)) continue;
            if (concurrencyNames != null && concurrencyNames.Contains(name)) continue;
            if (navigationNames != null && navigationNames.Contains(name)) continue;

            var entityProperty = entry.Property(name);
            if (entityProperty == null) continue; // Should not happen but safe guard

            // Skip if not mutable
            var propMeta = entityProperty.Metadata;
            // Skip key (already filtered optionally), shadow, or store-generated on add/update depending on configuration
            if (propMeta.IsShadowProperty()) continue;
            // If store-generated and not yet saved, we generally skip to avoid overwriting generated values.
            if (propMeta.ValueGenerated != ValueGenerated.Never && entityProperty.EntityEntry.State == EntityState.Added) continue;

            var facetValue = facetProp.GetValue(facet);
            var currentValue = entityProperty.CurrentValue;
            if (!Equals(facetValue, currentValue))
            {
                entityProperty.CurrentValue = facetValue;
                entityProperty.IsModified = true;
                changed.Add(name);
            }
        }
        return changed;
    }
    /// <summary>
    /// Asynchronously projects an IQueryable&lt;TSource&gt; to a List&lt;TTarget&gt;
    /// using the generated Projection expression and Entity Framework Core's ToListAsync.
    /// </summary>
    public static Task<List<TTarget>> ToFacetsAsync<TSource, TTarget>(
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
    public static Task<List<TTarget>> ToFacetsAsync<TTarget>(
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
    public static Task<TTarget?> FirstFacetAsync<TSource, TTarget>(
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
    public static Task<TTarget?> FirstFacetAsync<TTarget>(
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
    public static Task<TTarget> SingleFacetAsync<TSource, TTarget>(
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
    public static Task<TTarget> SingleFacetAsync<TTarget>(
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
    /// <summary>
    /// Core update method copying differing scalar property values from a facet/DTO onto an entity.
    /// </summary>
    /// <param name="skipKeys">Skip primary key properties (default true).</param>
    /// <param name="skipConcurrency">Skip concurrency token properties (default true).</param>
    /// <param name="skipNavigations">Skip navigation properties (default true).</param>
    /// <param name="excludedProperties">Specific property names to ignore.</param>
    /// <param name="propertyPredicate">Additional predicate filter; return false to skip.</param>
    public static TEntity UpdateFromFacet<TEntity, TFacet>(
        this TEntity entity,
        TFacet facet,
        DbContext context,
        bool skipKeys = true,
        bool skipConcurrency = true,
        bool skipNavigations = true,
        IEnumerable<string>? excludedProperties = null,
        Func<PropertyInfo, bool>? propertyPredicate = null)
        where TEntity : class
    {
        if (entity is null) throw new ArgumentNullException(nameof(entity));
        if (facet is null) throw new ArgumentNullException(nameof(facet));
        if (context is null) throw new ArgumentNullException(nameof(context));

        var entry = context.Entry(entity);
        var map = GetOrAddPropertyMap<TEntity, TFacet>(context);
        var excluded = excludedProperties != null ? new HashSet<string>(excludedProperties, StringComparer.Ordinal) : null;
        _ = ApplyFacetValues(entry, facet, map, skipKeys, skipConcurrency, skipNavigations, excluded, propertyPredicate);
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
    public static Task<TEntity> UpdateFromFacetAsync<TEntity, TFacet>(
        this TEntity entity,
        TFacet facet,
        DbContext context,
        bool skipKeys = true,
        bool skipConcurrency = true,
        bool skipNavigations = true,
        IEnumerable<string>? excludedProperties = null,
        Func<PropertyInfo, bool>? propertyPredicate = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var result = entity.UpdateFromFacet(facet, context, skipKeys, skipConcurrency, skipNavigations, excludedProperties, propertyPredicate);
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
    public static FacetUpdateResult<TEntity> UpdateFromFacetWithChanges<TEntity, TFacet>(
        this TEntity entity,
        TFacet facet,
        DbContext context,
        bool skipKeys = true,
        bool skipConcurrency = true,
        bool skipNavigations = true,
        IEnumerable<string>? excludedProperties = null,
        Func<PropertyInfo, bool>? propertyPredicate = null)
        where TEntity : class
    {
        if (entity is null) throw new ArgumentNullException(nameof(entity));
        if (facet is null) throw new ArgumentNullException(nameof(facet));
        if (context is null) throw new ArgumentNullException(nameof(context));

        var entry = context.Entry(entity);
        var map = GetOrAddPropertyMap<TEntity, TFacet>(context);
        var excluded = excludedProperties != null ? new HashSet<string>(excludedProperties, StringComparer.Ordinal) : null;
        var changed = ApplyFacetValues(entry, facet, map, skipKeys, skipConcurrency, skipNavigations, excluded, propertyPredicate);
        return new FacetUpdateResult<TEntity>(entity, changed);
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
