using System.Reflection;

namespace Facet.Dashboard;

/// <summary>
/// Discovers and catalogs all Facet types in the application.
/// </summary>
public static class FacetDiscovery
{
    private static readonly object _lock = new();
    private static IReadOnlyList<FacetMappingInfo>? _cachedMappings;

    /// <summary>
    /// Discovers all facet mappings from the specified assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan. If null, scans all loaded assemblies.</param>
    /// <returns>A collection of facet mappings grouped by source type.</returns>
    public static IReadOnlyList<FacetMappingInfo> DiscoverFacets(IEnumerable<Assembly>? assemblies = null)
    {
        lock (_lock)
        {
            if (_cachedMappings != null)
                return _cachedMappings;

            var assembliesToScan = assemblies ?? GetRelevantAssemblies();
            var mappings = DiscoverFacetsCore(assembliesToScan);
            _cachedMappings = mappings;
            return mappings;
        }
    }

    /// <summary>
    /// Clears the cached discovery results, forcing a re-scan on next call.
    /// </summary>
    public static void ClearCache()
    {
        lock (_lock)
        {
            _cachedMappings = null;
        }
    }

    /// <summary>
    /// Discovers facets from the entry assembly and its referenced assemblies.
    /// </summary>
    public static IReadOnlyList<FacetMappingInfo> DiscoverFromEntryAssembly()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly == null)
            return Array.Empty<FacetMappingInfo>();

        var assemblies = new HashSet<Assembly> { entryAssembly };
        
        // Add referenced assemblies
        foreach (var reference in entryAssembly.GetReferencedAssemblies())
        {
            try
            {
                var assembly = Assembly.Load(reference);
                assemblies.Add(assembly);
            }
            catch
            {
                // Skip assemblies that can't be loaded
            }
        }

        return DiscoverFacetsCore(assemblies);
    }

    private static IEnumerable<Assembly> GetRelevantAssemblies()
    {
        // Get all currently loaded assemblies, filtering out system assemblies
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !IsSystemAssembly(a));
    }

    private static bool IsSystemAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name;
        if (name == null) return true;

        // Skip system and framework assemblies
        return name.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("WindowsBase", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<FacetMappingInfo> DiscoverFacetsCore(IEnumerable<Assembly> assemblies)
    {
        var facetsBySource = new Dictionary<Type, List<FacetTypeInfo>>();

        foreach (var assembly in assemblies)
        {
            try
            {
                foreach (var type in GetLoadableTypes(assembly))
                {
                    var facetAttributes = type.GetCustomAttributes<FacetAttribute>(inherit: false);
                    
                    foreach (var attr in facetAttributes)
                    {
                        var sourceType = attr.SourceType;
                        if (sourceType == null) continue;

                        var facetInfo = CreateFacetTypeInfo(type, attr);

                        if (!facetsBySource.TryGetValue(sourceType, out var list))
                        {
                            list = new List<FacetTypeInfo>();
                            facetsBySource[sourceType] = list;
                        }

                        list.Add(facetInfo);
                    }
                }
            }
            catch
            {
                // Skip assemblies that can't be scanned
            }
        }

        // Build the final result
        var result = new List<FacetMappingInfo>();

        foreach (var kvp in facetsBySource.OrderBy(x => x.Key.FullName))
        {
            var sourceMembers = GetMembersFromType(kvp.Key);
            var mapping = new FacetMappingInfo(kvp.Key, kvp.Value, sourceMembers);
            result.Add(mapping);
        }

        return result.AsReadOnly();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    private static FacetTypeInfo CreateFacetTypeInfo(Type facetType, FacetAttribute attr)
    {
        // Determine type kind
        var typeKind = DetermineTypeKind(facetType);

        // Check for constructor from source type
        var hasConstructor = facetType.GetConstructor(new[] { attr.SourceType }) != null;

        // Check for Projection property
        var hasProjection = facetType.GetProperty("Projection", BindingFlags.Public | BindingFlags.Static) != null;

        // Check for ToSource method
        var hasToSource = facetType.GetMethod("ToSource", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) != null;

        // Get members
        var members = GetMembersFromType(facetType);

        // Get nested facets
        var nestedFacets = attr.NestedFacets ?? Array.Empty<Type>();

        // Configuration type
        var configTypeName = attr.Configuration?.FullName;

        return new FacetTypeInfo(
            facetType: facetType,
            hasConstructor: hasConstructor,
            hasProjection: hasProjection,
            hasToSource: hasToSource,
            excludedProperties: attr.Exclude ?? Array.Empty<string>(),
            includedProperties: attr.Include,
            members: members,
            nestedFacets: nestedFacets,
            typeKind: typeKind,
            nullableProperties: attr.NullableProperties,
            copyAttributes: attr.CopyAttributes,
            configurationTypeName: configTypeName
        );
    }

    private static string DetermineTypeKind(Type type)
    {
        if (type.IsValueType)
        {
            // Check for record struct by looking for <Clone>$ method
            var cloneMethod = type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance);
            if (cloneMethod != null)
                return "record struct";
            return "struct";
        }
        else
        {
            // Check for record by looking for <Clone>$ method
            var cloneMethod = type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance);
            if (cloneMethod != null)
                return "record";
            return "class";
        }
    }

    private static IReadOnlyList<FacetMemberInfo> GetMembersFromType(Type type)
    {
        var members = new List<FacetMemberInfo>();

        // Get properties
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            var memberInfo = CreateMemberInfo(prop);
            members.Add(memberInfo);
        }

        // Get public fields
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in fields)
        {
            var memberInfo = CreateMemberInfo(field);
            members.Add(memberInfo);
        }

        return members.AsReadOnly();
    }

    private static FacetMemberInfo CreateMemberInfo(PropertyInfo prop)
    {
        var propertyType = prop.PropertyType;
        var isNullable = IsNullableType(propertyType) || 
                         HasNullableAttribute(prop);

        var isReadOnly = !prop.CanWrite;
        var isInitOnly = IsInitOnlyProperty(prop);
        var isRequired = HasRequiredAttribute(prop);

        // Check for MapFrom attribute
        var mapFromAttr = prop.GetCustomAttributes()
            .FirstOrDefault(a => a.GetType().Name == "MapFromAttribute");
        
        string? mappedFromProperty = null;
        if (mapFromAttr != null)
        {
            var sourceProperty = mapFromAttr.GetType().GetProperty("Source");
            mappedFromProperty = sourceProperty?.GetValue(mapFromAttr) as string;
        }

        var attributes = prop.GetCustomAttributes(inherit: true)
            .Select(a => a.GetType().Name.Replace("Attribute", ""))
            .ToList();

        var isNestedFacet = propertyType.GetCustomAttribute<FacetAttribute>() != null;
        var isCollection = IsCollectionType(propertyType);

        return new FacetMemberInfo(
            name: prop.Name,
            typeName: GetFriendlyTypeName(propertyType),
            isProperty: true,
            isNullable: isNullable,
            isRequired: isRequired,
            isInitOnly: isInitOnly,
            isReadOnly: isReadOnly,
            xmlDocumentation: null, // Would need XML documentation file
            attributes: attributes,
            isNestedFacet: isNestedFacet,
            isCollection: isCollection,
            mappedFromProperty: mappedFromProperty
        );
    }

    private static FacetMemberInfo CreateMemberInfo(FieldInfo field)
    {
        var fieldType = field.FieldType;
        var isNullable = IsNullableType(fieldType) ||
                         HasNullableAttribute(field);

        var isRequired = HasRequiredAttribute(field);

        var attributes = field.GetCustomAttributes(inherit: true)
            .Select(a => a.GetType().Name.Replace("Attribute", ""))
            .ToList();

        var isNestedFacet = fieldType.GetCustomAttribute<FacetAttribute>() != null;
        var isCollection = IsCollectionType(fieldType);

        return new FacetMemberInfo(
            name: field.Name,
            typeName: GetFriendlyTypeName(fieldType),
            isProperty: false,
            isNullable: isNullable,
            isRequired: isRequired,
            isInitOnly: field.IsInitOnly,
            isReadOnly: field.IsInitOnly,
            xmlDocumentation: null,
            attributes: attributes,
            isNestedFacet: isNestedFacet,
            isCollection: isCollection,
            mappedFromProperty: null
        );
    }

    private static bool IsNullableType(Type type)
    {
        return Nullable.GetUnderlyingType(type) != null ||
               (!type.IsValueType && type.IsClass);
    }

    private static bool HasNullableAttribute(MemberInfo member)
    {
        // Check for nullable reference type annotations
        var nullableAttr = member.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.Name == "NullableAttribute");
        
        if (nullableAttr?.ConstructorArguments.Count > 0)
        {
            var arg = nullableAttr.ConstructorArguments[0];
            if (arg.Value is byte b)
                return b == 2; // 2 = nullable
            if (arg.Value is byte[] bytes && bytes.Length > 0)
                return bytes[0] == 2;
        }

        return false;
    }

    private static bool IsInitOnlyProperty(PropertyInfo prop)
    {
        var setMethod = prop.SetMethod;
        if (setMethod == null) return false;

        // Check for init accessor by looking for modreq
        var returnParam = setMethod.ReturnParameter;
        var modreqs = returnParam?.GetRequiredCustomModifiers();
        
        return modreqs?.Any(m => m.Name == "IsExternalInit") == true;
    }

    private static bool HasRequiredAttribute(MemberInfo member)
    {
        return member.CustomAttributes
            .Any(a => a.AttributeType.Name == "RequiredMemberAttribute");
    }

    private static bool IsCollectionType(Type type)
    {
        if (type == typeof(string)) return false;
        if (type.IsArray) return true;

        return type.GetInterfaces()
            .Any(i => i.IsGenericType && 
                     (i.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
                      i.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                      i.GetGenericTypeDefinition() == typeof(IList<>)));
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();
            var argsStr = string.Join(", ", args.Select(GetFriendlyTypeName));
            
            var typeName = genericType.Name;
            var backtickIndex = typeName.IndexOf('`');
            if (backtickIndex > 0)
                typeName = typeName.Substring(0, backtickIndex);

            // Handle nullable value types
            if (genericType == typeof(Nullable<>))
                return $"{GetFriendlyTypeName(args[0])}?";

            return $"{typeName}<{argsStr}>";
        }

        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            return $"{GetFriendlyTypeName(elementType)}[]";
        }

        // Common type aliases
        return type.FullName switch
        {
            "System.String" => "string",
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.Int16" => "short",
            "System.Byte" => "byte",
            "System.Boolean" => "bool",
            "System.Decimal" => "decimal",
            "System.Double" => "double",
            "System.Single" => "float",
            "System.Object" => "object",
            "System.Void" => "void",
            _ => type.Name
        };
    }
}
