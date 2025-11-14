using System.Text;

namespace Facet.Generators;

/// <summary>
/// Generates constructors for wrapper types.
/// </summary>
internal static class WrapperConstructorGenerator
{
    /// <summary>
    /// Generates a constructor that stores a reference to the source object.
    /// </summary>
    public static void GenerateConstructor(StringBuilder sb, WrapperTargetModel model, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Initializes a new instance of the {model.Name} wrapper.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <param name=\"source\">The source object to wrap.</param>");
        sb.AppendLine($"{indent}/// <exception cref=\"global::System.ArgumentNullException\">Thrown when source is null.</exception>");

        // Extract the simple type name from fully qualified name for the parameter
        var sourceParamName = "source";

        sb.AppendLine($"{indent}public {model.Name}({model.SourceTypeName} {sourceParamName})");
        sb.AppendLine($"{indent}{{");

        // Add null check
        sb.AppendLine($"{indent}    {model.SourceFieldName} = {sourceParamName} ?? throw new global::System.ArgumentNullException(nameof({sourceParamName}));");

        sb.AppendLine($"{indent}}}");
    }
}
