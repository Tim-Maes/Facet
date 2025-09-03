using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace Facet.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class FacetGenerator : IIncrementalGenerator
{
    private const string FacetAttributeName = "Facet.FacetAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var facets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                FacetAttributeName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, token) => GetTargetModel(ctx, token))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(facets, static (spc, model) =>
        {
            spc.CancellationToken.ThrowIfCancellationRequested();
            var code = Generate(model!);
            spc.AddSource($"{model!.Name}.g.cs", SourceText.From(code, Encoding.UTF8));
        });
    }

    private static FacetTargetModel? GetTargetModel(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not INamedTypeSymbol targetSymbol) return null;
        if (context.Attributes.Length == 0) return null;

        var attribute = context.Attributes[0];
        token.ThrowIfCancellationRequested();

        var sourceType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        if (sourceType == null) return null;

        var excluded = new HashSet<string>(
            attribute.ConstructorArguments.ElementAtOrDefault(1).Values
                .Select(v => v.Value?.ToString())
                .Where(n => n != null)!);

        var includeFields = GetNamedArg(attribute.NamedArguments, "IncludeFields", false);
        var generateConstructor = GetNamedArg(attribute.NamedArguments, "GenerateConstructor", true);
        var generateParameterlessConstructor = GetNamedArg(attribute.NamedArguments, "GenerateParameterlessConstructor", true);
        var generateProjection = GetNamedArg(attribute.NamedArguments, "GenerateProjection", true);

        var configurationTypeName = attribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == "Configuration")
            .Value.Value?
            .ToString();

        // If Auto is specified, infer the kind from the target type first
        var kind = attribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == "Kind")
            .Value.Value is int kindValue
            ? (FacetKind)kindValue
            : FacetKind.Auto;

        if (kind == FacetKind.Auto)
        {
            kind = InferFacetKind(targetSymbol);
        }

        // For record types, default to preserving init-only and required modifiers
        // unless explicitly overridden by the user
        var preserveInitOnlyDefault = kind is FacetKind.Record or FacetKind.RecordStruct;
        var preserveRequiredDefault = kind is FacetKind.Record or FacetKind.RecordStruct;

        var preserveInitOnly = GetNamedArg(attribute.NamedArguments, "PreserveInitOnlyProperties", preserveInitOnlyDefault);
        var preserveRequired = GetNamedArg(attribute.NamedArguments, "PreserveRequiredProperties", preserveRequiredDefault);

        var members = new List<FacetMember>();
        var addedMembers = new HashSet<string>();

        var allMembersWithModifiers = GetAllMembersWithModifiers(sourceType);

        // Extract type-level XML documentation from the source type
        var typeXmlDocumentation = ExtractXmlDocumentation(sourceType);

        foreach (var (member, isInitOnly, isRequired) in allMembersWithModifiers)
        {
            token.ThrowIfCancellationRequested();
            if (excluded.Contains(member.Name)) continue;
            if (addedMembers.Contains(member.Name)) continue;

            if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } p)
            {
                var shouldPreserveInitOnly = preserveInitOnly && isInitOnly;
                var shouldPreserveRequired = preserveRequired && isRequired;
                var memberXmlDocumentation = ExtractXmlDocumentation(p);

                members.Add(new FacetMember(
                    p.Name,
                    p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    FacetMemberKind.Property,
                    shouldPreserveInitOnly,
                    shouldPreserveRequired,
                    memberXmlDocumentation));
                addedMembers.Add(p.Name);
            }
            else if (includeFields && member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public } f)
            {
                var shouldPreserveRequired = preserveRequired && isRequired;
                var memberXmlDocumentation = ExtractXmlDocumentation(f);

                members.Add(new FacetMember(
                    f.Name,
                    f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    FacetMemberKind.Field,
                    false, // Fields don't have init-only
                    shouldPreserveRequired,
                    memberXmlDocumentation));
                addedMembers.Add(f.Name);
            }
        }

        var ns = targetSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : targetSymbol.ContainingNamespace.ToDisplayString();

        // Check if the target type already has a primary constructor
        var hasExistingPrimaryConstructor = HasExistingPrimaryConstructor(targetSymbol);

        return new FacetTargetModel(
            targetSymbol.Name,
            ns,
            kind,
            generateConstructor,
            generateParameterlessConstructor,
            generateProjection,
            sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            configurationTypeName,
            members.ToImmutableArray(),
            hasExistingPrimaryConstructor,
            typeXmlDocumentation);
    }

    /// <summary>
    /// Checks if the target type already has a primary constructor defined.
    /// For records, this means checking if the user has defined constructor parameters.
    /// </summary>
    private static bool HasExistingPrimaryConstructor(INamedTypeSymbol targetSymbol)
    {
        // Check if this is a record type with an existing primary constructor
        if (targetSymbol.TypeKind == TypeKind.Class || targetSymbol.TypeKind == TypeKind.Struct)
        {
            // Look at the syntax to see if it has primary constructor parameters
            var syntaxRef = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef != null)
            {
                var syntax = syntaxRef.GetSyntax();

                // Check for record with parameter list
                if (syntax is RecordDeclarationSyntax recordDecl && recordDecl.ParameterList != null && recordDecl.ParameterList.Parameters.Count > 0)
                {
                    return true;
                }

                // Check for regular class/struct with primary constructor (C# 12+)
                if ((syntax is ClassDeclarationSyntax classDecl && classDecl.ParameterList != null && classDecl.ParameterList.Parameters.Count > 0) ||
                    (syntax is StructDeclarationSyntax structDecl && structDecl.ParameterList != null && structDecl.ParameterList.Parameters.Count > 0))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets all members from the inheritance hierarchy, starting from the most derived type
    /// and walking up to the base types. This ensures that overridden members are preferred.
    /// </summary>
    private static IEnumerable<(ISymbol Symbol, bool IsInitOnly, bool IsRequired)> GetAllMembersWithModifiers(INamedTypeSymbol type)
    {
        var visited = new HashSet<string>();
        var current = type;

        while (current != null)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.DeclaredAccessibility == Accessibility.Public &&
                    !visited.Contains(member.Name))
                {
                    if (member is IPropertySymbol prop)
                    {
                        visited.Add(member.Name);
                        var isInitOnly = prop.SetMethod?.IsInitOnly == true;
                        var isRequired = prop.IsRequired;
                        yield return (prop, isInitOnly, isRequired);
                    }
                    else if (member is IFieldSymbol field)
                    {
                        visited.Add(member.Name);
                        var isRequired = field.IsRequired;
                        yield return (field, false, isRequired);
                    }
                }
            }

            current = current.BaseType;

            if (current?.SpecialType == SpecialType.System_Object)
                break;
        }
    }

    /// <summary>
    /// Gets all members from the inheritance hierarchy, starting from the most derived type
    /// and walking up to the base types. This ensures that overridden members are preferred.
    /// </summary>
    private static IEnumerable<ISymbol> GetAllMembers(INamedTypeSymbol type)
    {
        return GetAllMembersWithModifiers(type).Select(x => x.Symbol);
    }

    /// <summary>
    /// Attempts to determine the FacetKind from the target symbol's declaration.
    /// </summary>
    private static FacetKind InferFacetKind(INamedTypeSymbol targetSymbol)
    {
        if (targetSymbol.TypeKind == TypeKind.Struct)
        {
            var syntax = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (syntax != null && syntax.ToString().Contains("record struct"))
            {
                return FacetKind.RecordStruct;
            }
            return FacetKind.Struct;
        }

        if (targetSymbol.TypeKind == TypeKind.Class)
        {
            var syntax = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (syntax != null && syntax.ToString().Contains("record "))
            {
                return FacetKind.Record;
            }

            if (targetSymbol.GetMembers().Any(m => m.Name.Contains("Clone") && m.IsImplicitlyDeclared))
            {
                return FacetKind.Record;
            }

            return FacetKind.Class;
        }

        return FacetKind.Class;
    }

    private static T GetNamedArg<T>(
        ImmutableArray<KeyValuePair<string, TypedConstant>> args,
        string name,
        T defaultValue)
        => args.FirstOrDefault(kv => kv.Key == name)
            .Value.Value is T t ? t : defaultValue;

    /// <summary>
    /// Extracts and formats XML documentation from a symbol.
    /// </summary>
    private static string? ExtractXmlDocumentation(ISymbol symbol)
    {
        var documentationComment = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(documentationComment))
            return null;

        return FormatXmlDocumentation(documentationComment);
    }

    /// <summary>
    /// Formats XML documentation comment into proper /// format for code generation.
    /// </summary>
    private static string FormatXmlDocumentation(string xmlDoc)
    {
        if (string.IsNullOrWhiteSpace(xmlDoc))
            return string.Empty;

        var lines = new List<string>();
        
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xmlDoc);
            var root = doc.Root;
            
            if (root == null)
                return string.Empty;

            // Process summary
            var summary = root.Element("summary");
            if (summary != null)
            {
                lines.Add("/// <summary>");
                var summaryText = summary.Value.Trim();
                if (!string.IsNullOrEmpty(summaryText))
                {
                    foreach (var line in summaryText.Split('\n'))
                    {
                        lines.Add($"/// {line.Trim()}");
                    }
                }
                lines.Add("/// </summary>");
            }

            // Process value
            var value = root.Element("value");
            if (value != null)
            {
                lines.Add("/// <value>");
                var valueText = value.Value.Trim();
                if (!string.IsNullOrEmpty(valueText))
                {
                    foreach (var line in valueText.Split('\n'))
                    {
                        lines.Add($"/// {line.Trim()}");
                    }
                }
                lines.Add("/// </value>");
            }

            // Process remarks
            var remarks = root.Element("remarks");
            if (remarks != null)
            {
                lines.Add("/// <remarks>");
                var remarksText = remarks.Value.Trim();
                if (!string.IsNullOrEmpty(remarksText))
                {
                    foreach (var line in remarksText.Split('\n'))
                    {
                        lines.Add($"/// {line.Trim()}");
                    }
                }
                lines.Add("/// </remarks>");
            }

            // Process example
            var example = root.Element("example");
            if (example != null)
            {
                lines.Add("/// <example>");
                var exampleText = example.Value.Trim();
                if (!string.IsNullOrEmpty(exampleText))
                {
                    foreach (var line in exampleText.Split('\n'))
                    {
                        lines.Add($"/// {line.Trim()}");
                    }
                }
                lines.Add("/// </example>");
            }

            return lines.Count > 0 ? string.Join("\n", lines) : string.Empty;
        }
        catch
        {
            // If XML parsing fails, return empty string rather than crashing the generator
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the simple type name from a fully qualified type name.
    /// </summary>
    private static string GetSimpleTypeName(string fullyQualifiedTypeName)
    {
        var lastDotIndex = fullyQualifiedTypeName.LastIndexOf('.');
        if (lastDotIndex >= 0 && lastDotIndex < fullyQualifiedTypeName.Length - 1)
        {
            return fullyQualifiedTypeName.Substring(lastDotIndex + 1);
        }
        return fullyQualifiedTypeName;
    }

    private static string Generate(FacetTargetModel model)
    {
        var sb = new StringBuilder();
        GenerateFileHeader(sb);
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace};");
        }

        // Generate type-level XML documentation if available
        if (!string.IsNullOrWhiteSpace(model.TypeXmlDocumentation))
        {
            sb.AppendLine(model.TypeXmlDocumentation);
        }

        var keyword = model.Kind switch
        {
            FacetKind.Class => "class",
            FacetKind.Record => "record",
            FacetKind.RecordStruct => "record struct",
            FacetKind.Struct => "struct",
            _ => "class",
        };

        var isPositional = model.Kind is FacetKind.Record or FacetKind.RecordStruct && !model.HasExistingPrimaryConstructor;
        var hasInitOnlyProperties = model.Members.Any(m => m.IsInitOnly);
        var hasCustomMapping = !string.IsNullOrWhiteSpace(model.ConfigurationTypeName);

        // Only generate positional declaration if there's no existing primary constructor
        if (isPositional)
        {
            var parameters = string.Join(", ",
                model.Members.Select(m =>
                {
                    var param = $"{m.TypeName} {m.Name}";
                    // Add required modifier for positional parameters if needed
                    if (m.IsRequired && model.Kind == FacetKind.RecordStruct)
                    {
                        param = $"required {param}";
                    }
                    return param;
                }));
            sb.AppendLine($"public partial {keyword} {model.Name}({parameters});");
        }

        sb.AppendLine($"public partial {keyword} {model.Name}");
        sb.AppendLine("{");

        // Generate properties if not positional OR if there's an existing primary constructor
        if (!isPositional || model.HasExistingPrimaryConstructor)
        {
            foreach (var m in model.Members)
            {
                // Generate member XML documentation if available
                if (!string.IsNullOrWhiteSpace(m.XmlDocumentation))
                {
                    sb.AppendLine($"    {m.XmlDocumentation.Replace("\n", "\n    ")}");
                }

                if (m.Kind == FacetMemberKind.Property)
                {
                    var propDef = $"public {m.TypeName} {m.Name}";

                    if (m.IsInitOnly)
                    {
                        propDef += " { get; init; }";
                    }
                    else
                    {
                        propDef += " { get; set; }";
                    }

                    if (m.IsRequired)
                    {
                        propDef = $"required {propDef}";
                    }

                    sb.AppendLine($"    {propDef}");
                }
                else
                {
                    var fieldDef = $"public {m.TypeName} {m.Name};";
                    if (m.IsRequired)
                    {
                        fieldDef = $"required {fieldDef}";
                    }
                    sb.AppendLine($"    {fieldDef}");
                }
            }
        }

        // Generate constructor
        if (model.GenerateConstructor)
        {
            GenerateConstructor(sb, model, isPositional, hasInitOnlyProperties, hasCustomMapping);
        }

        // Generate parameterless constructor if requested
        if (model.GenerateParameterlessConstructor)
        {
            GenerateParameterlessConstructor(sb, model, isPositional);
        }

        // Generate projection
        if (model.GenerateExpressionProjection)
        {
            sb.AppendLine();

            if (model.HasExistingPrimaryConstructor && model.Kind is FacetKind.Record or FacetKind.RecordStruct)
            {
                // For records with existing primary constructors, the projection can't use the standard constructor approach
                sb.AppendLine($"    // Note: Projection generation is not supported for records with existing primary constructors.");
                sb.AppendLine($"    // You must manually create projection expressions or use the FromSource factory method.");
                sb.AppendLine($"    // Example: source => new {model.Name}(defaultPrimaryConstructorValue) {{ PropA = source.PropA, PropB = source.PropB }}");
            }
            else
            {
                // Generate projection XML documentation
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// Gets the projection expression for converting <see cref=\"{GetSimpleTypeName(model.SourceTypeName)}\"/> to <see cref=\"{model.Name}\"/>.");
                sb.AppendLine("    /// Use this for LINQ and Entity Framework query projections.");
                sb.AppendLine("    /// </summary>");
                sb.AppendLine($"    /// <value>An expression tree that can be used in LINQ queries for efficient database projections.</value>");
                sb.AppendLine("    /// <example>");
                sb.AppendLine("    /// <code>");
                sb.AppendLine($"    /// var dtos = context.{GetSimpleTypeName(model.SourceTypeName)}s");
                sb.AppendLine("    ///     .Where(x => x.IsActive)");
                sb.AppendLine($"    ///     .Select({model.Name}.Projection)");
                sb.AppendLine("    ///     .ToList();");
                sb.AppendLine("    /// </code>");
                sb.AppendLine("    /// </example>");
                sb.AppendLine($"    public static Expression<Func<{model.SourceTypeName}, {model.Name}>> Projection =>");
                sb.AppendLine($"        source => new {model.Name}(source);");
            }
        }

        // Generate reverse mapping method (BackTo)
        GenerateBackToMethod(sb, model);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateConstructor(StringBuilder sb, FacetTargetModel model, bool isPositional, bool hasInitOnlyProperties, bool hasCustomMapping)
    {
        // If the target has an existing primary constructor, skip constructor generation
        // and provide only a factory method
        if (model.HasExistingPrimaryConstructor && model.Kind is FacetKind.Record or FacetKind.RecordStruct)
        {
            GenerateFactoryMethodForExistingPrimaryConstructor(sb, model, hasCustomMapping);
            return;
        }

        // Generate constructor XML documentation
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the <see cref=\"{model.Name}\"/> class from the specified <see cref=\"{GetSimpleTypeName(model.SourceTypeName)}\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <param name=\"source\">The source <see cref=\"{GetSimpleTypeName(model.SourceTypeName)}\"/> object to copy data from.</param>");
        if (hasCustomMapping)
        {
            sb.AppendLine("    /// <remarks>");
            sb.AppendLine("    /// This constructor automatically maps all compatible properties and applies custom mapping logic.");
            sb.AppendLine("    /// </remarks>");
        }

        var ctorSig = $"public {model.Name}({model.SourceTypeName} source)";

        if (isPositional && !model.HasExistingPrimaryConstructor)
        {
            // Traditional positional record - chain to primary constructor
            var args = string.Join(", ",
                model.Members.Select(m => $"source.{m.Name}"));
            ctorSig += $" : this({args})";
        }

        sb.AppendLine($"    {ctorSig}");
        sb.AppendLine("    {");

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
                    sb.AppendLine($"        this.{m.Name} = source.{m.Name};");
            }
        }
        else if (hasCustomMapping && !model.HasExistingPrimaryConstructor)
        {
            // For positional records/record structs with custom mapping
            sb.AppendLine($"        {model.ConfigurationTypeName}.Map(source, this);");
        }

        sb.AppendLine("    }");

        // Add static factory method for types with init-only properties
        if (!isPositional && hasInitOnlyProperties && !model.HasExistingPrimaryConstructor)
        {
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Creates a new instance of <see cref=\"{model.Name}\"/> from the specified <see cref=\"{GetSimpleTypeName(model.SourceTypeName)}\"/> with init-only properties.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    /// <param name=\"source\">The source <see cref=\"{GetSimpleTypeName(model.SourceTypeName)}\"/> object to copy data from.</param>");
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
                    sb.AppendLine($"            {m.Name} = source.{m.Name}{comma}");
                }
                sb.AppendLine("        };");
            }

            sb.AppendLine("    }");
        }
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

    private static void GenerateFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//     This code was generated by the Facet source generator.");
        sb.AppendLine("//     Changes to this file may cause incorrect behavior and will be lost if");
        sb.AppendLine("//     the code is regenerated.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();
    }

    private static void GenerateParameterlessConstructor(StringBuilder sb, FacetTargetModel model, bool isPositional)
    {
        sb.AppendLine();

        // Don't generate parameterless constructor for records with existing primary constructors
        // as it would conflict with the C# language rules
        if (model.HasExistingPrimaryConstructor && model.Kind is FacetKind.Record or FacetKind.RecordStruct)
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
            var defaultValues = model.Members.Select(m => GetDefaultValue(m.TypeName)).ToArray();
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

    private static string GetDefaultValue(string typeName)
    {
        // Handle nullable types
        if (typeName.EndsWith("?"))
        {
            return "null";
        }

        // Handle common value types
        return typeName switch
        {
            "bool" => "false",
            "byte" => "0",
            "sbyte" => "0",
            "short" => "0",
            "ushort" => "0",
            "int" => "0",
            "uint" => "0",
            "long" => "0",
            "ulong" => "0",
            "float" => "0f",
            "double" => "0d",
            "decimal" => "0m",
            "char" => "'\\0'",
            "string" => "string.Empty",
            var t when t.StartsWith("System.DateTime") => "default",
            var t when t.StartsWith("System.DateTimeOffset") => "default",
            var t when t.StartsWith("System.TimeSpan") => "default",
            var t when t.StartsWith("System.Guid") => "default",
            // For other types, use default() expression
            _ => "default"
        };
    }

    private static void GenerateBackToMethod(StringBuilder sb, FacetTargetModel model)
    {
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Converts this instance of <see cref=\"{model.Name}\"/> to an instance of the entity type.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <returns>An instance of the entity type with properties mapped from this instance.</returns>");
        sb.AppendLine($"    public {model.SourceTypeName} BackTo()");
        sb.AppendLine("    {");
        sb.AppendLine("        return new " + model.SourceTypeName + " {");

        // TODO this needs work - this won't work when the entity has a positional constructor, and it also won't work when the entity has properties without setters
        var propertyAssignments = model.Members
            .Select(m => $"            {m.Name} = this.{m.Name}");
        sb.AppendLine(string.Join(",\n", propertyAssignments));

        sb.AppendLine("        };");
        sb.AppendLine("    }");
    }
}
