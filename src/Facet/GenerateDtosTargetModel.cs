using System;
using System.Collections.Immutable;
using System.Linq;
using Facet.Generators;

namespace Facet;

/// <summary>
/// Invalid <see cref="OutputType"/> shapes detected during attribute expansion. Models
/// carrying an issue generate nothing; the generator reports a diagnostic instead
/// (FAC101 for <see cref="ConflictingConcreteKinds"/>, FAC102 for <see cref="PartialWithoutKind"/>).
/// </summary>
internal enum OutputTypeIssue
{
    None = 0,
    ConflictingConcreteKinds = 1,
    PartialWithoutKind = 2,
}

internal sealed class GenerateDtosTargetModel : IEquatable<GenerateDtosTargetModel>
{
    public string SourceTypeName { get; }
    public string? SourceNamespace { get; }
    public string? TargetNamespace { get; }
    public DtoTypes Types { get; }
    public OutputType OutputType { get; }
    public string? Prefix { get; }
    public string? Suffix { get; }
    public bool IncludeFields { get; }
    public bool GenerateConstructors { get; }
    public bool GenerateProjections { get; }
    public bool GenerateReadOnlyProperties { get; }
    public string? PropertySuffix { get; }
    public string? ConvertEnumsTo { get; }
    public ImmutableArray<string> ExcludeProperties { get; }
    public ImmutableArray<FacetMember> Members { get; }
    public bool UseFullName { get; }
    /// <summary>
    /// The attribute's navigation-property exclusion request: true or false when set
    /// explicitly, null when the attribute leaves it unset — the generation stage then
    /// defaults it to whether an EF model manifest is wired into the compilation. Members are
    /// NOT yet filtered for navigations here: manifests are AdditionalFiles, unavailable in
    /// the syntax transform, so the final member set is resolved at generation time. A shaped
    /// source type with no manifest entry is an error (FAC105) — there is no heuristic
    /// fallback.
    /// </summary>
    public bool? ExcludeNavigationProperties { get; }
    /// <summary>
    /// The attribute's IncludeProperties escape hatch, kept on the model because manifest-based
    /// filtering happens after the transform and must still honor forced inclusions.
    /// </summary>
    public ImmutableArray<string> IncludeProperties { get; }
    /// <summary>
    /// Names of property members that EF could plausibly map — settable, or get-only
    /// collections — recorded unless <see cref="ExcludeNavigationProperties"/> is explicitly
    /// false (an unset value may still resolve to shaping, so the names must be carried).
    /// The generation stage checks these against a manifest entry's known-set: a name the
    /// model has no opinion on means the manifest predates the property (FAC106).
    /// </summary>
    public ImmutableArray<string> SettableProperties { get; }
    /// <summary>
    /// Location of the [GenerateDtos] attribute application, so diagnostics point at the
    /// attribute instead of Location.None (and become #pragma-suppressible). Null when the
    /// syntax reference is unavailable.
    /// </summary>
    public SourceLocationInfo? AttributeLocation { get; }
    /// <summary>
    /// The set of DTO type bits for which a sibling <see cref="OutputType.Interface"/> output on the
    /// same source type generates a matching interface (same Prefix/Suffix/Namespace, overlapping
    /// <see cref="DtoTypes"/>) — whether from a separate attribute or from a flags-combined
    /// OutputType. Meaningful for every concrete output kind; lets the generator declare
    /// <c>: I{Name}</c> on the concrete type so the two outputs compose into a
    /// contract + implementation pair.
    /// </summary>
    public DtoTypes SiblingInterfaceTypes { get; }
    /// <summary>
    /// Set when the attribute's <see cref="Facet.OutputType"/> flags form an invalid shape
    /// (see <see cref="OutputTypeIssue"/>). Such models generate nothing; the generator
    /// reports the matching diagnostic. <see cref="OutputType"/> holds the full offending
    /// mask for the message.
    /// </summary>
    public OutputTypeIssue Issue { get; }
    /// <summary>
    /// Whether the consuming compilation references System.Text.Json. Gates emission of
    /// the Optional&lt;T&gt; wire-format attributes and converter on Patch DTOs so projects
    /// without STJ still compile.
    /// </summary>
    public bool SupportsSystemTextJson { get; }
    /// <summary>
    /// Whether the consuming compilation references Newtonsoft.Json. Gates emission of the
    /// Json.NET counterpart attributes and converter on Patch DTOs (e.g. for ASP.NET Core
    /// apps using AddNewtonsoftJson, whose MVC body binding bypasses System.Text.Json).
    /// </summary>
    public bool SupportsNewtonsoftJson { get; }

