using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Facet.Mapping.Expressions;

/// <summary>
/// Maps properties between source types and their Facet projections.
/// Handles both direct property mapping and custom property mappings defined in Facet attributes.
/// </summary>
internal class PropertyPathMapper
{
    private readonly Type _sourceType;
    private readonly Type _targetType;
    private readonly Dictionary<string, MemberInfo> _sourceProperties;
    private readonly Dictionary<string, MemberInfo> _targetProperties;
    private readonly Dictionary<MemberInfo, MemberInfo> _propertyMappings;

    // Cache for reflection results to improve performance
    private static readonly ConcurrentDictionary<Type, Dictionary<string, MemberInfo>> 
        _typePropertiesCache = new();

    public PropertyPathMapper(Type sourceType, Type targetType)
    {
        _sourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        _targetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        
        _sourceProperties = GetTypeProperties(_sourceType);
        _targetProperties = GetTypeProperties(_targetType);
        _propertyMappings = BuildPropertyMappings();
    }

    /// <summary>
    /// Maps a source property to its corresponding target property.
    /// </summary>
    /// <param name="sourceMember">The source property to map</param>
    /// <returns>The corresponding target property, or null if no mapping exists</returns>
    public MemberInfo? MapProperty(MemberInfo sourceMember)
    {
        if (sourceMember == null) return null;

        // Try direct mapping first
        if (_propertyMappings.TryGetValue(sourceMember, out var targetMember))
        {
            return targetMember;
        }

        // Try mapping by name as fallback
        if (_targetProperties.TryGetValue(sourceMember.Name, out var namedTarget))
        {
            // Verify type compatibility
            if (IsCompatiblePropertyType(sourceMember, namedTarget))
            {
                return namedTarget;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all properties and fields for a type, using caching for performance.
    /// </summary>
    private static Dictionary<string, MemberInfo> GetTypeProperties(Type type)
    {
        return _typePropertiesCache.GetOrAdd(type, t =>
        {
            var properties = new Dictionary<string, MemberInfo>();

            // Get all public properties
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                properties[prop.Name] = prop;
            }

            // Get all public fields
            foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                properties[field.Name] = field;
            }

            return properties;
        });
    }

    /// <summary>
    /// Builds the mapping between source and target properties.
    /// This attempts to match properties by name and type, with special handling for Facet projections.
    /// </summary>
    private Dictionary<MemberInfo, MemberInfo> BuildPropertyMappings()
    {
        var mappings = new Dictionary<MemberInfo, MemberInfo>();

        // Attempt to map each source property to a target property
        foreach (var sourceProperty in _sourceProperties.Values)
        {
            // Try exact name match first
            if (_targetProperties.TryGetValue(sourceProperty.Name, out var targetProperty))
            {
                if (IsCompatiblePropertyType(sourceProperty, targetProperty))
                {
                    mappings[sourceProperty] = targetProperty;
                    continue;
                }
            }

            // Try to find alternative mappings (could be extended for custom mapping rules)
            var alternativeMapping = FindAlternativeMapping(sourceProperty);
            if (alternativeMapping != null)
            {
                mappings[sourceProperty] = alternativeMapping;
            }
        }

        return mappings;
    }

    /// <summary>
    /// Checks if two properties have compatible types for mapping.
    /// </summary>
    private static bool IsCompatiblePropertyType(MemberInfo source, MemberInfo target)
    {
        var sourceType = GetMemberType(source);
        var targetType = GetMemberType(target);

        if (sourceType == null || targetType == null) return false;

        // Exact type match
        if (sourceType == targetType) return true;

        // Check if types are assignable
        if (sourceType.IsAssignableFrom(targetType) || targetType.IsAssignableFrom(sourceType))
            return true;

        // Handle nullable/non-nullable variations
        var sourceNonNullable = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
        var targetNonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
        
        if (sourceNonNullable == targetNonNullable) return true;

        // Handle common type conversions that are safe for expressions
        return IsImplicitlyConvertible(sourceType, targetType);
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
    /// Checks if one type can be implicitly converted to another in expression trees.
    /// </summary>
    private static bool IsImplicitlyConvertible(Type from, Type to)
    {
        // Handle primitive type conversions
        if (from.IsPrimitive && to.IsPrimitive)
        {
            // Allow widening numeric conversions
            var fromCode = Type.GetTypeCode(from);
            var toCode = Type.GetTypeCode(to);
            
            return IsWideningConversion(fromCode, toCode);
        }

        return false;
    }

    /// <summary>
    /// Determines if a type conversion is a widening (safe) conversion.
    /// </summary>
    private static bool IsWideningConversion(TypeCode from, TypeCode to)
    {
        return (from, to) switch
        {
            (TypeCode.SByte, TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.Byte, TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.Int16, TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.UInt16, TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.Int32, TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.UInt32, TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.Int64, TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.UInt64, TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.Single, TypeCode.Double) => true,
            _ => false
        };
    }

    /// <summary>
    /// Attempts to find alternative mappings for properties that don't have direct name matches.
    /// This could be extended to support custom mapping attributes or naming conventions.
    /// </summary>
    private MemberInfo? FindAlternativeMapping(MemberInfo sourceProperty)
    {
        // Look for potential naming variations
        var sourceName = sourceProperty.Name;
        
        // Try common variations (could be extended based on naming conventions)
        var variations = new[]
        {
            sourceName + "Value",
            sourceName + "Data",
            "Display" + sourceName,
            sourceName.TrimEnd('s'), // Remove plural 's'
            sourceName + "s" // Add plural 's'
        };

        foreach (var variation in variations)
        {
            if (_targetProperties.TryGetValue(variation, out var targetProperty))
            {
                if (IsCompatiblePropertyType(sourceProperty, targetProperty))
                {
                    return targetProperty;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all mapped property pairs for debugging/inspection purposes.
    /// </summary>
    public IReadOnlyDictionary<MemberInfo, MemberInfo> GetAllMappings()
    {
        return _propertyMappings;
    }

    /// <summary>
    /// Gets all unmapped source properties (properties that couldn't be mapped to the target type).
    /// </summary>
    public IEnumerable<MemberInfo> GetUnmappedSourceProperties()
    {
        return _sourceProperties.Values.Where(sp => !_propertyMappings.ContainsKey(sp));
    }

    /// <summary>
    /// Gets all unmapped target properties (properties on target that don't correspond to source properties).
    /// </summary>
    public IEnumerable<MemberInfo> GetUnmappedTargetProperties()
    {
        var mappedTargets = new HashSet<MemberInfo>(_propertyMappings.Values);
        return _targetProperties.Values.Where(tp => !mappedTargets.Contains(tp));
    }
}