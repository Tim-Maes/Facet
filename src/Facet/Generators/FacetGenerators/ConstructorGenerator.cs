using Facet.Generators.Shared;
using System.Linq;
using System.Text;

namespace Facet.Generators;

/// <summary>
/// Generates constructors for facet types, including depth-aware constructors for circular reference prevention
/// and parameterless constructors.
/// </summary>
internal static class ConstructorGenerator
{
    /// <summary>
    /// Generates the main constructor and related factory methods for the facet type.
    /// </summary>
    public static void GenerateConstructor(
        StringBuilder sb,
        FacetTargetModel model,
        bool isPositional,
        bool hasInitOnlyProperties,
        bool hasCustomMapping,
        bool hasRequiredProperties)
    {
        // If the target has an existing primary constructor, skip constructor generation
        // and provide only a factory method
        if (model.HasExistingPrimaryConstructor && model.IsRecord)
        {
            GenerateFactoryMethodForExistingPrimaryConstructor(sb, model, hasCustomMapping);
            return;
        }

        // Check if we have nested facets and depth tracking is needed
        bool hasNestedFacets = model.Members.Any(m => m.IsNestedFacet);
        // IMPORTANT: Generate depth-aware constructors whenever MaxDepth > 0 OR PreserveReferences is enabled
        // This ensures ALL facets (even those without nested facets) can be instantiated with depth tracking
        // when they are used as nested facets by other facets that have depth tracking enabled
        bool needsDepthTracking = model.MaxDepth > 0 || model.PreserveReferences;

        GenerateMainConstructor(sb, model, isPositional, hasRequiredProperties, needsDepthTracking, hasInitOnlyProperties, hasCustomMapping);

        // Generate internal depth-aware constructor if needed
        if (needsDepthTracking)
        {
            GenerateDepthAwareConstructor(sb, model, isPositional, hasInitOnlyProperties, hasCustomMapping, hasRequiredProperties);
        }

        // Add static factory method for types with init-only properties
        if (!isPositional && hasInitOnlyProperties && !model.HasExistingPrimaryConstructor)
        {
            GenerateFromSourceFactoryMethod(sb, model, hasCustomMapping);
        }
    }

    /// <summary>
    /// Generates a parameterless constructor for the facet type.
    /// </summary>
    public static void GenerateParameterlessConstructor(StringBuilder sb, FacetTargetModel model, bool isPositional)
    {
        sb.AppendLine();

        // Don't generate parameterless constructor for records with existing primary constructors
        // as it would conflict with the C# language rules
        if (model.HasExistingPrimaryConstructor && model.IsRecord)
        {
            sb.AppendLine($"    // Note: Parameterless constructor not generated for records with existing primary constructors");
            sb.AppendLine($"    // to avoid conflicts with C# language rules. Use object initializer syntax instead:");
            sb.AppendLine($"    // var instance = new {model.Name}(primaryConstructorParams) {{ /* initialize faceted properties */ }};");
            return;
        }

        // Generate parameterless constructor XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the <see cref=\"{model.Name}\"/> class with default values.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// This constructor is useful for unit testing, object initialization, and scenarios");
        sb.AppendLine("    /// where you need to create an empty instance and populate properties later.");
        sb.AppendLine("    /// </remarks>");

        // For positional records, we need to call the primary constructor with default values
        if (isPositional && !model.HasExistingPrimaryConstructor)
        {
            var defaultValues = model.Members.Select(m => GeneratorUtilities.GetDefaultValue(m.TypeName)).ToArray();
            var defaultArgs = string.Join(", ", defaultValues);

            sb.AppendLine($"    public {model.Name}() : this({defaultArgs})");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
        }
        // For non-positional types (classes, structs), generate a simple parameterless constructor
        else if (!isPositional)
        {
            sb.AppendLine($"    public {model.Name}()");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
        }
    }

    #region Private Helper Methods

    private static void GenerateMainConstructor(
        StringBuilder sb,
        FacetTargetModel model,
        bool isPositional,
        bool hasRequiredProperties,
        bool needsDepthTracking,
        bool hasInitOnlyProperties,
        bool hasCustomMapping)
    {
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the <see cref=\"{model.Name}\"/> class from the specified <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"source\">The source <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/> object to copy data from.</param>");
        if (hasCustomMapping)
        {
            sb.AppendLine("    /// <remarks>");
            sb.AppendLine("    /// This constructor automatically maps all compatible properties and applies custom mapping logic.");
            sb.AppendLine("    /// </remarks>");
        }