    public GenerateDtosTargetModel(
        string sourceTypeName,
        string? sourceNamespace,
        string? targetNamespace,
        DtoTypes types,
        OutputType outputType,
        string? prefix,
        string? suffix,
        bool includeFields,
        bool generateConstructors,
        bool generateProjections,
        bool generateReadOnlyProperties,
        string? propertySuffix,
        string? convertEnumsTo,
        ImmutableArray<string> excludeProperties,
        ImmutableArray<FacetMember> members,
        bool useFullName,
        bool? excludeNavigationProperties = null,
        ImmutableArray<string> includeProperties = default,
        ImmutableArray<string> settableProperties = default,
        SourceLocationInfo? attributeLocation = null,
        DtoTypes siblingInterfaceTypes = DtoTypes.None,
        OutputTypeIssue issue = OutputTypeIssue.None,
        bool supportsSystemTextJson = false,
        bool supportsNewtonsoftJson = false)
    {
        SourceTypeName = sourceTypeName;
        SourceNamespace = sourceNamespace;
        TargetNamespace = targetNamespace;
        Types = types;
        OutputType = outputType;
        Prefix = prefix;
        Suffix = suffix;
        IncludeFields = includeFields;
        GenerateConstructors = generateConstructors;
        GenerateProjections = generateProjections;
        GenerateReadOnlyProperties = generateReadOnlyProperties;
        PropertySuffix = propertySuffix;
        ConvertEnumsTo = convertEnumsTo;
        ExcludeProperties = excludeProperties;
        Members = members;
        UseFullName = useFullName;
        ExcludeNavigationProperties = excludeNavigationProperties;
        IncludeProperties = includeProperties.IsDefault ? ImmutableArray<string>.Empty : includeProperties;
        SettableProperties = settableProperties.IsDefault ? ImmutableArray<string>.Empty : settableProperties;
        AttributeLocation = attributeLocation;
        SiblingInterfaceTypes = siblingInterfaceTypes;
        Issue = issue;
        SupportsSystemTextJson = supportsSystemTextJson;
        SupportsNewtonsoftJson = supportsNewtonsoftJson;
    }

    /// <summary>
    /// Returns a copy of this model targeting a different <see cref="Facet.OutputType"/>,
    /// used to expand a flags-combined attribute value into one model per set bit.
    /// </summary>
    public GenerateDtosTargetModel WithOutputType(OutputType outputType)
    {
        return new GenerateDtosTargetModel(
            SourceTypeName,
            SourceNamespace,
            TargetNamespace,
            Types,
            outputType,
            Prefix,
            Suffix,
            IncludeFields,
            GenerateConstructors,
            GenerateProjections,
            GenerateReadOnlyProperties,
            PropertySuffix,
            ConvertEnumsTo,
            ExcludeProperties,
            Members,
            UseFullName,
            ExcludeNavigationProperties,
            IncludeProperties,
            SettableProperties,
            AttributeLocation,
            SiblingInterfaceTypes,
            Issue,
            SupportsSystemTextJson,
            SupportsNewtonsoftJson);
    }

    /// <summary>
    /// Returns a copy of this model with the final, navigation-filtered member list, used by
    /// the generation stage once manifest exclusion is resolved. The navigation bookkeeping
    /// fields are cleared: they exist only to carry the pending decision.
    /// </summary>
    public GenerateDtosTargetModel WithResolvedMembers(ImmutableArray<FacetMember> members)
    {
        return new GenerateDtosTargetModel(
            SourceTypeName,
            SourceNamespace,
            TargetNamespace,
            Types,
            OutputType,
            Prefix,
            Suffix,
            IncludeFields,
            GenerateConstructors,
            GenerateProjections,
            GenerateReadOnlyProperties,
            PropertySuffix,
            ConvertEnumsTo,
            ExcludeProperties,
            members,
            UseFullName,
            excludeNavigationProperties: false,
            includeProperties: IncludeProperties,
            settableProperties: ImmutableArray<string>.Empty,
            attributeLocation: AttributeLocation,
            siblingInterfaceTypes: SiblingInterfaceTypes,
            issue: Issue,
            supportsSystemTextJson: SupportsSystemTextJson,
            supportsNewtonsoftJson: SupportsNewtonsoftJson);
    }

