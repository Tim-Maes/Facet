using System;
using System.Collections.Immutable;
using System.Linq;

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
    public string? ConvertEnumsTo { get; }
    public ImmutableArray<string> ExcludeProperties { get; }
    public ImmutableArray<FacetMember> Members { get; }
    public bool UseFullName { get; }
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
        string? convertEnumsTo,
        ImmutableArray<string> excludeProperties,
        ImmutableArray<FacetMember> members,
        bool useFullName,
        DtoTypes siblingInterfaceTypes = DtoTypes.None,
        OutputTypeIssue issue = OutputTypeIssue.None)
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
        ConvertEnumsTo = convertEnumsTo;
        ExcludeProperties = excludeProperties;
        Members = members;
        UseFullName = useFullName;
        SiblingInterfaceTypes = siblingInterfaceTypes;
        Issue = issue;
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
            ConvertEnumsTo,
            ExcludeProperties,
            Members,
            UseFullName,
            SiblingInterfaceTypes);
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
            ConvertEnumsTo,
            ExcludeProperties,
            Members,
            UseFullName,
            SiblingInterfaceTypes,
            issue);
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
            && ConvertEnumsTo == other.ConvertEnumsTo
            && ExcludeProperties.SequenceEqual(other.ExcludeProperties)
            && Members.SequenceEqual(other.Members)
            && UseFullName == other.UseFullName
            && SiblingInterfaceTypes == other.SiblingInterfaceTypes
            && Issue == other.Issue;
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
            hash = hash * 31 + (ConvertEnumsTo?.GetHashCode() ?? 0);
            hash = hash * 31 + UseFullName.GetHashCode();
            hash = hash * 31 + SiblingInterfaceTypes.GetHashCode();
            hash = hash * 31 + Issue.GetHashCode();

            foreach (var prop in ExcludeProperties)
                hash = hash * 31 + prop.GetHashCode();

            foreach (var member in Members)
                hash = hash * 31 + member.GetHashCode();

            return hash;
        }
    }
}