        var ctorSig = $"public {model.Name}({model.SourceTypeName} source)";

        if (needsDepthTracking)
        {
            // Chain to internal depth-aware constructor with reference tracking
            if (model.PreserveReferences)
            {
                ctorSig += " : this(source, 0, new System.Collections.Generic.HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance))";
            }
            else
            {
                ctorSig += " : this(source, 0, null)";
            }
        }
        else if (isPositional && !model.HasExistingPrimaryConstructor)
        {
            // Traditional positional record - chain to primary constructor
            var args = string.Join(", ",
                model.Members.Select(m => ExpressionBuilder.GetSourceValueExpression(m, "source")));
            ctorSig += $" : this({args})";
        }

        if (hasRequiredProperties)
        {
            sb.AppendLine("    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]");
        }
        sb.AppendLine($"    {ctorSig}");
        sb.AppendLine("    {");

        // Only generate body if not chaining to depth-aware constructor
        if (!needsDepthTracking)
        {
            GenerateMainConstructorBody(sb, model, isPositional, hasInitOnlyProperties, hasCustomMapping);
        }

        sb.AppendLine("    }");
    }

    private static void GenerateMainConstructorBody(
        StringBuilder sb,
        FacetTargetModel model,
        bool isPositional,
        bool hasInitOnlyProperties,
        bool hasCustomMapping)
    {
        if (!isPositional && !model.HasExistingPrimaryConstructor)
        {
            if (hasCustomMapping && hasInitOnlyProperties)
            {
                // For types with init-only properties and custom mapping,
                // we can't assign after construction
                sb.AppendLine($"        // This constructor should not be used for types with init-only properties and custom mapping");
                sb.AppendLine($"        // Use FromSource factory method instead");
                sb.AppendLine($"        throw new InvalidOperationException(\"Use {model.Name}.FromSource(source) for types with init-only properties\");");
            }
            else if (hasCustomMapping)
            {
                // Regular mutable properties - copy first, then apply custom mapping
                foreach (var m in model.Members)
                    sb.AppendLine($"        this.{m.Name} = source.{m.Name};");
                sb.AppendLine($"        {model.ConfigurationTypeName}.Map(source, this);");
            }
            else
            {
                // No custom mapping - copy properties directly
                foreach (var m in model.Members.Where(x => !x.IsInitOnly))
                {
                    var sourceValue = ExpressionBuilder.GetSourceValueExpression(m, "source");
                    sb.AppendLine($"        this.{m.Name} = {sourceValue};");
                }
            }
        }
        else if (hasCustomMapping && !model.HasExistingPrimaryConstructor)
        {
            // For positional records/record structs with custom mapping
            sb.AppendLine($"        {model.ConfigurationTypeName}.Map(source, this);");
        }
    }

    /// <summary>
    /// Generates an internal constructor with depth tracking for circular reference prevention.
    /// </summary>
    private static void GenerateDepthAwareConstructor(
        StringBuilder sb,
        FacetTargetModel model,
        bool isPositional,
        bool hasInitOnlyProperties,
        bool hasCustomMapping,
        bool hasRequiredProperties)
    {
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Internal constructor with depth tracking to prevent stack overflow from circular references.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"source\">The source object to copy data from.</param>");
        sb.AppendLine($"    /// <param name=\"__depth\">Current nesting depth for circular reference detection.</param>");
        sb.AppendLine($"    /// <param name=\"__processed\">Set of already processed objects to detect circular references.</param>");

        var ctorSig = $"internal {model.Name}({model.SourceTypeName} source, int __depth, System.Collections.Generic.HashSet<object>? __processed)";

        if (isPositional && !model.HasExistingPrimaryConstructor)
        {
            // Traditional positional record - chain to primary constructor
            var args = string.Join(", ",
                model.Members.Select(m => ExpressionBuilder.GetSourceValueExpression(m, "source", model.MaxDepth, true, model.PreserveReferences)));
            ctorSig += $" : this({args})";
        }

        if (hasRequiredProperties)
        {
            sb.AppendLine("    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]");
        }
        sb.AppendLine($"    {ctorSig}");
        sb.AppendLine("    {");

        if (!isPositional && !model.HasExistingPrimaryConstructor)
        {
            GenerateDepthAwareConstructorBody(sb, model, hasInitOnlyProperties, hasCustomMapping);
        }
        else if (hasCustomMapping && !model.HasExistingPrimaryConstructor)
        {
            // For positional records/record structs with custom mapping
            sb.AppendLine($"        {model.ConfigurationTypeName}.Map(source, this);");
        }

        sb.AppendLine("    }");
    }

    private static void GenerateDepthAwareConstructorBody(
        StringBuilder sb,
        FacetTargetModel model,
        bool hasInitOnlyProperties,
        bool hasCustomMapping)
    {
        if (hasCustomMapping && hasInitOnlyProperties)
        {
            sb.AppendLine($"        // This constructor should not be used for types with init-only properties and custom mapping");
            sb.AppendLine($"        // Use FromSource factory method instead");
            sb.AppendLine($"        throw new InvalidOperationException(\"Use {model.Name}.FromSource(source) for types with init-only properties\");");
        }
        else if (hasCustomMapping)
        {
            // Regular mutable properties - copy first, then apply custom mapping
            foreach (var m in model.Members)
                sb.AppendLine($"        this.{m.Name} = source.{m.Name};");
            sb.AppendLine($"        {model.ConfigurationTypeName}.Map(source, this);");
        }
        else
        {
            // No custom mapping - copy properties directly with depth tracking
            foreach (var m in model.Members.Where(x => !x.IsInitOnly))
            {
                var sourceValue = ExpressionBuilder.GetSourceValueExpression(m, "source", model.MaxDepth, true, model.PreserveReferences);
                sb.AppendLine($"        this.{m.Name} = {sourceValue};");
            }
        }
    }

    private static void GenerateFromSourceFactoryMethod(StringBuilder sb, FacetTargetModel model, bool hasCustomMapping)
    {
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Creates a new instance of <see cref=\"{model.Name}\"/> from the specified <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/> with init-only properties.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"source\">The source <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/> object to copy data from.</param>");
        sb.AppendLine($"    /// <returns>A new <see cref=\"{model.Name}\"/> instance with all properties initialized from the source.</returns>");
        sb.AppendLine($"    public static {model.Name} FromSource({model.SourceTypeName} source)");
        sb.AppendLine("    {");

        if (hasCustomMapping)
        {
            // For custom mapping with init-only properties, the mapper should create the instance
            sb.AppendLine($"        // Custom mapper creates and returns the instance with init-only properties set");
            sb.AppendLine($"        return {model.ConfigurationTypeName}.Map(source, null);");
        }
        else
        {
            sb.AppendLine($"        return new {model.Name}");
            sb.AppendLine("        {");
            foreach (var m in model.Members)
            {
                var comma = m == model.Members.Last() ? "" : ",";
                var sourceValue = ExpressionBuilder.GetSourceValueExpression(m, "source");
                sb.AppendLine($"            {m.Name} = {sourceValue}{comma}");
            }
            sb.AppendLine("        };");
        }

        sb.AppendLine("    }");
    }

    private static void GenerateFactoryMethodForExistingPrimaryConstructor(StringBuilder sb, FacetTargetModel model, bool hasCustomMapping)
    {
        // For records with existing primary constructor, provide only a factory method
        // Users must handle the primary constructor parameters manually

        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new {model.Name} from the source with faceted properties initialized.");
        sb.AppendLine($"    /// This record has an existing primary constructor, so you must provide values");
        sb.AppendLine($"    /// for the primary constructor parameters when creating instances.");
        sb.AppendLine($"    /// Example: new {model.Name}(primaryConstructorParam) {{ PropA = source.PropA, PropB = source.PropB }}");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static {model.Name} FromSource({model.SourceTypeName} source, params object[] primaryConstructorArgs)");
        sb.AppendLine("    {");

        if (hasCustomMapping)
        {
            sb.AppendLine($"        // Custom mapping is configured for this facet");
            sb.AppendLine($"        // The custom mapper should handle both the primary constructor and faceted properties");
            sb.AppendLine($"        throw new NotImplementedException(");
            sb.AppendLine($"            \"Custom mapping with existing primary constructors requires manual implementation. \" +");
            sb.AppendLine($"            \"Implement the mapping in your custom mapper configuration.\");");
        }
        else
        {
            sb.AppendLine($"        // For records with existing primary constructors, you must manually create the instance");
            sb.AppendLine($"        // and initialize the faceted properties using object initializer syntax.");
            sb.AppendLine($"        throw new NotSupportedException(");
            sb.AppendLine($"            \"Records with existing primary constructors must be created manually. \" +");
            sb.AppendLine($"            \"Example: new {model.Name}(primaryConstructorParam) {{ {string.Join(", ", model.Members.Take(2).Select(m => $"{m.Name} = source.{m.Name}"))} }}\");");
        }

        sb.AppendLine("    }");
    }

    #endregion
}