    /// <summary>
    /// Returns a copy of this model flagged with an invalid <see cref="Facet.OutputType"/>
    /// shape. The copy generates nothing; the generator reports the matching diagnostic.
    /// </summary>
    public GenerateDtosTargetModel WithIssue(OutputTypeIssue issue)
    {
        return new GenerateDtosTargetModel(
            SourceTypeName,
            SourceNamespace,
            TargetNamespace,
            Types,
            OutputType,
            Prefix,
            Suffix,
            IncludeFields,
            GenerateConstructors,
            GenerateProjections,
            GenerateReadOnlyProperties,
            PropertySuffix,
            ConvertEnumsTo,
            ExcludeProperties,
            Members,
            UseFullName,
            ExcludeNavigationProperties,
            IncludeProperties,
            SettableProperties,
            AttributeLocation,
            SiblingInterfaceTypes,
            issue,
            SupportsSystemTextJson,
            SupportsNewtonsoftJson);
    }

    public bool Equals(GenerateDtosTargetModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return SourceTypeName == other.SourceTypeName
            && SourceNamespace == other.SourceNamespace
            && TargetNamespace == other.TargetNamespace
            && Types == other.Types
            && OutputType == other.OutputType
            && Prefix == other.Prefix
            && Suffix == other.Suffix
            && IncludeFields == other.IncludeFields
            && GenerateConstructors == other.GenerateConstructors
            && GenerateProjections == other.GenerateProjections
            && GenerateReadOnlyProperties == other.GenerateReadOnlyProperties
            && PropertySuffix == other.PropertySuffix
            && ConvertEnumsTo == other.ConvertEnumsTo
            && ExcludeProperties.SequenceEqual(other.ExcludeProperties)
            && Members.SequenceEqual(other.Members)
            && UseFullName == other.UseFullName
            && ExcludeNavigationProperties == other.ExcludeNavigationProperties
            && IncludeProperties.SequenceEqual(other.IncludeProperties)
            && SettableProperties.SequenceEqual(other.SettableProperties)
            && Nullable.Equals(AttributeLocation, other.AttributeLocation)
            && SiblingInterfaceTypes == other.SiblingInterfaceTypes
            && Issue == other.Issue
            && SupportsSystemTextJson == other.SupportsSystemTextJson
            && SupportsNewtonsoftJson == other.SupportsNewtonsoftJson;
    }

    public override bool Equals(object? obj) => obj is GenerateDtosTargetModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (SourceTypeName?.GetHashCode() ?? 0);
            hash = hash * 31 + (SourceNamespace?.GetHashCode() ?? 0);
            hash = hash * 31 + (TargetNamespace?.GetHashCode() ?? 0);
            hash = hash * 31 + Types.GetHashCode();
            hash = hash * 31 + OutputType.GetHashCode();
            hash = hash * 31 + (Prefix?.GetHashCode() ?? 0);
            hash = hash * 31 + (Suffix?.GetHashCode() ?? 0);
            hash = hash * 31 + IncludeFields.GetHashCode();
            hash = hash * 31 + GenerateConstructors.GetHashCode();
            hash = hash * 31 + GenerateProjections.GetHashCode();
            hash = hash * 31 + GenerateReadOnlyProperties.GetHashCode();
            hash = hash * 31 + (PropertySuffix?.GetHashCode() ?? 0);
            hash = hash * 31 + (ConvertEnumsTo?.GetHashCode() ?? 0);
            hash = hash * 31 + UseFullName.GetHashCode();
            hash = hash * 31 + ExcludeNavigationProperties.GetHashCode();
            hash = hash * 31 + SiblingInterfaceTypes.GetHashCode();
            hash = hash * 31 + Issue.GetHashCode();
            hash = hash * 31 + SupportsSystemTextJson.GetHashCode();
            hash = hash * 31 + SupportsNewtonsoftJson.GetHashCode();

            foreach (var prop in ExcludeProperties)
                hash = hash * 31 + prop.GetHashCode();

            foreach (var prop in IncludeProperties)
                hash = hash * 31 + prop.GetHashCode();

            foreach (var prop in SettableProperties)
                hash = hash * 31 + prop.GetHashCode();

            hash = hash * 31 + (AttributeLocation?.GetHashCode() ?? 0);

            foreach (var member in Members)
                hash = hash * 31 + member.GetHashCode();

            return hash;
        }
    }
}
