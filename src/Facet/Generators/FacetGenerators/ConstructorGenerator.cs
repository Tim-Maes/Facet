using Facet.Generators.Shared;
using System;
using System.Collections.Generic;
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
        if (model.HasExistingPrimaryConstructor && model.IsRecord)
        {
            GenerateFactoryMethodForExistingPrimaryConstructor(sb, model, hasCustomMapping);
            return;
        }

        bool hasNestedFacets = model.Members.Any(m => m.IsNestedFacet);
        
        bool needsDepthTracking = model.MaxDepth > 0 || model.PreserveReferences;

        GenerateMainConstructor(sb, model, isPositional, hasRequiredProperties, needsDepthTracking, hasInitOnlyProperties, hasCustomMapping);

        if (needsDepthTracking)
        {
            GenerateDepthAwareConstructor(sb, model, isPositional, hasInitOnlyProperties, hasCustomMapping, hasRequiredProperties);
        }

        if (!isPositional && !model.HasExistingPrimaryConstructor)
        {
            GenerateFromSourceFactoryMethod(sb, model, hasCustomMapping, needsDepthTracking);
        }
    }

    /// <summary>
    /// Generates a parameterless constructor for the facet type.
    /// </summary>
    public static void GenerateParameterlessConstructor(StringBuilder sb, FacetTargetModel model, bool isPositional)
    {
        sb.AppendLine();

        if (model.HasExistingPrimaryConstructor && model.IsRecord)
        {
            sb.AppendLine($"    // Note: Parameterless constructor not generated for records with existing primary constructors");
            sb.AppendLine($"    // to avoid conflicts with C# language rules. Use object initializer syntax instead:");
            sb.AppendLine($"    // var instance = new {model.Name}(primaryConstructorParams) {{ /* initialize faceted properties */ }};");
            return;
        }

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the <see cref=\"{model.Name}\"/> class with default values.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// This constructor is useful for unit testing, object initialization, and scenarios");
        sb.AppendLine("    /// where you need to create an empty instance and populate properties later.");
        sb.AppendLine("    /// </remarks>");

        if (isPositional && !model.HasExistingPrimaryConstructor)
        {
            var defaultValues = model.Members.Select(m => GeneratorUtilities.GetDefaultValue(m.TypeName)).ToArray();
            var defaultArgs = string.Join(", ", defaultValues);

            sb.AppendLine($"    public {model.Name}() : this({defaultArgs})");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
        }
        
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
            var sourceNames = GetSourcePropertySet(model);
            var args = string.Join(", ",
                model.Members.Select(m => ExpressionBuilder.GetSourceValueExpression(m, "source", 0, false, false, sourceNames)));
            ctorSig += $" : this({args})";
        }
        else if (model.ChainToParameterlessConstructor && !isPositional)
        {
            ctorSig += " : this()";
        }

        if (hasRequiredProperties)
        {
            sb.AppendLine("    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]");
        }
        sb.AppendLine($"    {ctorSig}");
        sb.AppendLine("    {");

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
        var hasBeforeMap = !string.IsNullOrWhiteSpace(model.BeforeMapConfigurationTypeName);
        var hasAfterMap = !string.IsNullOrWhiteSpace(model.AfterMapConfigurationTypeName);

        if (!isPositional && !model.HasExistingPrimaryConstructor)
        {
            if (hasBeforeMap)
            {
                sb.AppendLine($"        {model.BeforeMapConfigurationTypeName}.BeforeMap(source, this);");
            }

            if (hasCustomMapping && hasInitOnlyProperties)
            {
                sb.AppendLine($"        // This constructor should not be used for types with init-only properties and custom mapping");
                sb.AppendLine($"        // Use FromSource factory method instead");
                sb.AppendLine($"        throw new InvalidOperationException(\"Use {model.Name}.FromSource(source) for types with init-only properties\");");
            }
            else if (hasCustomMapping)
            {
                var sourceNames = GetSourcePropertySet(model);
                foreach (var m in model.Members)
                {
                    var sourceValue = ExpressionBuilder.GetSourceValueExpression(m, "source", 0, false, false, sourceNames);
                    sb.AppendLine($"        this.{m.Name} = {sourceValue};");
                }
                sb.AppendLine($"        {GetMappingCall(model, "source", "this")};");
            }
            else
            {
                // Constructors can assign init-only members.
                var sourceNames = GetSourcePropertySet(model);
                foreach (var m in model.Members)
                {
                    var sourceValue = ExpressionBuilder.GetSourceValueExpression(m, "source", 0, false, false, sourceNames);
                    sb.AppendLine($"        this.{m.Name} = {sourceValue};");
                }
            }

            if (hasAfterMap)
            {
                sb.AppendLine($"        {model.AfterMapConfigurationTypeName}.AfterMap(source, this);");
            }
        }
        else if (hasCustomMapping && !model.HasExistingPrimaryConstructor)
        {
            if (hasBeforeMap)
            {
                sb.AppendLine($"        {model.BeforeMapConfigurationTypeName}.BeforeMap(source, this);");
            }
            sb.AppendLine($"        {GetMappingCall(model, "source", "this")};");
            if (hasAfterMap)
            {
                sb.AppendLine($"        {model.AfterMapConfigurationTypeName}.AfterMap(source, this);");
            }
        }
        else if (!model.HasExistingPrimaryConstructor)
        {
            if (hasBeforeMap)
            {
                sb.AppendLine($"        {model.BeforeMapConfigurationTypeName}.BeforeMap(source, this);");
            }
            if (hasAfterMap)
            {
                sb.AppendLine($"        {model.AfterMapConfigurationTypeName}.AfterMap(source, this);");
            }
        }
    }

    /// <summary>
    /// Generates a public constructor with depth tracking for circular reference prevention.
    /// This is public to support nested facets across assemblies.
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
        sb.AppendLine($"    /// Constructor with depth tracking to prevent stack overflow from circular references.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"source\">The source object to copy data from.</param>");
        sb.AppendLine($"    /// <param name=\"__depth\">Current nesting depth for circular reference detection.</param>");
        sb.AppendLine($"    /// <param name=\"__processed\">Set of already processed objects to detect circular references.</param>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// This constructor is public to support nested facets across different assemblies.");
        sb.AppendLine("    /// For typical usage, prefer the single-parameter constructor or FromSource factory method.");
        sb.AppendLine("    /// </remarks>");

        var ctorSig = $"public {model.Name}({model.SourceTypeName} source, int __depth, System.Collections.Generic.HashSet<object>? __processed)";

        if (isPositional && !model.HasExistingPrimaryConstructor)
        {
            var sourceNames = GetSourcePropertySet(model);
            var args = string.Join(", ",
                model.Members.Select(m => ExpressionBuilder.GetSourceValueExpression(m, "source", model.MaxDepth, true, model.PreserveReferences, sourceNames)));
            ctorSig += $" : this({args})";
        }
        else if (model.ChainToParameterlessConstructor && !isPositional)
        {
            ctorSig += " : this()";
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
            sb.AppendLine($"        {GetMappingCall(model, "source", "this")};");
        }

        sb.AppendLine("    }");
    }

    private static void GenerateDepthAwareConstructorBody(
        StringBuilder sb,
        FacetTargetModel model,
        bool hasInitOnlyProperties,
        bool hasCustomMapping)
    {
        var hasBeforeMap = !string.IsNullOrWhiteSpace(model.BeforeMapConfigurationTypeName);
        var hasAfterMap = !string.IsNullOrWhiteSpace(model.AfterMapConfigurationTypeName);

        if (hasBeforeMap)
        {
            sb.AppendLine($"        {model.BeforeMapConfigurationTypeName}.BeforeMap(source, this);");
        }

        if (hasCustomMapping && hasInitOnlyProperties)
        {
            sb.AppendLine($"        // This constructor should not be used for types with init-only properties and custom mapping");
            sb.AppendLine($"        // Use FromSource factory method instead");
            sb.AppendLine($"        throw new InvalidOperationException(\"Use {model.Name}.FromSource(source) for types with init-only properties\");");
        }
        else if (hasCustomMapping)
        {
            var sourceNames = GetSourcePropertySet(model);
            foreach (var m in model.Members)
            {
                var sourceValue = ExpressionBuilder.GetSourceValueExpression(m, "source", model.MaxDepth, true, model.PreserveReferences, sourceNames);
                sb.AppendLine($"        this.{m.Name} = {sourceValue};");
            }
            sb.AppendLine($"        {GetMappingCall(model, "source", "this")};");
        }
        else
        {
            // Constructors can assign init-only members.
            var sourceNames = GetSourcePropertySet(model);
            foreach (var m in model.Members)
            {
                var sourceValue = ExpressionBuilder.GetSourceValueExpression(m, "source", model.MaxDepth, true, model.PreserveReferences, sourceNames);
                sb.AppendLine($"        this.{m.Name} = {sourceValue};");
            }
        }

        if (hasAfterMap)
        {
            sb.AppendLine($"        {model.AfterMapConfigurationTypeName}.AfterMap(source, this);");
        }
    }

    private static void GenerateFromSourceFactoryMethod(StringBuilder sb, FacetTargetModel model, bool hasCustomMapping, bool needsDepthTracking)
    {
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Creates a new instance of <see cref=\"{model.Name}\"/> from the specified <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"source\">The source <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/> object to copy data from.</param>");
        sb.AppendLine($"    /// <returns>A new <see cref=\"{model.Name}\"/> instance with all properties initialized from the source.</returns>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// This static factory method provides optimal performance for runtime mapping by allowing");
        sb.AppendLine("    /// direct delegate creation instead of expression compilation.");
        sb.AppendLine("    /// </remarks>");
        sb.AppendLine($"    public static {(model.BaseHidesFromSource ? "new " : "")}{model.Name} FromSource({model.SourceTypeName} source)");
        sb.AppendLine("    {");

        if (needsDepthTracking)
        {
            if (model.PreserveReferences)
            {
                sb.AppendLine($"        return new {model.Name}(source, 0, new System.Collections.Generic.HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance));");
            }
            else
            {
                sb.AppendLine($"        return new {model.Name}(source, 0, null);");
            }
        }
        else if (hasCustomMapping)
        {
            sb.AppendLine($"        // Custom mapper creates and returns the instance");
            sb.AppendLine($"        var instance = new {model.Name}();");
            sb.AppendLine($"        {GetMappingCall(model, "source", "instance")};");
            sb.AppendLine($"        return instance;");
        }
        else
        {
            sb.AppendLine($"        return new {model.Name}");
            sb.AppendLine("        {");
            var sourceNames = GetSourcePropertySet(model);
            foreach (var m in model.Members)
            {
                var comma = m == model.Members.Last() ? "" : ",";
                var sourceValue = ExpressionBuilder.GetSourceValueExpression(m, "source", 0, false, false, sourceNames);
                sb.AppendLine($"            {m.Name} = {sourceValue}{comma}");
            }
            sb.AppendLine("        };");
        }

        sb.AppendLine("    }");
    }

    private static void GenerateFactoryMethodForExistingPrimaryConstructor(StringBuilder sb, FacetTargetModel model, bool hasCustomMapping)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new {model.Name} from the source with faceted properties initialized.");
        sb.AppendLine($"    /// This record has an existing primary constructor, so you must provide values");
        sb.AppendLine($"    /// for the primary constructor parameters when creating instances.");
        sb.AppendLine($"    /// Example: new {model.Name}(primaryConstructorParam) {{ PropA = source.PropA, PropB = source.PropB }}");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static {(model.BaseHidesFromSource ? "new " : "")}{model.Name} FromSource({model.SourceTypeName} source, params object[] primaryConstructorArgs)");
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

    private static HashSet<string> GetSourcePropertySet(FacetTargetModel model)
        => model.SourcePropertyNames.Length > 0
            ? new HashSet<string>(model.SourcePropertyNames, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Returns the appropriate mapping call for the constructor body.
    /// When the config only implements IFacetProjectionMapConfiguration (no Map method),
    /// delegates to the compiled projection action. Otherwise calls Map() directly.
    /// </summary>
    private static string GetMappingCall(FacetTargetModel model, string sourceExpr, string targetExpr)
    {
        if (!model.HasMapConfiguration && model.HasProjectionMapConfiguration)
        {
            return $"__GetProjectionMapAction()({sourceExpr}, {targetExpr})";
        }
        return $"global::{model.ConfigurationTypeName}.Map({sourceExpr}, {targetExpr})";
    }
}
