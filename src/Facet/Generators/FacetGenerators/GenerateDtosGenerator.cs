using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace Facet.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class GenerateDtosGenerator : IIncrementalGenerator
{
    private const string GenerateDtosAttributeName = "Facet.GenerateDtosAttribute";
    private const string GenerateAuditableDtosAttributeName = "Facet.GenerateAuditableDtosAttribute";

    // Diagnostic for generator internal errors
    private static readonly DiagnosticDescriptor GeneratorErrorRule = new DiagnosticDescriptor(
        "FAC100",
        "GenerateDtos generator encountered an error",
        "Error generating DTOs for type '{0}': {1}",
        "Generator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The GenerateDtos source generator encountered an unexpected error while processing this type.");

    // Common audit field patterns
    private static readonly HashSet<string> DefaultAuditFields = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "CreatedDate", "UpdatedDate", "CreatedAt", "UpdatedAt",
        "CreatedBy", "UpdatedBy", "CreatedById", "UpdatedById"
    };

    // Common ID field patterns
    private static readonly HashSet<string> IdFieldPatterns = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "Id"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generateDtosTargets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateDtosAttributeName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, token) => GetGenerateDtosModels(ctx, token))
            .Where(static m => m is not null)
            .SelectMany(static (models, _) => models!);

        var generateAuditableDtosTargets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateAuditableDtosAttributeName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, token) => GetGenerateAuditableDtosModels(ctx, token))
            .Where(static m => m is not null)
            .SelectMany(static (models, _) => models!);

        var allTargets = generateDtosTargets.Collect().Combine(generateAuditableDtosTargets.Collect())
            .Select(static (combined, _) => combined.Left.Concat(combined.Right));

        context.RegisterSourceOutput(allTargets, (spc, models) =>
        {
            foreach (var model in models)
            {
                if (model != null)
                {
                    spc.CancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        GenerateDtosForModel(spc, model);
                    }
                    catch (Exception ex)
                    {
                        var diagnostic = Diagnostic.Create(
                            GeneratorErrorRule,
                            Location.None,
                            GetSimpleTypeName(model.SourceTypeName),
                            ex.Message);
                        spc.ReportDiagnostic(diagnostic);
                    }
                }
            }
        });
    }

    private static IEnumerable<GenerateDtosTargetModel>? GetGenerateDtosModels(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        return GetDtosModels(context, token, isAuditable: false);
    }

    private static IEnumerable<GenerateDtosTargetModel>? GetGenerateAuditableDtosModels(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        return GetDtosModels(context, token, isAuditable: true);
    }

    private static IEnumerable<GenerateDtosTargetModel>? GetDtosModels(GeneratorAttributeSyntaxContext context, CancellationToken token, bool isAuditable)
    {
        token.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not INamedTypeSymbol sourceSymbol) return null;
        if (context.Attributes.Length == 0) return null;

        var models = new List<GenerateDtosTargetModel>();

        // Process each attribute separately to support AllowMultiple
        foreach (var attribute in context.Attributes)
        {
            token.ThrowIfCancellationRequested();

            var model = GetDtosModel(context, attribute, sourceSymbol, isAuditable, token);
            if (model != null)
            {
                models.Add(model);
            }
        }

        return models.Count > 0 ? models : null;
    }

    private static GenerateDtosTargetModel? GetDtosModel(GeneratorAttributeSyntaxContext context, AttributeData attribute, INamedTypeSymbol sourceSymbol, bool isAuditable, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        try
        {
            // Extract attribute properties with proper enum handling
            var types = GetNamedArg(attribute.NamedArguments, "Types", DtoTypes.All);
            var outputType = GetNamedArg(attribute.NamedArguments, "OutputType", OutputType.Record);
            var targetNamespace = GetNamedArg<string?>(attribute.NamedArguments, "Namespace", null);
            var prefix = GetNamedArg<string?>(attribute.NamedArguments, "Prefix", null);
            var suffix = GetNamedArg<string?>(attribute.NamedArguments, "Suffix", null);
            var includeFields = GetNamedArg(attribute.NamedArguments, "IncludeFields", false);
            var generateConstructors = GetNamedArg(attribute.NamedArguments, "GenerateConstructors", true);
            var generateProjections = GetNamedArg(attribute.NamedArguments, "GenerateProjections", true);
            var useFullName = GetNamedArg(attribute.NamedArguments, "UseFullName", false);

            // Fix the ExcludeProperties handling
            var userExcludeProperties = new List<string>();
            var excludePropertiesArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "ExcludeProperties");
            if (excludePropertiesArg.Value.Kind != TypedConstantKind.Error && !excludePropertiesArg.Value.IsNull)
            {
                if (excludePropertiesArg.Value.Kind == TypedConstantKind.Array)
                {
                    userExcludeProperties.AddRange(
                        excludePropertiesArg.Value.Values
                            .Where(v => v.Value?.ToString() != null)
                            .Select(v => v.Value!.ToString()!));
                }
            }

            // Build exclusion list
            var excludeProperties = new HashSet<string>(userExcludeProperties, System.StringComparer.OrdinalIgnoreCase);

            if (isAuditable)
            {
                foreach (var field in DefaultAuditFields)
                {
                    excludeProperties.Add(field);
                }
            }

            var members = new List<FacetMember>();
            var addedMembers = new HashSet<string>();

            var allMembersWithModifiers = GeneratorUtilities.GetAllMembersWithModifiers(sourceSymbol);

            foreach (var (member, isInitOnly, isRequired) in allMembersWithModifiers)
            {
                token.ThrowIfCancellationRequested();
                if (excludeProperties.Contains(member.Name)) continue;
                if (addedMembers.Contains(member.Name)) continue;

                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } p)
                {
                    members.Add(new FacetMember(
                        p.Name,
                        GeneratorUtilities.GetTypeNameWithNullability(p.Type),
                        FacetMemberKind.Property,
                        p.Type.IsValueType,
                        isInitOnly,
                        isRequired,
                        false, // Properties are not readonly in the field sense
                        null)); // No XML documentation for GenerateDtos
                    addedMembers.Add(p.Name);
                }
                else if (includeFields && member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public } f)
                {
                    bool isReadOnly = f.IsReadOnly;
                    members.Add(new FacetMember(
                        f.Name,
                        GeneratorUtilities.GetTypeNameWithNullability(f.Type),
                        FacetMemberKind.Field,
                        f.Type.IsValueType,
                        false, // Fields don't have init-only
                        isRequired,
                        isReadOnly,
                        null)); // No XML documentation for GenerateDtos
                    addedMembers.Add(f.Name);
                }
            }

            var sourceNamespace = sourceSymbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : sourceSymbol.ContainingNamespace.ToDisplayString();

            return new GenerateDtosTargetModel(
                sourceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                sourceNamespace,
                targetNamespace ?? sourceNamespace,
                types,
                outputType,
                prefix,
                suffix,
                includeFields,
                generateConstructors,
                generateProjections,
                excludeProperties.ToImmutableArray(),
                members.ToImmutableArray(),
                useFullName);
        }
        catch (Exception ex)
        {
            // Return null to skip this model, but the error is captured in the exception
            // Note: In incremental generators, we can't report diagnostics from the transform phase.
            // Consider adding error information to the model in the future to report in output phase.
            System.Diagnostics.Debug.WriteLine($"GenerateDtos error for {sourceSymbol.Name}: {ex.Message}");
            return null;
        }
    }

    private static void GenerateDtosForModel(SourceProductionContext context, GenerateDtosTargetModel model)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var sourceTypeName = GetSimpleTypeName(model.SourceTypeName);

        // Generate Create DTO (excludes ID fields)
        if ((model.Types & DtoTypes.Create) != 0)
        {
            var createMembers = FilterMembers(model.Members, model.ExcludeProperties, IdFieldPatterns);
            var createDtoName = BuildDtoName(sourceTypeName, "Create", "Request", model.Prefix, model.Suffix);
            var createCode = GenerateDtoCode(model, createDtoName, createMembers, "Create");
            context.AddSource($"{GenerateFileDtoFullName(model, createDtoName)}", SourceText.From(createCode, Encoding.UTF8));
        }

        // Generate Update DTO (includes ID for identification)
        if ((model.Types & DtoTypes.Update) != 0)
        {
            var updateMembers = FilterMembers(model.Members, model.ExcludeProperties);
            var updateDtoName = BuildDtoName(sourceTypeName, "Update", "Request", model.Prefix, model.Suffix);
            var updateCode = GenerateDtoCode(model, updateDtoName, updateMembers, "Update");
            context.AddSource($"{GenerateFileDtoFullName(model, updateDtoName)}", SourceText.From(updateCode, Encoding.UTF8));
        }

        // Generate Upsert DTO (includes ID, can be null for create)
        if ((model.Types & DtoTypes.Upsert) != 0)
        {
            var upsertMembers = FilterMembers(model.Members, model.ExcludeProperties);
            var upsertDtoName = BuildDtoName(sourceTypeName, "Upsert", "Request", model.Prefix, model.Suffix);
            var upsertCode = GenerateDtoCode(model, upsertDtoName, upsertMembers, "Upsert");
            context.AddSource($"{GenerateFileDtoFullName(model, upsertDtoName)}", SourceText.From(upsertCode, Encoding.UTF8));
        }

        // Generate Response DTO (all non-excluded properties)
        if ((model.Types & DtoTypes.Response) != 0)
        {
            var responseMembers = FilterMembers(model.Members, model.ExcludeProperties);
            var responseDtoName = BuildDtoName(sourceTypeName, "", "Response", model.Prefix, model.Suffix);
            var responseCode = GenerateDtoCode(model, responseDtoName, responseMembers, "Response");
            context.AddSource($"{GenerateFileDtoFullName(model, responseDtoName)}", SourceText.From(responseCode, Encoding.UTF8));
        }

        // Generate Query DTO
        if ((model.Types & DtoTypes.Query) != 0)
        {
            var queryMembers = model.Members.Select(m => new FacetMember(
                m.Name,
                GeneratorUtilities.MakeNullable(m.TypeName),
                m.Kind,
                m.IsInitOnly,
                false)) // Make all properties optional in Query DTOs
                .ToImmutableArray();

            var queryDtoName = BuildDtoName(sourceTypeName, "", "Query", model.Prefix, model.Suffix);

            var queryCode = GenerateDtoCode(model, queryDtoName, queryMembers, "Query");
            context.AddSource($"{GenerateFileDtoFullName(model, queryDtoName)}", SourceText.From(queryCode, Encoding.UTF8));
        }

        // Generate Patch DTO (uses Optional<T> to distinguish between unspecified and null)
        if ((model.Types & DtoTypes.Patch) != 0)
        {
            var patchMembers = FilterMembers(model.Members, model.ExcludeProperties);
            var patchDtoName = BuildDtoName(sourceTypeName, "", "Patch", model.Prefix, model.Suffix);
            var patchCode = GeneratePatchDtoCode(model, patchDtoName, patchMembers);
            context.AddSource($"{GenerateFileDtoFullName(model, patchDtoName)}", SourceText.From(patchCode, Encoding.UTF8));
        }
    }

    private static string BuildDtoName(string sourceTypeName, string prefix, string suffix, string? customPrefix, string? customSuffix)
    {
        var name = sourceTypeName;

        if (!string.IsNullOrWhiteSpace(customPrefix))
            name = customPrefix + name;

        if (!string.IsNullOrWhiteSpace(prefix))
            name = prefix + name;

        if (!string.IsNullOrWhiteSpace(suffix))
            name = name + suffix;

        if (!string.IsNullOrWhiteSpace(customSuffix))
            name = name + customSuffix;

        return name;
    }

    private static string GenerateFileDtoFullName(GenerateDtosTargetModel model, string dtoName)
    {
        if (!model.UseFullName)
        {
            return $"{dtoName}.g.cs";
        }

        var ns = string.IsNullOrEmpty(model.SourceNamespace) ? "Global" : model.SourceNamespace;
        var baseName = $"{ns}.{dtoName}";
        var safeName = baseName.GetSafeName();

        return $"{safeName}.g.cs";
    }

    private static string GetSimpleTypeName(string fullyQualifiedName)
    {
        // remove global:: prefix if present (for types in global namespace)
        var name = Shared.GeneratorUtilities.StripGlobalPrefix(fullyQualifiedName);

        var parts = name.Split('.');
        return parts[parts.Length - 1];
    }

    /// <summary>
    /// Filters members by exclusion lists, returning only members not in any exclusion set.
    /// </summary>
    private static ImmutableArray<FacetMember> FilterMembers(
        ImmutableArray<FacetMember> members,
        ImmutableArray<string> baseExclusions,
        HashSet<string>? additionalExclusions = null)
    {
        var exclusions = new HashSet<string>(baseExclusions, StringComparer.OrdinalIgnoreCase);

        if (additionalExclusions != null)
        {
            foreach (var item in additionalExclusions)
            {
                exclusions.Add(item);
            }
        }

        return members.Where(m => !exclusions.Contains(m.Name)).ToImmutableArray();
    }

    private static string GenerateDtoCode(GenerateDtosTargetModel model, string dtoName, ImmutableArray<FacetMember> members, string purpose)
    {
        var sb = new StringBuilder();
        var sourceTypeName = GetSimpleTypeName(model.SourceTypeName);
        var hasInitOnlyProperties = members.Any(m => m.IsInitOnly);
        var hasReadOnlyFields = members.Any(m => m.IsReadOnly);

        // Generate file structure
        GenerateDtoFileHeader(sb, model);
        GenerateDtoTypeDeclaration(sb, model, dtoName, sourceTypeName, purpose);

        // Generate members
        GenerateDtoMembers(sb, members);

        // Generate constructors if requested
        if (model.GenerateConstructors)
        {
            GenerateDtoConstructors(sb, model, dtoName, sourceTypeName, members, hasInitOnlyProperties, hasReadOnlyFields);
        }

        // Generate projection if requested
        if (model.GenerateProjections)
        {
            GenerateDtoProjection(sb, model, dtoName, sourceTypeName, members, hasInitOnlyProperties, hasReadOnlyFields);
        }

        // Generate ToSource method
        GenerateDtoToSource(sb, model, dtoName, sourceTypeName, members);

        // Generate deprecated BackTo method
        GenerateDtoBackTo(sb, model, dtoName, sourceTypeName);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateDtoFileHeader(StringBuilder sb, GenerateDtosTargetModel model)
    {
        GenerateFileHeader(sb);
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine();

        // Nullable must be enabled in generated code with a directive
        var hasNullableRefTypeMembers = model.Members.Any(m => !m.IsValueType && m.TypeName.EndsWith("?"));
        if (hasNullableRefTypeMembers)
        {
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(model.TargetNamespace))
        {
            sb.AppendLine($"namespace {model.TargetNamespace};");
            sb.AppendLine();
        }
    }

    private static void GenerateDtoTypeDeclaration(StringBuilder sb, GenerateDtosTargetModel model, string dtoName, string sourceTypeName, string purpose)
    {
        var keyword = model.OutputType switch
        {
            OutputType.Class => "class",
            OutputType.Record => "record",
            OutputType.RecordStruct => "record struct",
            OutputType.Struct => "struct",
            _ => "record"
        };

        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated {purpose} DTO for {sourceTypeName}.");
        sb.AppendLine($"/// </summary>");

        // Add [Facet] attribute to make it work with extension methods
        sb.AppendLine($"[Facet.Facet(typeof({model.SourceTypeName}))]");

        sb.AppendLine($"public {keyword} {dtoName}");
        sb.AppendLine("{");
    }

    private static void GenerateDtoMembers(StringBuilder sb, ImmutableArray<FacetMember> members)
    {
        foreach (var member in members)
        {
            if (member.Kind == FacetMemberKind.Property)
            {
                GenerateDtoProperty(sb, member);
            }
            else
            {
                GenerateDtoField(sb, member);
            }
        }
    }

    private static void GenerateDtoProperty(StringBuilder sb, FacetMember member)
    {
        var propDef = $"public {member.TypeName} {member.Name}";

        if (member.IsInitOnly)
        {
            propDef += " { get; init; }";
        }
        else
        {
            propDef += " { get; set; }";
        }

        if (member.IsRequired)
        {
            propDef = $"required {propDef}";
        }

        sb.AppendLine($"    {propDef}");
    }

    private static void GenerateDtoField(StringBuilder sb, FacetMember member)
    {
        var fieldDef = "public";
        if (member.IsReadOnly)
        {
            fieldDef += " readonly";
        }
        fieldDef += $" {member.TypeName} {member.Name}";

        // For readonly fields, we need to provide a default value since they can't be assigned in constructor
        if (member.IsReadOnly)
        {
            var defaultValue = GeneratorUtilities.GetDefaultValueForType(member.TypeName);
            fieldDef += $" = {defaultValue}";
        }

        fieldDef += ";";

        if (member.IsRequired && !member.IsReadOnly)
        {
            fieldDef = $"required {fieldDef}";
        }

        sb.AppendLine($"    {fieldDef}");
    }

    private static void GenerateDtoConstructors(StringBuilder sb, GenerateDtosTargetModel model, string dtoName, string sourceTypeName, ImmutableArray<FacetMember> members, bool hasInitOnlyProperties, bool hasReadOnlyFields)
    {
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the <see cref=\"{dtoName}\"/> class from the specified <see cref=\"{sourceTypeName}\"/>.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"source\">The source <see cref=\"{sourceTypeName}\"/> object to copy data from.</param>");

        var hasRequiredProperties = model.Members.Any(m => m.IsRequired);
        if (hasRequiredProperties)
        {
            sb.AppendLine("    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]");
        }

        sb.AppendLine($"    public {dtoName}({model.SourceTypeName} source)");
        sb.AppendLine("    {");

        // Only assign to non-init-only properties and non-readonly fields
        var assignableMembers = members.Where(x => !x.IsInitOnly && !x.IsReadOnly).ToArray();

        if (assignableMembers.Length > 0)
        {
            foreach (var member in assignableMembers)
            {
                sb.AppendLine($"        this.{member.Name} = source.{member.Name};");
            }
        }
        else
        {
            // If there are no assignable members, add a comment to explain
            sb.AppendLine("        // No assignable members to initialize from source");
            sb.AppendLine("        // (all members are either init-only properties or readonly fields with default values)");
        }

        sb.AppendLine("    }");

        // Add parameterless constructor
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the <see cref=\"{dtoName}\"/> class with default values.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public {dtoName}()");
        sb.AppendLine("    {");
        sb.AppendLine("    }");

        // Add static factory method for types with init-only properties or readonly fields
        if (hasInitOnlyProperties || hasReadOnlyFields)
        {
            GenerateDtoFromSourceFactory(sb, model, dtoName, sourceTypeName, members, hasReadOnlyFields);
        }
    }

    private static void GenerateDtoFromSourceFactory(StringBuilder sb, GenerateDtosTargetModel model, string dtoName, string sourceTypeName, ImmutableArray<FacetMember> members, bool hasReadOnlyFields)
    {
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Creates a new instance of <see cref=\"{dtoName}\"/> from the specified <see cref=\"{sourceTypeName}\"/> with init-only properties.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"source\">The source <see cref=\"{sourceTypeName}\"/> object to copy data from.</param>");
        sb.AppendLine($"    /// <returns>A new <see cref=\"{dtoName}\"/> instance with all properties initialized from the source.</returns>");

        if (hasReadOnlyFields)
        {
            sb.AppendLine($"    /// <remarks>");
            sb.AppendLine($"    /// Note: Readonly fields will use their default values and cannot be copied from the source.");
            sb.AppendLine($"    /// </remarks>");
        }

        sb.AppendLine($"    public static {dtoName} FromSource({model.SourceTypeName} source)");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {dtoName}");
        sb.AppendLine("        {");

        // Only include non-readonly fields in the object initializer
        var initializableMembers = members.Where(m => !m.IsReadOnly).ToArray();
        for (int i = 0; i < initializableMembers.Length; i++)
        {
            var member = initializableMembers[i];
            var comma = i == initializableMembers.Length - 1 ? "" : ",";
            sb.AppendLine($"            {member.Name} = source.{member.Name}{comma}");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
    }

    private static void GenerateDtoProjection(StringBuilder sb, GenerateDtosTargetModel model, string dtoName, string sourceTypeName, ImmutableArray<FacetMember> members, bool hasInitOnlyProperties, bool hasReadOnlyFields)
    {
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Gets the projection expression for converting <see cref=\"{sourceTypeName}\"/> to <see cref=\"{dtoName}\"/>.");
        sb.AppendLine($"    /// Use this for LINQ and Entity Framework query projections.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <value>An expression tree that can be used in LINQ queries for efficient database projections.</value>");
        sb.AppendLine($"    /// <example>");
        sb.AppendLine($"    /// <code>");
        sb.AppendLine($"    /// var dtos = context.{sourceTypeName}s");
        sb.AppendLine($"    ///     .Where(x => x.IsActive)");
        sb.AppendLine($"    ///     .Select({dtoName}.Projection)");
        sb.AppendLine($"    ///     .ToList();");
        sb.AppendLine($"    /// </code>");
        sb.AppendLine($"    /// </example>");
        sb.AppendLine($"    public static Expression<Func<{model.SourceTypeName}, {dtoName}>> Projection =>");

        if (hasInitOnlyProperties || hasReadOnlyFields)
        {
            sb.AppendLine($"        source => new {dtoName}");
            sb.AppendLine("        {");

            // Only include non-readonly fields in the object initializer for projections too
            var initializableMembers = members.Where(m => !m.IsReadOnly).ToArray();
            for (int i = 0; i < initializableMembers.Length; i++)
            {
                var member = initializableMembers[i];
                var comma = i == initializableMembers.Length - 1 ? "" : ",";
                sb.AppendLine($"            {member.Name} = source.{member.Name}{comma}");
            }

            sb.AppendLine("        };");
        }
        else
        {
            sb.AppendLine($"        source => new {dtoName}(source);");
        }
    }

    private static void GenerateDtoToSource(StringBuilder sb, GenerateDtosTargetModel model, string dtoName, string sourceTypeName, ImmutableArray<FacetMember> members)
    {
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Converts this instance of <see cref=\"{dtoName}\"/> back to an instance of the source type <see cref=\"{sourceTypeName}\"/>.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>An instance of <see cref=\"{sourceTypeName}\"/> with properties mapped from this DTO.</returns>");
        sb.AppendLine($"    public {model.SourceTypeName} ToSource()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {model.SourceTypeName}");
        sb.AppendLine("        {");

        // Map all members back to the source
        var toSourceMembers = members.Where(m => !m.IsReadOnly).ToArray();
        for (int i = 0; i < toSourceMembers.Length; i++)
        {
            var member = toSourceMembers[i];
            var comma = i == toSourceMembers.Length - 1 ? "" : ",";

            // Find the corresponding source member to check nullability
            var sourceMember = model.Members.FirstOrDefault(sm => sm.Name == member.Name);

            // If the DTO member is nullable but the source is not, use GetValueOrDefault()
            if (member.TypeName.EndsWith("?") && sourceMember != null && !sourceMember.TypeName.EndsWith("?"))
            {
                if (sourceMember.IsValueType)
                {
                    // For value types, use GetValueOrDefault()
                    sb.AppendLine($"            {member.Name} = this.{member.Name}.GetValueOrDefault(){comma}");
                }
                else
                {
                    // For reference types, use ?? operator with default
                    sb.AppendLine($"            {member.Name} = this.{member.Name} ?? default!{comma}");
                }
            }
            else
            {
                sb.AppendLine($"            {member.Name} = this.{member.Name}{comma}");
            }
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
    }

    private static void GenerateDtoBackTo(StringBuilder sb, GenerateDtosTargetModel model, string dtoName, string sourceTypeName)
    {
        // Generate deprecated BackTo method that calls ToSource
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Converts this instance of <see cref=\"{dtoName}\"/> back to an instance of the source type <see cref=\"{sourceTypeName}\"/>.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <returns>An instance of <see cref=\"{sourceTypeName}\"/> with properties mapped from this DTO.</returns>");
        sb.AppendLine("    [global::System.Obsolete(\"Use ToSource() instead. This method will be removed in a future version.\")]");
        sb.AppendLine($"    public {model.SourceTypeName} BackTo() => ToSource();");
    }

    private static string GeneratePatchDtoCode(GenerateDtosTargetModel model, string dtoName, ImmutableArray<FacetMember> members)
    {
        var sb = new StringBuilder();
        var sourceTypeName = GetSimpleTypeName(model.SourceTypeName);

        // Generate file header
        GenerateFileHeader(sb);
        sb.AppendLine("using System;");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(model.TargetNamespace))
        {
            sb.AppendLine($"namespace {model.TargetNamespace};");
            sb.AppendLine();
        }

        // Generate type declaration
        var keyword = model.OutputType switch
        {
            OutputType.Class => "class",
            OutputType.Record => "record",
            OutputType.RecordStruct => "record struct",
            OutputType.Struct => "struct",
            _ => "record"
        };

        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated Patch DTO for {sourceTypeName} that supports partial updates.");
        sb.AppendLine($"/// Uses Optional&lt;T&gt; to distinguish between unspecified values and explicit null values.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public {keyword} {dtoName}");
        sb.AppendLine("{");

        // Generate properties wrapped in Optional<T>
        foreach (var member in members)
        {
            if (member.Kind == FacetMemberKind.Property)
            {
                sb.AppendLine($"    /// <summary>Optional value for {member.Name}.</summary>");
                sb.AppendLine($"    public global::Facet.Optional<{member.TypeName}> {member.Name} {{ get; set; }}");
            }
        }

        // Generate ApplyTo method
        GeneratePatchApplyToMethod(sb, model, dtoName, sourceTypeName, members);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GeneratePatchApplyToMethod(StringBuilder sb, GenerateDtosTargetModel model, string dtoName, string sourceTypeName, ImmutableArray<FacetMember> members)
    {
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Applies the specified optional values from this patch DTO to the target <see cref=\"{sourceTypeName}\"/> instance.");
        sb.AppendLine($"    /// Only properties with HasValue = true will be updated.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"target\">The target <see cref=\"{sourceTypeName}\"/> object to update.</param>");
        sb.AppendLine($"    public void ApplyTo({model.SourceTypeName} target)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (target == null) throw new System.ArgumentNullException(nameof(target));");
        sb.AppendLine();

        // Generate property updates for non-readonly members
        var updatableMembers = members.Where(m => !m.IsReadOnly && m.Kind == FacetMemberKind.Property).ToArray();
        foreach (var member in updatableMembers)
        {
            sb.AppendLine($"        if ({member.Name}.HasValue)");
            sb.AppendLine($"            target.{member.Name} = {member.Name}.Value;");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
    }

    private static void GenerateFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//     This code was generated by the Facet GenerateDtos source generator.");
        sb.AppendLine("//     Changes to this file may cause incorrect behavior and will be lost if");
        sb.AppendLine("//     the code is regenerated.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
    }


    private static T GetNamedArg<T>(
        ImmutableArray<KeyValuePair<string, TypedConstant>> args,
        string name,
        T defaultValue)
    {
        var arg = args.FirstOrDefault(kv => kv.Key == name);
        if (arg.Key == null) return defaultValue;

        var value = arg.Value.Value;
        if (value == null) return defaultValue;

        if (typeof(T).IsEnum && value is int intValue)
        {
            return (T)Enum.ToObject(typeof(T), intValue);
        }

        if (value is T t) return t;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

}