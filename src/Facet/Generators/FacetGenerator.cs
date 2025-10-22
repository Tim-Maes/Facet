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

        // Collect all facet models to enable nested facet lookup during generation
        var allFacets = facets.Collect();

        context.RegisterSourceOutput(allFacets, static (spc, models) =>
        {
            spc.CancellationToken.ThrowIfCancellationRequested();

            // Build a lookup dictionary for nested facet resolution
            var facetLookup = models
                .Where(m => m is not null)
                .ToDictionary(m => m!.FullName, m => m!);

            // Generate code for each facet with access to all facet models
            foreach (var model in models)
            {
                if (model is null) continue;

                var code = Generate(model, facetLookup);
                spc.AddSource($"{model.FullName}.g.cs", SourceText.From(code, Encoding.UTF8));
            }
        });
    }

    /// <summary>
    /// Extracts nested facet mappings from the NestedFacets parameter.
    /// Returns a dictionary mapping source type full names to nested facet type information.
    /// </summary>
    private static Dictionary<string, (string childFacetTypeName, string sourceTypeName)> ExtractNestedFacetMappings(
        AttributeData attribute,
        Compilation compilation)
    {
        var mappings = new Dictionary<string, (string, string)>();

        var childrenArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "NestedFacets");
        if (childrenArg.Value.Kind != TypedConstantKind.Error && !childrenArg.Value.IsNull)
        {
            if (childrenArg.Value.Kind == TypedConstantKind.Array)
            {
                foreach (var childValue in childrenArg.Value.Values)
                {
                    if (childValue.Value is INamedTypeSymbol childFacetType)
                    {
                        // Find the Facet attribute on the child type to get its source type
                        var childFacetAttr = childFacetType.GetAttributes()
                            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Facet.FacetAttribute");

                        if (childFacetAttr != null && childFacetAttr.ConstructorArguments.Length > 0)
                        {
                            if (childFacetAttr.ConstructorArguments[0].Value is INamedTypeSymbol childSourceType)
                            {
                                var sourceTypeName = childSourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                var childFacetTypeName = childFacetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                                // Map the source type to the child facet type
                                mappings[sourceTypeName] = (childFacetTypeName, sourceTypeName);
                            }
                        }
                    }
                }
            }
        }

        return mappings;
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

        var excluded = new HashSet<string>();
        var included = new HashSet<string>();
        bool isIncludeMode = false;

        if (attribute.ConstructorArguments.Length > 1)
        {
            var excludeArg = attribute.ConstructorArguments[1];
            if (excludeArg.Kind == TypedConstantKind.Array)
            {
                excluded = new HashSet<string>(
                    excludeArg.Values
                        .Select(v => v.Value?.ToString())
                        .Where(n => n != null)!);
            }
        }

        var includeArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Include");
        if (includeArg.Value.Kind != TypedConstantKind.Error && !includeArg.Value.IsNull)
        {
            if (includeArg.Value.Kind == TypedConstantKind.Array)
            {
                included = new HashSet<string>(
                    includeArg.Value.Values
                        .Select(v => v.Value?.ToString())
                        .Where(n => n != null)!);
                isIncludeMode = true;
            }
        }

        var includeFields = isIncludeMode 
            ? GetNamedArg(attribute.NamedArguments, "IncludeFields", false)
            : GetNamedArg(attribute.NamedArguments, "IncludeFields", false);

        var generateConstructor = GetNamedArg(attribute.NamedArguments, "GenerateConstructor", true);
        var generateParameterlessConstructor = GetNamedArg(attribute.NamedArguments, "GenerateParameterlessConstructor", true);
        var generateProjection = GetNamedArg(attribute.NamedArguments, "GenerateProjection", true);
        var generateBackTo = GetNamedArg(attribute.NamedArguments, "GenerateBackTo", true);

        var configurationTypeName = attribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == "Configuration")
            .Value.Value?
            .ToString();

        // Infer the type kind and whether it's a record from the target type declaration
        var (typeKind, isRecord) = InferTypeKind(targetSymbol);

        // For record types, default to preserving init-only and required modifiers
        // unless explicitly overridden by the user
        var preserveInitOnlyDefault = isRecord;
        var preserveRequiredDefault = isRecord;

        var preserveInitOnly = GetNamedArg(attribute.NamedArguments, "PreserveInitOnlyProperties", preserveInitOnlyDefault);
        var preserveRequired = GetNamedArg(attribute.NamedArguments, "PreserveRequiredProperties", preserveRequiredDefault);
        var nullableProperties = GetNamedArg(attribute.NamedArguments, "NullableProperties", false);
        var copyAttributes = GetNamedArg(attribute.NamedArguments, "CopyAttributes", false);

        // Extract nested facets parameter and build mapping from source type to child facet type
        var nestedFacetMappings = ExtractNestedFacetMappings(attribute, context.SemanticModel.Compilation);

        var members = new List<FacetMember>();
        var excludedRequiredMembers = new List<FacetMember>();
        var addedMembers = new HashSet<string>();

        var allMembersWithModifiers = GeneratorUtilities.GetAllMembersWithModifiers(sourceType);

        // Extract type-level XML documentation from the source type
        var typeXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(sourceType);

        foreach (var (member, isInitOnly, isRequired) in allMembersWithModifiers)
        {
            token.ThrowIfCancellationRequested();
            
            if (addedMembers.Contains(member.Name)) continue;

            bool shouldIncludeMember = false;

            if (isIncludeMode)
            {
                shouldIncludeMember = included.Contains(member.Name);
            }
            else
            {
                shouldIncludeMember = !excluded.Contains(member.Name);
            }

            if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } p)
            {
                var memberXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(p);

                if (!shouldIncludeMember)
                {
                    // If this is a required member that was excluded, track it for BackTo generation
                    if (isRequired)
                    {
                        excludedRequiredMembers.Add(new FacetMember(
                            p.Name,
                            GeneratorUtilities.GetTypeNameWithNullability(p.Type),
                            FacetMemberKind.Property,
                            p.Type.IsValueType,
                            isInitOnly,
                            isRequired,
                            false, // Properties are not readonly
                            memberXmlDocumentation));
                    }
                    continue;
                }

                var shouldPreserveInitOnly = preserveInitOnly && isInitOnly;
                var shouldPreserveRequired = preserveRequired && isRequired;

                var typeName = GeneratorUtilities.GetTypeNameWithNullability(p.Type);
                var propertyTypeName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                bool isNestedFacet = false;
                string? nestedFacetSourceTypeName = null;
                bool isCollection = false;
                string? collectionWrapper = null;

                // Check if the property type is nullable (reference types)
                bool isNullableReferenceType = p.Type.NullableAnnotation == NullableAnnotation.Annotated;

                // Check if this property's type is a collection
                if (GeneratorUtilities.TryGetCollectionElementType(p.Type, out var elementType, out var wrapper))
                {
                    // Check if the collection element type matches a child facet source type
                    var elementTypeName = elementType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (nestedFacetMappings.TryGetValue(elementTypeName, out var nestedMapping))
                    {
                        // Wrap the child facet type in the same collection type
                        var wrappedType = GeneratorUtilities.WrapInCollectionType(nestedMapping.childFacetTypeName, wrapper!);
                        // Preserve nullability if the collection itself was nullable
                        typeName = isNullableReferenceType ? wrappedType + "?" : wrappedType;
                        isNestedFacet = true;
                        isCollection = true;
                        collectionWrapper = wrapper;
                        nestedFacetSourceTypeName = nestedMapping.sourceTypeName;
                    }
                }
                // Check if this property's type matches a child facet source type (non-collection)
                else if (nestedFacetMappings.TryGetValue(propertyTypeName, out var nestedMapping))
                {
                    // Preserve nullability when assigning nested facet type name
                    typeName = isNullableReferenceType
                        ? nestedMapping.childFacetTypeName + "?"
                        : nestedMapping.childFacetTypeName;
                    isNestedFacet = true;
                    nestedFacetSourceTypeName = nestedMapping.sourceTypeName;
                }

                if (nullableProperties && !isNestedFacet)
                {
                    typeName = GeneratorUtilities.MakeNullable(typeName);
                }

                // Extract copiable attributes if requested
                var attributes = copyAttributes
                    ? AttributeProcessor.ExtractCopiableAttributes(p, FacetMemberKind.Property)
                    : new List<string>();

                members.Add(new FacetMember(
                    p.Name,
                    typeName,
                    FacetMemberKind.Property,
                    p.Type.IsValueType,
                    shouldPreserveInitOnly,
                    shouldPreserveRequired,
                    false, // Properties are not readonly
                    memberXmlDocumentation,
                    isNestedFacet,
                    nestedFacetSourceTypeName,
                    attributes,
                    isCollection,
                    collectionWrapper));
                addedMembers.Add(p.Name);
            }
            else if (includeFields && member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public } f)
            {
                var memberXmlDocumentation = CodeGenerationHelpers.ExtractXmlDocumentation(f);

                if (!shouldIncludeMember)
                {
                    // If this is a required field that was excluded, track it for BackTo generation
                    if (isRequired)
                    {
                        excludedRequiredMembers.Add(new FacetMember(
                            f.Name,
                            GeneratorUtilities.GetTypeNameWithNullability(f.Type),
                            FacetMemberKind.Field,
                            f.Type.IsValueType,
                            false, // Fields don't have init-only
                            isRequired,
                            f.IsReadOnly, // Fields can be readonly
                            memberXmlDocumentation));
                    }
                    continue;
                }

                var shouldPreserveRequired = preserveRequired && isRequired;

                var typeName = GeneratorUtilities.GetTypeNameWithNullability(f.Type);
                if (nullableProperties)
                {
                    typeName = GeneratorUtilities.MakeNullable(typeName);
                }

                // Extract copiable attributes if requested
                var attributes = copyAttributes
                    ? AttributeProcessor.ExtractCopiableAttributes(f, FacetMemberKind.Field)
                    : new List<string>();

                members.Add(new FacetMember(
                    f.Name,
                    typeName,
                    FacetMemberKind.Field,
                    f.Type.IsValueType,
                    false, // Fields don't have init-only
                    shouldPreserveRequired,
                    f.IsReadOnly, // Fields can be readonly
                    memberXmlDocumentation,
                    false, // Fields don't support nested facets
                    null,
                    attributes));
                addedMembers.Add(f.Name);
            }
        }

        var useFullName = GetNamedArg(attribute.NamedArguments, "UseFullName", false);

        string fullName = string.Empty;

        if (useFullName)
        {
            fullName = targetSymbol
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .GetSafeName();
        }
        else
        {
            fullName = targetSymbol.Name;
        }

        var ns = targetSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : targetSymbol.ContainingNamespace.ToDisplayString();

        // Get containing types for nested classes
        var containingTypes = GetContainingTypes(targetSymbol);

        // Check if the target type already has a primary constructor
        var hasExistingPrimaryConstructor = HasExistingPrimaryConstructor(targetSymbol);
        
        // Check if the source type has a positional constructor
        var hasPositionalConstructor = HasPositionalConstructor(sourceType);

        return new FacetTargetModel(
            targetSymbol.Name,
            ns,
            fullName,
            typeKind,
            isRecord,
            generateConstructor,
            generateParameterlessConstructor,
            generateProjection,
            generateBackTo,
            sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            configurationTypeName,
            members.ToImmutableArray(),
            hasExistingPrimaryConstructor,
            hasPositionalConstructor,
            typeXmlDocumentation,
            containingTypes,
            useFullName,
            excludedRequiredMembers.ToImmutableArray(),
            nullableProperties,
            copyAttributes);
    }

    /// <summary>
    /// Gets the containing types for nested classes, in order from outermost to innermost.
    /// </summary>
    private static ImmutableArray<string> GetContainingTypes(INamedTypeSymbol targetSymbol)
    {
        var containingTypes = new List<string>();
        var current = targetSymbol.ContainingType;

        while (current != null)
        {
            containingTypes.Insert(0, current.Name); // Insert at beginning to maintain order
            current = current.ContainingType;
        }

        return containingTypes.ToImmutableArray();
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
    /// Checks if the source type has a positional constructor (like records with primary constructors).
    /// </summary>
    private static bool HasPositionalConstructor(INamedTypeSymbol sourceType)
    {
        if (sourceType.TypeKind == TypeKind.Class || sourceType.TypeKind == TypeKind.Struct)
        {
            // Look at the syntax to see if it has primary constructor parameters
            var syntaxRef = sourceType.DeclaringSyntaxReferences.FirstOrDefault();
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
    /// Infers the TypeKind and whether it's a record from the target symbol's declaration.
    /// Returns a tuple of (TypeKind, IsRecord).
    /// </summary>
    private static (TypeKind typeKind, bool isRecord) InferTypeKind(INamedTypeSymbol targetSymbol)
    {
        var typeKind = targetSymbol.TypeKind;
        var isRecord = false;

        if (typeKind == TypeKind.Struct || typeKind == TypeKind.Class)
        {
            var syntax = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (syntax != null)
            {
                var syntaxText = syntax.ToString();
                if (syntaxText.Contains("record struct") || syntaxText.Contains("record "))
                {
                    isRecord = true;
                }
            }

            // Additional check for records by looking for the compiler-generated Clone method
            if (!isRecord && typeKind == TypeKind.Class)
            {
                if (targetSymbol.GetMembers().Any(m => m.Name.Contains("Clone") && m.IsImplicitlyDeclared))
                {
                    isRecord = true;
                }
            }
        }

        return (typeKind, isRecord);
    }

    private static T GetNamedArg<T>(
        ImmutableArray<KeyValuePair<string, TypedConstant>> args,
        string name,
        T defaultValue)
        => args.FirstOrDefault(kv => kv.Key == name)
            .Value.Value is T t ? t : defaultValue;

    private static string Generate(FacetTargetModel model, Dictionary<string, FacetTargetModel> facetLookup)
    {
        var sb = new StringBuilder();
        GenerateFileHeader(sb);

        // Collect all namespaces from referenced types
        var namespacesToImport = CodeGenerationHelpers.CollectNamespaces(model);

        // Generate using statements for all required namespaces
        foreach (var ns in namespacesToImport.OrderBy(x => x))
        {
            sb.AppendLine($"using {ns};");
        }
        sb.AppendLine();

        // Nullable must be enabled in generated code with a directive
        var hasNullableRefTypeMembers = model.Members.Any(m => !m.IsValueType && m.TypeName.EndsWith("?"));
        if (hasNullableRefTypeMembers)
        {
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace};");
        }

        // Generate containing type hierarchy for nested classes
        var containingTypeIndent = "";
        foreach (var containingType in model.ContainingTypes)
        {
            sb.AppendLine($"{containingTypeIndent}public partial class {containingType}");
            sb.AppendLine($"{containingTypeIndent}{{");
            containingTypeIndent += "    ";
        }

        // Generate type-level XML documentation if available
        if (!string.IsNullOrWhiteSpace(model.TypeXmlDocumentation))
        {
            var indentedDocumentation = model.TypeXmlDocumentation!.Replace("\n", $"\n{containingTypeIndent}");
            sb.AppendLine($"{containingTypeIndent}{indentedDocumentation}");
        }

        var keyword = (model.TypeKind, model.IsRecord) switch
        {
            (TypeKind.Class, false) => "class",
            (TypeKind.Class, true) => "record",
            (TypeKind.Struct, true) => "record struct",
            (TypeKind.Struct, false) => "struct",
            _ => "class",
        };

        var isPositional = model.IsRecord && !model.HasExistingPrimaryConstructor;
        var hasInitOnlyProperties = model.Members.Any(m => m.IsInitOnly);
        var hasRequiredProperties = model.Members.Any(m => m.IsRequired);
        var hasCustomMapping = !string.IsNullOrWhiteSpace(model.ConfigurationTypeName);

        // Only generate positional declaration if there's no existing primary constructor
        if (isPositional)
        {
            var parameters = string.Join(", ",
                model.Members.Select(m =>
                {
                    var param = $"{m.TypeName} {m.Name}";
                    // Add required modifier for positional parameters if needed
                    if (m.IsRequired && model.TypeKind == TypeKind.Struct && model.IsRecord)
                    {
                        param = $"required {param}";
                    }
                    return param;
                }));
            sb.AppendLine($"{containingTypeIndent}public partial {keyword} {model.Name}({parameters});");
        }

        sb.AppendLine($"{containingTypeIndent}public partial {keyword} {model.Name}");
        sb.AppendLine($"{containingTypeIndent}{{");

        var memberIndent = containingTypeIndent + "    ";

        // Generate properties if not positional OR if there's an existing primary constructor
        if (!isPositional || model.HasExistingPrimaryConstructor)
        {
            GenerateMembers(sb, model, memberIndent);
        }

        // Generate constructor
        if (model.GenerateConstructor)
        {
            GenerateConstructor(sb, model, isPositional, hasInitOnlyProperties, hasCustomMapping, hasRequiredProperties);
        }

        // Generate parameterless constructor if requested
        if (model.GenerateParameterlessConstructor)
        {
            GenerateParameterlessConstructor(sb, model, isPositional);
        }

        // Generate projection
        if (model.GenerateExpressionProjection)
        {
            GenerateProjectionProperty(sb, model, memberIndent, facetLookup);
        }

        // Generate reverse mapping method (BackTo)
        if (model.GenerateBackTo)
        {
            GenerateBackToMethod(sb, model);
        }

        sb.AppendLine($"{containingTypeIndent}}}");

        // Close containing type braces
        for (int i = model.ContainingTypes.Length - 1; i >= 0; i--)
        {
            containingTypeIndent = containingTypeIndent.Substring(0, containingTypeIndent.Length - 4);
            sb.AppendLine($"{containingTypeIndent}}}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates member declarations (properties and fields) for the target type.
    /// </summary>
    private static void GenerateMembers(StringBuilder sb, FacetTargetModel model, string memberIndent)
    {
        foreach (var m in model.Members)
        {
            // Generate member XML documentation if available
            if (!string.IsNullOrWhiteSpace(m.XmlDocumentation))
            {
                var indentedDocumentation = m.XmlDocumentation!.Replace("\n", $"\n{memberIndent}");
                sb.AppendLine($"{memberIndent}{indentedDocumentation}");
            }

            // Generate attributes if any
            foreach (var attribute in m.Attributes)
            {
                sb.AppendLine($"{memberIndent}{attribute}");
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

                sb.AppendLine($"{memberIndent}{propDef}");
            }
            else
            {
                var fieldDef = $"public {m.TypeName} {m.Name};";
                if (m.IsRequired)
                {
                    fieldDef = $"required {fieldDef}";
                }
                sb.AppendLine($"{memberIndent}{fieldDef}");
            }
        }
    }

    /// <summary>
    /// Generates the projection property for LINQ/EF Core query optimization.
    /// </summary>
    private static void GenerateProjectionProperty(StringBuilder sb, FacetTargetModel model, string memberIndent, Dictionary<string, FacetTargetModel> facetLookup)
    {
        sb.AppendLine();

        if (model.HasExistingPrimaryConstructor && model.IsRecord)
        {
            // For records with existing primary constructors, the projection can't use the standard constructor approach
            sb.AppendLine($"{memberIndent}// Note: Projection generation is not supported for records with existing primary constructors.");
            sb.AppendLine($"{memberIndent}// You must manually create projection expressions or use the FromSource factory method.");
            sb.AppendLine($"{memberIndent}// Example: source => new {model.Name}(defaultPrimaryConstructorValue) {{ PropA = source.PropA, PropB = source.PropB }}");
        }
        else
        {
            // Generate projection XML documentation
            sb.AppendLine($"{memberIndent}/// <summary>");
            sb.AppendLine($"{memberIndent}/// Gets the projection expression for converting <see cref=\"{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}\"/> to <see cref=\"{model.Name}\"/>.");
            sb.AppendLine($"{memberIndent}/// Use this for LINQ and Entity Framework query projections.");
            sb.AppendLine($"{memberIndent}/// </summary>");
            sb.AppendLine($"{memberIndent}/// <value>An expression tree that can be used in LINQ queries for efficient database projections.</value>");
            sb.AppendLine($"{memberIndent}/// <example>");
            sb.AppendLine($"{memberIndent}/// <code>");
            sb.AppendLine($"{memberIndent}/// var dtos = context.{CodeGenerationHelpers.GetSimpleTypeName(model.SourceTypeName)}s");
            sb.AppendLine($"{memberIndent}///     .Where(x => x.IsActive)");
            sb.AppendLine($"{memberIndent}///     .Select({model.Name}.Projection)");
            sb.AppendLine($"{memberIndent}///     .ToList();");
            sb.AppendLine($"{memberIndent}/// </code>");
            sb.AppendLine($"{memberIndent}/// </example>");
            sb.AppendLine($"{memberIndent}public static Expression<Func<{model.SourceTypeName}, {model.Name}>> Projection =>");

            // Generate object initializer projection for EF Core compatibility
            GenerateProjectionExpression(sb, model, memberIndent, facetLookup);
        }
    }

    /// <summary>
    /// Generates the projection expression body using object initializer syntax for EF Core compatibility.
    /// This allows EF Core to automatically include navigation properties without requiring explicit .Include() calls.
    /// </summary>
    private static void GenerateProjectionExpression(StringBuilder sb, FacetTargetModel model, string baseIndent, Dictionary<string, FacetTargetModel> facetLookup)
    {
        var indent = baseIndent + "    ";
        sb.AppendLine($"{indent}source => new {model.Name}");
        sb.AppendLine($"{indent}{{");

        var members = model.Members;
        var memberCount = members.Length;

        for (int i = 0; i < memberCount; i++)
        {
            var member = members[i];
            var comma = i < memberCount - 1 ? "," : "";
            var memberIndent = indent + "    ";

            // Generate the property assignment
            var projectionValue = GetProjectionValueExpression(member, "source", memberIndent, facetLookup);
            sb.Append($"{memberIndent}{member.Name} = {projectionValue}{comma}");

            // Add newline
            sb.AppendLine();
        }

        sb.AppendLine($"{indent}}};");
    }

    /// <summary>
    /// Gets the projection expression for a member that's compatible with EF Core query translation.
    /// For nested facets, generates nested object initializers instead of constructor calls.
    /// </summary>
    private static string GetProjectionValueExpression(FacetMember member, string sourceVariableName, string indent, Dictionary<string, FacetTargetModel> facetLookup)
    {
        // Check if the member type is nullable
        bool isNullable = member.TypeName.Contains("?");

        if (member.IsNestedFacet && member.IsCollection)
        {
            // For collection nested facets, use Select with nested projection
            var elementTypeName = ExtractElementTypeFromCollectionTypeName(member.TypeName);
            var nonNullableElementType = elementTypeName.TrimEnd('?');

            var collectionProjection = GenerateNestedCollectionProjection(
                $"{sourceVariableName}.{member.Name}",
                nonNullableElementType,
                member.NestedFacetSourceTypeName!,
                member.CollectionWrapper!,
                facetLookup);

            if (isNullable)
            {
                return $"{sourceVariableName}.{member.Name} != null ? {collectionProjection} : null";
            }

            return collectionProjection;
        }
        else if (member.IsNestedFacet)
        {
            // For single nested facets, inline expand the nested facet's members
            var nonNullableTypeName = member.TypeName.TrimEnd('?');
            var nestedSourceExpression = $"{sourceVariableName}.{member.Name}";

            // Try to look up the nested facet model
            // The TypeName might be fully qualified (global::Namespace.Type) or simple (Type)
            FacetTargetModel? nestedFacetModel = null;

            // Strip "global::" prefix and extract simple name
            var lookupName = nonNullableTypeName
                .Replace("global::", "")
                .Split('.', ':')
                .Last();

            // First try exact match with the lookup name
            if (facetLookup.TryGetValue(lookupName, out nestedFacetModel))
            {
                // Found exact match
            }
            else
            {
                // Try matching by simple name or full name
                foreach (var kvp in facetLookup)
                {
                    if (kvp.Key == lookupName ||
                        kvp.Value.Name == lookupName ||
                        kvp.Key.EndsWith("." + lookupName))
                    {
                        nestedFacetModel = kvp.Value;
                        break;
                    }
                }
            }

            string nestedProjection;
            if (nestedFacetModel != null)
            {
                // Recursively inline the nested facet's members
                nestedProjection = GenerateInlineNestedFacetInitializer(
                    nestedFacetModel,
                    nestedSourceExpression,
                    nonNullableTypeName,
                    indent,
                    facetLookup);
            }
            else
            {
                // Fallback to constructor call if we can't find the nested facet model
                nestedProjection = $"new {nonNullableTypeName}({nestedSourceExpression})";
            }

            if (isNullable)
            {
                return $"{nestedSourceExpression} != null ? {nestedProjection} : null";
            }

            return nestedProjection;
        }

        // Regular property - direct assignment
        return $"{sourceVariableName}.{member.Name}";
    }

    /// <summary>
    /// Generates an inline object initializer for a nested facet, recursively expanding all members.
    /// </summary>
    private static string GenerateInlineNestedFacetInitializer(
        FacetTargetModel nestedFacetModel,
        string sourceExpression,
        string facetTypeName,
        string indent,
        Dictionary<string, FacetTargetModel> facetLookup)
    {
        var sb = new StringBuilder();
        sb.Append($"new {facetTypeName} {{ ");

        var members = nestedFacetModel.Members;
        for (int i = 0; i < members.Length; i++)
        {
            var member = members[i];
            var projectionValue = GetProjectionValueExpression(member, sourceExpression, indent, facetLookup);
            sb.Append($"{member.Name} = {projectionValue}");

            if (i < members.Length - 1)
            {
                sb.Append(", ");
            }
        }

        sb.Append(" }");
        return sb.ToString();
    }

    /// <summary>
    /// Generates a collection projection expression for nested facets.
    /// </summary>
    private static string GenerateNestedCollectionProjection(
        string sourceCollectionExpression,
        string elementFacetTypeName,
        string elementSourceTypeName,
        string collectionWrapper,
        Dictionary<string, FacetTargetModel> facetLookup)
    {
        // Try to find the nested facet model to inline expand it
        FacetTargetModel? nestedFacetModel = null;

        // Strip "global::" prefix and extract simple name
        var lookupName = elementFacetTypeName
            .Replace("global::", "")
            .Split('.', ':')
            .Last();

        // First try exact match with the lookup name
        if (facetLookup.TryGetValue(lookupName, out nestedFacetModel))
        {
            // Found exact match
        }
        else
        {
            // Try matching by simple name or full name
            foreach (var kvp in facetLookup)
            {
                if (kvp.Key == lookupName ||
                    kvp.Value.Name == lookupName ||
                    kvp.Key.EndsWith("." + lookupName))
                {
                    nestedFacetModel = kvp.Value;
                    break;
                }
            }
        }

        string projection;
        if (nestedFacetModel != null)
        {
            // Inline expand the nested facet
            var inlineInitializer = GenerateInlineNestedFacetInitializer(nestedFacetModel, "x", elementFacetTypeName, "", facetLookup);
            projection = $"{sourceCollectionExpression}.Select(x => {inlineInitializer})";
        }
        else
        {
            // Fallback to constructor call
            projection = $"{sourceCollectionExpression}.Select(x => new {elementFacetTypeName}(x))";
        }

        return collectionWrapper switch
        {
            "array" => $"{projection}.ToArray()",
            "IEnumerable" => projection,
            _ => $"{projection}.ToList()"
        };
    }

    private static void GenerateConstructor(StringBuilder sb, FacetTargetModel model, bool isPositional, bool hasInitOnlyProperties, bool hasCustomMapping, bool hasRequiredProperties)
    {
        var indent = CodeGenerationHelpers.GetIndentation(model);

        // If the target has an existing primary constructor, skip constructor generation
        // and provide only a factory method
        if (model.HasExistingPrimaryConstructor && model.IsRecord)
        {
            GenerateFactoryMethodForExistingPrimaryConstructor(sb, model, hasCustomMapping);
            return;
        }

        // For now, keep the hardcoded indentation approach but add the proper nesting support later
        // Generate constructor XML documentation
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

        if (isPositional && !model.HasExistingPrimaryConstructor)
        {
            // Traditional positional record - chain to primary constructor
            var args = string.Join(", ",
                model.Members.Select(m => GetSourceValueExpression(m, "source")));
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
                    var sourceValue = GetSourceValueExpression(m, "source");
                    sb.AppendLine($"        this.{m.Name} = {sourceValue};");
                }
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
                    var sourceValue = GetSourceValueExpression(m, "source");
                    sb.AppendLine($"            {m.Name} = {sourceValue}{comma}");
                }
                sb.AppendLine("        };");
            }

            sb.AppendLine("    }");
        }
    }

    /// <summary>
    /// Gets the appropriate source value expression for a member.
    /// For nested facets, returns "new NestedFacetType(source.PropertyName)" with null checks if nullable.
    /// For collection nested facets, returns "source.PropertyName.Select(x => new NestedFacetType(x)).ToList()" with null checks if nullable.
    /// For regular members, returns "source.PropertyName".
    /// </summary>
    private static string GetSourceValueExpression(FacetMember member, string sourceVariableName)
    {
        bool isNullable = member.TypeName.Contains("?");

        if (member.IsNestedFacet && member.IsCollection)
        {
            var elementTypeName = ExtractElementTypeFromCollectionTypeName(member.TypeName);

            // Use LINQ Select to map each element
            var projection = $"{sourceVariableName}.{member.Name}.Select(x => new {elementTypeName}(x))";

            // Convert back to the appropriate collection type
            var collectionExpression = member.CollectionWrapper switch
            {
                "List" => $"{projection}.ToList()",
                "IList" => $"{projection}.ToList()",
                "ICollection" => $"{projection}.ToList()",
                "IEnumerable" => projection,
                "array" => $"{projection}.ToArray()",
                _ => projection
            };

            if (isNullable)
            {
                return $"{sourceVariableName}.{member.Name} != null ? {collectionExpression} : null";
            }

            return collectionExpression;
        }
        else if (member.IsNestedFacet)
        {
            var nonNullableTypeName = member.TypeName.TrimEnd('?');

            if (isNullable)
            {
                return $"{sourceVariableName}.{member.Name} != null ? new {nonNullableTypeName}({sourceVariableName}.{member.Name}) : null";
            }

            // Use the nested facet's generated constructor
            return $"new {nonNullableTypeName}({sourceVariableName}.{member.Name})";
        }

        return $"{sourceVariableName}.{member.Name}";
    }

    /// <summary>
    /// Extracts the element type name from a collection type name.
    /// For example: "global::System.Collections.Generic.List<global::MyNamespace.MyType>" => "global::MyNamespace.MyType"
    /// </summary>
    private static string ExtractElementTypeFromCollectionTypeName(string collectionTypeName)
    {
        // Handle array syntax
        if (collectionTypeName.EndsWith("[]"))
        {
            return collectionTypeName.Substring(0, collectionTypeName.Length - 2);
        }

        // Handle generic collection syntax
        var startIndex = collectionTypeName.IndexOf('<');
        var endIndex = collectionTypeName.LastIndexOf('>');

        if (startIndex > 0 && endIndex > startIndex)
        {
            return collectionTypeName.Substring(startIndex + 1, endIndex - startIndex - 1);
        }

        return collectionTypeName;
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

    private static void GenerateBackToMethod(StringBuilder sb, FacetTargetModel model)
    {
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Converts this instance of <see cref=\"{model.Name}\"/> to an instance of the source type.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <returns>An instance of the source type with properties mapped from this instance.</returns>");
        sb.AppendLine($"    public {model.SourceTypeName} BackTo()");
        sb.AppendLine("    {");

        if (model.SourceHasPositionalConstructor)
        {
            // For source types with positional constructors (like records), use positional syntax
            var constructorArgs = string.Join(", ", model.Members.Select(m => GetBackToValueExpression(m)));
            sb.AppendLine($"        return new {model.SourceTypeName}({constructorArgs});");
        }
        else
        {
            // For source types without positional constructors, use object initializer syntax
            sb.AppendLine($"        return new {model.SourceTypeName}");
            sb.AppendLine("        {");

            var propertyAssignments = new List<string>();

            // Add assignments for included properties
            foreach (var member in model.Members)
            {
                var backToValue = GetBackToValueExpression(member);
                propertyAssignments.Add($"            {member.Name} = {backToValue}");
            }

            // Add default values for excluded required members
            foreach (var excludedMember in model.ExcludedRequiredMembers)
            {
                var defaultValue = GeneratorUtilities.GetDefaultValueForType(excludedMember.TypeName);
                propertyAssignments.Add($"            {excludedMember.Name} = {defaultValue}");
            }

            sb.AppendLine(string.Join(",\n", propertyAssignments));
            sb.AppendLine("        };");
        }

        sb.AppendLine("    }");
    }

    /// <summary>
    /// Gets the appropriate value expression for mapping back to the source type.
    /// For child facets, returns "this.PropertyName.BackTo()" with null checks if nullable.
    /// For collection child facets, returns "this.PropertyName.Select(x => x.BackTo()).ToList()" with null checks if nullable.
    /// For regular members, returns "this.PropertyName".
    /// </summary>
    private static string GetBackToValueExpression(FacetMember member)
    {
        // Check if the member type is nullable (ends with ?)
        bool isNullable = member.TypeName.Contains("?");

        if (member.IsNestedFacet && member.IsCollection)
        {
            // Use LINQ Select to map each element back
            var projection = $"this.{member.Name}.Select(x => x.BackTo())";

            // Convert back to the appropriate collection type
            var collectionExpression = member.CollectionWrapper switch
            {
                "List" => $"{projection}.ToList()",
                "IList" => $"{projection}.ToList()",
                "ICollection" => $"{projection}.ToList()",
                "IEnumerable" => projection,
                "array" => $"{projection}.ToArray()",
                _ => projection
            };

            // Add null check for nullable collections
            if (isNullable)
            {
                return $"this.{member.Name} != null ? {collectionExpression} : null";
            }

            return collectionExpression;
        }
        else if (member.IsNestedFacet)
        {
            // Add null check for nullable nested facets
            if (isNullable)
            {
                return $"this.{member.Name} != null ? this.{member.Name}.BackTo() : null";
            }

            // Use the child facet's generated BackTo method
            return $"this.{member.Name}.BackTo()";
        }

        return $"this.{member.Name}";
    }

}

