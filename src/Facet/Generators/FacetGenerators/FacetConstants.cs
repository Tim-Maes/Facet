using System;
using System.Reflection;

namespace Facet.Generators.FacetGenerators;

/// <summary>
/// Contains constant values used throughout the Facet source generator.
/// Centralizes magic strings and default values to improve maintainability.
/// </summary>
internal static class FacetConstants
{
    /// <summary>
    /// The version of the Facet generator, cached for performance.
    /// </summary>
    public static readonly string GeneratorVersion = GetGeneratorVersion();

    private static string GetGeneratorVersion()
    {
        try
        {
            return typeof(FacetConstants).Assembly.GetName().Version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// The fully qualified name of the FacetAttribute.
    /// </summary>
    public const string FacetAttributeFullName = "Facet.FacetAttribute";

    /// <summary>
    /// The default maximum depth for nested facet traversal to prevent stack overflow.
    /// </summary>
    public const int DefaultMaxDepth = 10;

    /// <summary>
    /// The default setting for preserving object references during mapping to detect circular references.
    /// </summary>
    public const bool DefaultPreserveReferences = true;

    /// <summary>
    /// The prefix used to specify global namespace qualification.
    /// </summary>
    public const string GlobalNamespacePrefix = "global::";

    /// <summary>
    /// The number of spaces per indentation level in generated code.
    /// </summary>
    public const int SpacesPerIndentLevel = 4;

    /// <summary>
    /// Standard collection wrapper type names.
    /// </summary>
    public static class CollectionWrappers
    {
        public const string List = "List";
        public const string IList = "IList";
        public const string ICollection = "ICollection";
        public const string IEnumerable = "IEnumerable";
        public const string Array = "array";
    }

    /// <summary>
    /// Common attribute names used in facet generation.
    /// </summary>
    public static class AttributeNames
    {
        public const string NestedFacets = "NestedFacets";
        public const string Include = "Include";
        public const string Configuration = "Configuration";
        public const string IncludeFields = "IncludeFields";
        public const string GenerateConstructor = "GenerateConstructor";
        public const string GenerateParameterlessConstructor = "GenerateParameterlessConstructor";
        public const string GenerateProjection = "GenerateProjection";
        public const string GenerateBackTo = "GenerateBackTo";
        public const string PreserveInitOnlyProperties = "PreserveInitOnlyProperties";
        public const string PreserveRequiredProperties = "PreserveRequiredProperties";
        public const string NullableProperties = "NullableProperties";
        public const string CopyAttributes = "CopyAttributes";
        public const string MaxDepth = "MaxDepth";
        public const string PreserveReferences = "PreserveReferences";
        public const string UseFullName = "UseFullName";
    }
}
