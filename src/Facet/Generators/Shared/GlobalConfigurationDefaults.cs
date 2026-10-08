using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
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
    /// Default value for CopyDocs (default: true).
    /// Can be overridden by setting the Facet_CopyDocs MSBuild property.
    /// </summary>
    public bool CopyDocs { get; }

    /// <summary>
    /// Default value for InheritDocs (default: true).
    /// Can be overridden by setting the Facet_InheritDocs MSBuild property.
    /// </summary>
    public bool InheritDocs { get; }

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
    /// Default value for MaxDepthToSource (default: 0 = no limiting).
    /// Can be overridden by setting the Facet_MaxDepthToSource MSBuild property.
    /// </summary>
    public int MaxDepthToSource { get; }

    /// <summary>
    /// Default value for PreserveReferences (default: false).
    /// Can be overridden by setting the Facet_PreserveReferences MSBuild property.
    /// </summary>
    public bool PreserveReferences { get; }

    /// <summary>
    /// When true (default), generates two files per type: <c>{FullName}.Properties.g.cs</c> (properties only)
    /// and <c>{FullName}.Mappings.g.cs</c> (constructors, projections, and conversion methods).
    /// Set <c>Facet_SplitGeneratedFiles</c> to <c>false</c> to revert to a single combined file.
    /// </summary>
    public bool SplitGeneratedFiles { get; }

    /// <summary>
    /// Extra attribute patterns <c>CopyAttributes</c> must not copy, from
    /// <c>Facet_CopyAttributesExclude</c>. Additive to the built-in list of persistence attributes
    /// in <see cref="AttributeCopyPolicy"/>.
    /// </summary>
    public ImmutableArray<string> CopyAttributesExclude { get; }

    /// <summary>
    /// Attribute patterns <c>CopyAttributes</c> must copy even when something would otherwise
    /// exclude them, from <c>Facet_CopyAttributesInclude</c>. Overrides the built-in list.
    /// </summary>
    public ImmutableArray<string> CopyAttributesInclude { get; }

    /// <summary>
    /// Project-wide default for <c>CopyAttributesMode</c>, from <c>Facet_CopyAttributesMode</c>
    /// (default: <c>Preserve</c>, which is the behaviour Facet had before the mode existed).
    /// </summary>
    public AttributeCopyMode CopyAttributesMode { get; }

    private GlobalConfigurationDefaults(
        bool generateConstructor,
        bool generateParameterlessConstructor,
        bool generateProjection,
        bool generateToSource,
        bool includeFields,
        bool chainToParameterlessConstructor,
        bool nullableProperties,
        bool copyAttributes,
        bool copyDocs,
        bool inheritDocs,
        bool useFullName,
        bool generateCopyConstructor,
        bool generateEquality,
        int maxDepth,
        bool preserveReferences,
        int maxDepthToSource,
        bool splitGeneratedFiles,
        ImmutableArray<string> copyAttributesExclude,
        ImmutableArray<string> copyAttributesInclude,
        AttributeCopyMode copyAttributesMode)
    {
        GenerateConstructor = generateConstructor;
        GenerateParameterlessConstructor = generateParameterlessConstructor;
        GenerateProjection = generateProjection;
        GenerateToSource = generateToSource;
        IncludeFields = includeFields;
        ChainToParameterlessConstructor = chainToParameterlessConstructor;
        NullableProperties = nullableProperties;
        CopyAttributes = copyAttributes;
        CopyDocs = copyDocs;
        InheritDocs = inheritDocs;
        UseFullName = useFullName;
        GenerateCopyConstructor = generateCopyConstructor;
        GenerateEquality = generateEquality;
        MaxDepth = maxDepth;
        PreserveReferences = preserveReferences;
        MaxDepthToSource = maxDepthToSource;
        SplitGeneratedFiles = splitGeneratedFiles;
        CopyAttributesExclude = copyAttributesExclude;
        CopyAttributesInclude = copyAttributesInclude;
        CopyAttributesMode = copyAttributesMode;
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
            copyDocs: GetBoolOption(globalOptions, "build_property.Facet_CopyDocs", defaultValue: true),
            inheritDocs: GetBoolOption(globalOptions, "build_property.Facet_InheritDocs", defaultValue: true),
            useFullName: GetBoolOption(globalOptions, "build_property.Facet_UseFullName", defaultValue: false),
            generateCopyConstructor: GetBoolOption(globalOptions, "build_property.Facet_GenerateCopyConstructor", defaultValue: false),
            generateEquality: GetBoolOption(globalOptions, "build_property.Facet_GenerateEquality", defaultValue: false),
            maxDepth: GetIntOption(globalOptions, "build_property.Facet_MaxDepth", defaultValue: FacetConstants.DefaultMaxDepth),
            preserveReferences: GetBoolOption(globalOptions, "build_property.Facet_PreserveReferences", defaultValue: FacetConstants.DefaultPreserveReferences),
            maxDepthToSource: GetIntOption(globalOptions, "build_property.Facet_MaxDepthToSource", defaultValue: 0),
            splitGeneratedFiles: GetBoolOption(globalOptions, "build_property.Facet_SplitGeneratedFiles", defaultValue: true),
            // The underscore-prefixed values are Facet.targets' semicolon-normalised copies; the
            // raw ones are the fallback for a project configuring this through .editorconfig.
            copyAttributesExclude: AttributeCopyPolicy.ParsePatternList(
                GetStringOption(globalOptions, "build_property._Facet_CopyAttributesExclude")
                ?? GetStringOption(globalOptions, "build_property.Facet_CopyAttributesExclude")),
            copyAttributesInclude: AttributeCopyPolicy.ParsePatternList(
                GetStringOption(globalOptions, "build_property._Facet_CopyAttributesInclude")
                ?? GetStringOption(globalOptions, "build_property.Facet_CopyAttributesInclude")),
            copyAttributesMode: GetEnumOption(globalOptions, "build_property.Facet_CopyAttributesMode", AttributeCopyMode.Preserve));
    }

    private static bool GetBoolOption(AnalyzerConfigOptions? options, string key, bool defaultValue)
    {
        if (options == null)
            return defaultValue;

        if (options.TryGetValue(key, out var value) && bool.TryParse(value, out var result))
            return result;

        return defaultValue;
    }

    private static AttributeCopyMode GetEnumOption(
        AnalyzerConfigOptions? options,
        string key,
        AttributeCopyMode defaultValue)
    {
        var value = GetStringOption(options, key);

        return value is not null && System.Enum.TryParse<AttributeCopyMode>(value, ignoreCase: true, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static string? GetStringOption(AnalyzerConfigOptions? options, string key)
    {
        if (options == null)
            return null;

        return options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
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
