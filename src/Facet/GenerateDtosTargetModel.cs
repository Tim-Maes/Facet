using System;
using System.Collections.Immutable;
using System.Linq;

namespace Facet;

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
    public ImmutableArray<string> ExcludeProperties { get; }
    public ImmutableArray<FacetMember> Members { get; }

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
        ImmutableArray<string> excludeProperties,
        ImmutableArray<FacetMember> members)
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
        ExcludeProperties = excludeProperties;
        Members = members;
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
            && ExcludeProperties.SequenceEqual(other.ExcludeProperties)
            && Members.SequenceEqual(other.Members);
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

            foreach (var prop in ExcludeProperties)
                hash = hash * 31 + prop.GetHashCode();

            foreach (var member in Members)
                hash = hash * 31 + member.GetHashCode();

            return hash;
        }
    }
}