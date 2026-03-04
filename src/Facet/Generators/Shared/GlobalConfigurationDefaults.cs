using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Facet.Generators.Shared;

/// <summary>
/// Reads global configuration defaults from MSBuild properties or .editorconfig.
/// These defaults override the hardcoded attribute defaults when the attribute property is not explicitly set.
/// </summary>
internal sealed class GlobalConfigurationDefaults
{
    /// <summary>
    /// Default value for GenerateConstructor (default: true).
    /// Can be overridden by setting the Facet_GenerateConstructor MSBuild property.
    /// </summary>
    public bool GenerateConstructor { get; }

    /// <summary>
    /// Default value for GenerateParameterlessConstructor (default: true).
    /// Can be overridden by setting the Facet_GenerateParameterlessConstructor MSBuild property.
    /// </summary>
    public bool GenerateParameterlessConstructor { get; }

    /// <summary>
    /// Default value for GenerateProjection (default: true).
    /// Can be overridden by setting the Facet_GenerateProjection MSBuild property.
    /// </summary>
    public bool GenerateProjection { get; }

    /// <summary>
    /// Default value for GenerateToSource (default: false).
    /// Can be overridden by setting the Facet_GenerateToSource MSBuild property.
    /// </summary>
    public bool GenerateToSource { get; }

    /// <summary>
    /// Default value for IncludeFields (default: false).
    /// Can be overridden by setting the Facet_IncludeFields MSBuild property.
    /// </summary>
    public bool IncludeFields { get; }

    /// <summary>
    /// Default value for ChainToParameterlessConstructor (default: false).
    /// Can be overridden by setting the Facet_ChainToParameterlessConstructor MSBuild property.
    /// </summary>
    public bool ChainToParameterlessConstructor { get; }

    /// <summary>
    /// Default value for NullableProperties (default: false).
    /// Can be overridden by setting the Facet_NullableProperties MSBuild property.
    /// </summary>
    public bool NullableProperties { get; }

    /// <summary>
    /// Default value for CopyAttributes (default: false).
    /// Can be overridden by setting the Facet_CopyAttributes MSBuild property.
    /// </summary>
    public bool CopyAttributes { get; }

    /// <summary>
    /// Default value for UseFullName (default: false).
    /// Can be overridden by setting the Facet_UseFullName MSBuild property.
    /// </summary>
    public bool UseFullName { get; }

    /// <summary>
    /// Default value for GenerateCopyConstructor (default: false).
    /// Can be overridden by setting the Facet_GenerateCopyConstructor MSBuild property.
    /// </summary>
    public bool GenerateCopyConstructor { get; }

    /// <summary>
    /// Default value for GenerateEquality (default: false).
    /// Can be overridden by setting the Facet_GenerateEquality MSBuild property.
    /// </summary>
    public bool GenerateEquality { get; }

    /// <summary>
    /// Default value for MaxDepth (default: 10).
    /// Can be overridden by setting the Facet_MaxDepth MSBuild property.
    /// </summary>
    public int MaxDepth { get; }

    /// <summary>
    /// Default value for PreserveReferences (default: true).
    /// Can be overridden by setting the Facet_PreserveReferences MSBuild property.
    /// </summary>
    public bool PreserveReferences { get; }

    private GlobalConfigurationDefaults(
        bool generateConstructor,
        bool generateParameterlessConstructor,
        bool generateProjection,
        bool generateToSource,
        bool includeFields,
        bool chainToParameterlessConstructor,
        bool nullableProperties,
        bool copyAttributes,
        bool useFullName,
        bool generateCopyConstructor,
        bool generateEquality,
        int maxDepth,
        bool preserveReferences)
    {
        GenerateConstructor = generateConstructor;
        GenerateParameterlessConstructor = generateParameterlessConstructor;
        GenerateProjection = generateProjection;
        GenerateToSource = generateToSource;
        IncludeFields = includeFields;
        ChainToParameterlessConstructor = chainToParameterlessConstructor;
        NullableProperties = nullableProperties;
        CopyAttributes = copyAttributes;
        UseFullName = useFullName;
        GenerateCopyConstructor = generateCopyConstructor;
        GenerateEquality = generateEquality;
        MaxDepth = maxDepth;
        PreserveReferences = preserveReferences;
    }

    /// <summary>
    /// Reads global configuration defaults from analyzer configuration options.
    /// Falls back to hardcoded defaults if not specified.
    /// </summary>
    public static GlobalConfigurationDefaults FromOptions(AnalyzerConfigOptions? globalOptions)
    {
        return new GlobalConfigurationDefaults(
            generateConstructor: GetBoolOption(globalOptions, "build_property.Facet_GenerateConstructor", defaultValue: true),
            generateParameterlessConstructor: GetBoolOption(globalOptions, "build_property.Facet_GenerateParameterlessConstructor", defaultValue: true),
            generateProjection: GetBoolOption(globalOptions, "build_property.Facet_GenerateProjection", defaultValue: true),
            generateToSource: GetBoolOption(globalOptions, "build_property.Facet_GenerateToSource", defaultValue: false),
            includeFields: GetBoolOption(globalOptions, "build_property.Facet_IncludeFields", defaultValue: false),
            chainToParameterlessConstructor: GetBoolOption(globalOptions, "build_property.Facet_ChainToParameterlessConstructor", defaultValue: false),
            nullableProperties: GetBoolOption(globalOptions, "build_property.Facet_NullableProperties", defaultValue: false),
            copyAttributes: GetBoolOption(globalOptions, "build_property.Facet_CopyAttributes", defaultValue: false),
            useFullName: GetBoolOption(globalOptions, "build_property.Facet_UseFullName", defaultValue: false),
            generateCopyConstructor: GetBoolOption(globalOptions, "build_property.Facet_GenerateCopyConstructor", defaultValue: false),
            generateEquality: GetBoolOption(globalOptions, "build_property.Facet_GenerateEquality", defaultValue: false),
            maxDepth: GetIntOption(globalOptions, "build_property.Facet_MaxDepth", defaultValue: FacetConstants.DefaultMaxDepth),
            preserveReferences: GetBoolOption(globalOptions, "build_property.Facet_PreserveReferences", defaultValue: FacetConstants.DefaultPreserveReferences));
    }

    private static bool GetBoolOption(AnalyzerConfigOptions? options, string key, bool defaultValue)
    {
        if (options == null)
            return defaultValue;

        if (options.TryGetValue(key, out var value) && bool.TryParse(value, out var result))
            return result;

        return defaultValue;
    }

    private static int GetIntOption(AnalyzerConfigOptions? options, string key, int defaultValue)
    {
        if (options == null)
            return defaultValue;

        if (options.TryGetValue(key, out var value) && int.TryParse(value, out var result))
            return result;

        return defaultValue;
    }
}
