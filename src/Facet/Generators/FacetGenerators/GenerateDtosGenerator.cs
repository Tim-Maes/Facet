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

    private static readonly DiagnosticDescriptor GeneratorErrorRule = new DiagnosticDescriptor(
        "FAC100",
        "GenerateDtos generator encountered an error",
        "Error generating DTOs for type '{0}': {1}",
        "Generator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The GenerateDtos source generator encountered an unexpected error while processing this type.");

    private static readonly DiagnosticDescriptor ConflictingOutputTypesRule = new DiagnosticDescriptor(
        "FAC101",
        "GenerateDtos OutputType combines multiple concrete output kinds",
        "GenerateDtos on '{0}' sets OutputType to '{1}', which combines multiple concrete output kinds; they would all generate the same type names and collide. Combine OutputType.Interface and the Partial modifier with at most one of Class, Record, Struct, or RecordStruct.",
        "Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Concrete output kinds (Class, Record, Struct, RecordStruct) all generate identically-named types, so at most one may be set. Only OutputType.Interface composes with a concrete kind, because its generated names carry an 'I' prefix; Partial is a modifier and combines with any kind.");

    private static readonly DiagnosticDescriptor PartialWithoutKindRule = new DiagnosticDescriptor(
        "FAC102",
        "GenerateDtos OutputType sets the Partial modifier without an output kind",
        "GenerateDtos on '{0}' sets OutputType to '{1}': Partial is a modifier and must be combined with at least one output kind (Class, Record, Struct, RecordStruct, or Interface).",
        "Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Partial flag only modifies how the requested kinds are emitted; on its own there is nothing to generate, which is more likely a mistake than an intentional no-op.");

    private static readonly HashSet<string> DefaultAuditFields = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "CreatedDate", "UpdatedDate", "CreatedAt", "UpdatedAt",
        "CreatedBy", "UpdatedBy", "CreatedById", "UpdatedById"
    };

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
                transform: static (ctx, token) => GetGenerateDtosModels(ctx, token, forceExcludeAuditFields: false))
            .Where(static m => m is not null)
            .SelectMany(static (models, _) => models!);

        var generateAuditableDtosTargets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateAuditableDtosAttributeName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, token) => GetGenerateDtosModels(ctx, token, forceExcludeAuditFields: true))
            .Where(static m => m is not null)
            .SelectMany(static (models, _) => models!);

        var allTargets = generateDtosTargets.Collect()
            .Combine(generateAuditableDtosTargets.Collect())
            .Select(static (combined, _) => combined.Left.Concat(combined.Right));

        context.RegisterSourceOutput(allTargets, (spc, models) =>
        {
            foreach (var model in models)
            {
                if (model != null)
                {
                    spc.CancellationToken.ThrowIfCancellationRequested();

                    if (model.Issue != OutputTypeIssue.None)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            model.Issue == OutputTypeIssue.PartialWithoutKind ? PartialWithoutKindRule : ConflictingOutputTypesRule,
                            Location.None,
                            GetSimpleTypeName(model.SourceTypeName),
                            model.OutputType.ToString()));
                        continue;
                    }

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

    private static IEnumerable<GenerateDtosTargetModel>? GetGenerateDtosModels(GeneratorAttributeSyntaxContext context, CancellationToken token, bool forceExcludeAuditFields)
    {
        token.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not INamedTypeSymbol sourceSymbol) return null;
        if (context.Attributes.Length == 0) return null;

        var models = new List<GenerateDtosTargetModel>();

        foreach (var attribute in context.Attributes)
        {
            token.ThrowIfCancellationRequested();

            var model = GetDtosModel(context, attribute, sourceSymbol, forceExcludeAuditFields, token);
            if (model == null) continue;

            // OutputType is a [Flags] value: kind bits (Class/Record/Struct/RecordStruct/
            // Interface) select what to emit, and the Partial modifier applies to every
            // selected kind. Expand into one model per kind (each carrying the modifier) so
            // downstream passes see a single kind. Sibling interface pairing below then
            // links Interface + concrete bits the same way separate attributes would.
            var outputTypes = DecomposeOutputTypes(model.OutputType);

            // Concrete kinds all generate the same type names, so combining more than one
            // can never compile; keep the un-expanded model as a marker and report FAC101
            // at generation time instead of emitting colliding sources.
            if (outputTypes.Count(t => GetKind(t) != OutputType.Interface) > 1)
            {
                models.Add(model.WithIssue(OutputTypeIssue.ConflictingConcreteKinds));
                continue;
            }

            // The Partial modifier with no kind to modify would silently generate nothing —
            // more likely a mistake than an intentional no-op, so fail loudly instead.
            if (outputTypes.Count == 0 && IsPartial(model.OutputType))
            {
                models.Add(model.WithIssue(OutputTypeIssue.PartialWithoutKind));
                continue;
            }
            if (outputTypes.Count == 1 && outputTypes[0] == model.OutputType)
            {
                models.Add(model);
                continue;
            }

            foreach (var outputType in outputTypes)
            {
                models.Add(model.WithOutputType(outputType));
            }
        }

        if (models.Count == 0) return null;

        var interfaceModels = models.Where(m => GetKind(m.OutputType) == OutputType.Interface).ToList();
        if (interfaceModels.Count == 0)
        {
            return models;
        }

        for (int i = 0; i < models.Count; i++)
        {
            var model = models[i];
            // Every concrete output kind (class, record, struct, record struct — partial or
            // not) can implement the sibling interface; skip interface models themselves and
            // invalid-mask markers (which generate nothing).
            if (GetKind(model.OutputType) == OutputType.Interface || model.Issue != OutputTypeIssue.None) continue;

            DtoTypes siblingMask = DtoTypes.None;
            foreach (var iface in interfaceModels)
            {
                if (iface.Prefix != model.Prefix) continue;
                if (iface.Suffix != model.Suffix) continue;
                if (iface.TargetNamespace != model.TargetNamespace) continue;

                siblingMask |= iface.Types & model.Types;
            }

            if (siblingMask == DtoTypes.None) continue;

            models[i] = new GenerateDtosTargetModel(
                model.SourceTypeName,
                model.SourceNamespace,
                model.TargetNamespace,
                model.Types,
                model.OutputType,
                model.Prefix,
                model.Suffix,
                model.IncludeFields,
                model.GenerateConstructors,
                model.GenerateProjections,
                model.ConvertEnumsTo,
                model.ExcludeProperties,
                model.Members,
                model.UseFullName,
                siblingMask);
        }

        return models;
    }

    private static GenerateDtosTargetModel? GetDtosModel(GeneratorAttributeSyntaxContext context, AttributeData attribute, INamedTypeSymbol sourceSymbol, bool forceExcludeAuditFields, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        try
        {
            var types = GetNamedArg(attribute.NamedArguments, "Types", DtoTypes.All);
            var outputType = GetNamedArg(attribute.NamedArguments, "OutputType", OutputType.Record);
            var targetNamespace = GetNamedArg<string?>(attribute.NamedArguments, "Namespace", null);
            var prefix = GetNamedArg<string?>(attribute.NamedArguments, "Prefix", null);
            var suffix = GetNamedArg<string?>(attribute.NamedArguments, "Suffix", null);
            var includeFields = GetNamedArg(attribute.NamedArguments, "IncludeFields", false);
            var generateConstructors = GetNamedArg(attribute.NamedArguments, "GenerateConstructors", true);
            var generateProjections = GetNamedArg(attribute.NamedArguments, "GenerateProjections", true);
            var useFullName = GetNamedArg(attribute.NamedArguments, "UseFullName", false);
            var convertEnumsTo = ExtractConvertEnumsTo(attribute.NamedArguments);
            
            var excludeAuditFields = forceExcludeAuditFields || GetNamedArg(attribute.NamedArguments, "ExcludeAuditFields", false);

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

            var excludeProperties = new HashSet<string>(userExcludeProperties, System.StringComparer.OrdinalIgnoreCase);

            if (excludeAuditFields)
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
                    members.Add(CreateGenerateDtoMember(
                        p.Name,
                        p.Type,
                        FacetMemberKind.Property,
                        isInitOnly,
                        isRequired,
                        false,
                        convertEnumsTo));
                    addedMembers.Add(p.Name);
                }
                else if (includeFields && member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public } f)
                {
                    bool isReadOnly = f.IsReadOnly;
                    members.Add(CreateGenerateDtoMember(
                        f.Name,
                        f.Type,
                        FacetMemberKind.Field,
                        false,
                        isRequired,
                        isReadOnly,
                        convertEnumsTo));
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
                convertEnumsTo,
                excludeProperties.ToImmutableArray(),
                members.ToImmutableArray(),
                useFullName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GenerateDtos error for {sourceSymbol.Name}: {ex.Message}");
            return null;
        }
    }

    private static void GenerateDtosForModel(SourceProductionContext context, GenerateDtosTargetModel model)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        var sourceTypeName = GetSimpleTypeName(model.SourceTypeName);
        
        // Keep a custom Prefix between I and the entity name.
        var interfaceLeader = GetKind(model.OutputType) == OutputType.Interface ? "I" : "";

        if ((model.Types & DtoTypes.Create) != 0)
        {
            var createMembers = FilterMembers(model.Members, model.ExcludeProperties, IdFieldPatterns);
            var createDtoName = interfaceLeader + BuildDtoName(sourceTypeName, "Create", "Request", model.Prefix, model.Suffix);
            var createCode = GenerateDtoCode(model, createDtoName, createMembers, "Create", DtoTypes.Create);
            context.AddSource($"{GenerateFileDtoFullName(model, createDtoName)}", SourceText.From(createCode, Encoding.UTF8));
        }

        if ((model.Types & DtoTypes.Update) != 0)
        {
            var updateMembers = FilterMembers(model.Members, model.ExcludeProperties);
            var updateDtoName = interfaceLeader + BuildDtoName(sourceTypeName, "Update", "Request", model.Prefix, model.Suffix);
            var updateCode = GenerateDtoCode(model, updateDtoName, updateMembers, "Update", DtoTypes.Update);
            context.AddSource($"{GenerateFileDtoFullName(model, updateDtoName)}", SourceText.From(updateCode, Encoding.UTF8));
        }

        if ((model.Types & DtoTypes.Upsert) != 0)
        {
            var upsertMembers = FilterMembers(model.Members, model.ExcludeProperties);
            var upsertDtoName = interfaceLeader + BuildDtoName(sourceTypeName, "Upsert", "Request", model.Prefix, model.Suffix);
            var upsertCode = GenerateDtoCode(model, upsertDtoName, upsertMembers, "Upsert", DtoTypes.Upsert);
            context.AddSource($"{GenerateFileDtoFullName(model, upsertDtoName)}", SourceText.From(upsertCode, Encoding.UTF8));
        }

        if ((model.Types & DtoTypes.Response) != 0)
        {
            var responseMembers = FilterMembers(model.Members, model.ExcludeProperties);
            var responseDtoName = interfaceLeader + BuildDtoName(sourceTypeName, "", "Response", model.Prefix, model.Suffix);
            var responseCode = GenerateDtoCode(model, responseDtoName, responseMembers, "Response", DtoTypes.Response);
            context.AddSource($"{GenerateFileDtoFullName(model, responseDtoName)}", SourceText.From(responseCode, Encoding.UTF8));
        }

        if ((model.Types & DtoTypes.Query) != 0)
        {
            var queryMembers = model.Members
                .Select(CreateQueryMember) 
                .ToImmutableArray();

            var queryDtoName = interfaceLeader + BuildDtoName(sourceTypeName, "", "Query", model.Prefix, model.Suffix);

            var queryCode = GenerateDtoCode(model, queryDtoName, queryMembers, "Query", DtoTypes.Query);
            context.AddSource($"{GenerateFileDtoFullName(model, queryDtoName)}", SourceText.From(queryCode, Encoding.UTF8));
        }

        // Patch DTO interfaces are skipped because ApplyTo needs a method body.
        if ((model.Types & DtoTypes.Patch) != 0 && GetKind(model.OutputType) != OutputType.Interface)
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

    private static string GenerateDtoCode(GenerateDtosTargetModel model, string dtoName, ImmutableArray<FacetMember> members, string purpose, DtoTypes dtoType)
    {
        var sb = new StringBuilder();
        var sourceTypeName = GetSimpleTypeName(model.SourceTypeName);
        var isInterface = GetKind(model.OutputType) == OutputType.Interface;
        var isPartial = IsPartial(model.OutputType);
        var hasInitOnlyProperties = members.Any(m => m.IsInitOnly);
        var hasReadOnlyFields = members.Any(m => m.IsReadOnly);

        GenerateDtoFileHeader(sb, model);
        GenerateDtoTypeDeclaration(sb, model, dtoName, sourceTypeName, purpose, dtoType);

        GenerateDtoMembers(sb, model, members);

        if (!isInterface)
        {
            if (model.GenerateConstructors)
            {
                GenerateDtoConstructors(sb, model, dtoName, sourceTypeName, members, hasInitOnlyProperties, hasReadOnlyFields);
            }

            // Partial output leaves mapping members to the user-defined partial half.
            if (!isPartial)
            {
                if (model.GenerateProjections)
                {
                    GenerateDtoProjection(sb, model, dtoName, sourceTypeName, members, hasInitOnlyProperties, hasReadOnlyFields);
                }

                GenerateDtoToSource(sb, model, dtoName, sourceTypeName, members);
                GenerateDtoBackTo(sb, model, dtoName, sourceTypeName);
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateDtoFileHeader(StringBuilder sb, GenerateDtosTargetModel model)
    {
        GenerateFileHeader(sb);
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Linq.Expressions;");
        sb.AppendLine();

        // Generated code needs #nullable enabled.
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

    private static void GenerateDtoTypeDeclaration(StringBuilder sb, GenerateDtosTargetModel model, string dtoName, string sourceTypeName, string purpose, DtoTypes dtoType)
    {
        var keyword = GetKind(model.OutputType) switch
        {
            OutputType.Class => "class",
            OutputType.Record => "record",
            OutputType.RecordStruct => "record struct",
            OutputType.Struct => "struct",
            OutputType.Interface => "interface",
            _ => "record"
        };

        if (IsPartial(model.OutputType))
        {
            keyword = "partial " + keyword;
        }

        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated {purpose} DTO contract for {sourceTypeName}.");
        sb.AppendLine($"/// </summary>");

        if (GetKind(model.OutputType) != OutputType.Interface)
        {
            if (model.ConvertEnumsTo != null)
            {
                var convertType = model.ConvertEnumsTo == "string" ? "string" : "int";
                sb.AppendLine($"[Facet.Facet(typeof({model.SourceTypeName}), ConvertEnumsTo = typeof({convertType}))]");
            }
            else
            {
                sb.AppendLine($"[Facet.Facet(typeof({model.SourceTypeName}))]");
            }
        }

        // Concrete outputs implement sibling interfaces like ICreateUserRequest —
        // records, structs, and record structs can all declare interface bases.
        var baseList = "";
        if (GetKind(model.OutputType) != OutputType.Interface && (model.SiblingInterfaceTypes & dtoType) != 0)
        {
            baseList = $" : I{dtoName}";
        }

        sb.AppendLine($"public {keyword} {dtoName}{baseList}");
        sb.AppendLine("{");
    }

    private static void GenerateDtoMembers(StringBuilder sb, GenerateDtosTargetModel model, ImmutableArray<FacetMember> members)
    {
        var isInterface = GetKind(model.OutputType) == OutputType.Interface;
        foreach (var member in members)
        {
            if (member.Kind == FacetMemberKind.Property)
            {
                GenerateDtoProperty(sb, member, isInterface);
            }
            else if (!isInterface)
            {
                GenerateDtoField(sb, member);
            }
        }
    }

    private static void GenerateDtoProperty(StringBuilder sb, FacetMember member, bool isInterface)
    {
        if (isInterface)
        {
            sb.AppendLine($"    {member.TypeName} {member.Name} {{ get; }}");
            return;
        }

        var propDef = $"public {member.TypeName} {member.Name}";

        if (member.IsInitOnly)
        {
            propDef += " { get; init; }";
        }
        else
        {
            propDef += " { get; set; }";
        }

        // Suppress CS8618 for generated non-nullable refs.
        if (!member.IsValueType && !member.IsRequired && !NullabilityAnalyzer.IsNullableTypeName(member.TypeName))
        {
            propDef += " = default!;";
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

        if (member.IsReadOnly)
        {
            var defaultValue = GeneratorUtilities.GetDefaultValueForType(member.TypeName);
            fieldDef += $" = {defaultValue}";
        }
        else if (!member.IsValueType && !member.IsRequired && !NullabilityAnalyzer.IsNullableTypeName(member.TypeName))
        {
            // Suppress CS8618 for generated non-nullable refs.
            fieldDef += " = default!";
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

        var assignableMembers = members.Where(x => !x.IsInitOnly && !x.IsReadOnly).ToArray();

        if (assignableMembers.Length > 0)
        {
            foreach (var member in assignableMembers)
            {
                var sourceExpression = $"source.{member.Name}";
                var mappedExpression = ConvertSourceToDtoExpression(sourceExpression, member, forProjection: false);
                sb.AppendLine($"        this.{member.Name} = {mappedExpression};");
            }
        }
        else
        {
            sb.AppendLine("        // No assignable members to initialize from source");
            sb.AppendLine("        // (all members are either init-only properties or readonly fields with default values)");
        }

        sb.AppendLine("    }");

        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Initializes a new instance of the <see cref=\"{dtoName}\"/> class with default values.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public {dtoName}()");
        sb.AppendLine("    {");
        sb.AppendLine("    }");

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

        var initializableMembers = members.Where(m => !m.IsReadOnly).ToArray();
        for (int i = 0; i < initializableMembers.Length; i++)
        {
            var member = initializableMembers[i];
            var comma = i == initializableMembers.Length - 1 ? "" : ",";
            var sourceExpression = $"source.{member.Name}";
            var mappedExpression = ConvertSourceToDtoExpression(sourceExpression, member, forProjection: false);
            sb.AppendLine($"            {member.Name} = {mappedExpression}{comma}");
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

            var initializableMembers = members.Where(m => !m.IsReadOnly).ToArray();
            for (int i = 0; i < initializableMembers.Length; i++)
            {
                var member = initializableMembers[i];
                var comma = i == initializableMembers.Length - 1 ? "" : ",";
                var sourceExpression = $"source.{member.Name}";
                var mappedExpression = ConvertSourceToDtoExpression(sourceExpression, member, forProjection: true);
                sb.AppendLine($"            {member.Name} = {mappedExpression}{comma}");
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

        var toSourceMembers = members.Where(m => !m.IsReadOnly).ToArray();
        for (int i = 0; i < toSourceMembers.Length; i++)
        {
            var member = toSourceMembers[i];
            var comma = i == toSourceMembers.Length - 1 ? "" : ",";

            var sourceMember = model.Members.FirstOrDefault(sm => sm.Name == member.Name);

            if (member.IsEnumConversion && member.OriginalEnumTypeName != null)
            {
                var convertedExpression = ConvertDtoToSourceExpression(member);
                sb.AppendLine($"            {member.Name} = {convertedExpression}{comma}");
                continue;
            }

            if (member.TypeName.EndsWith("?") && sourceMember != null && !sourceMember.TypeName.EndsWith("?"))
            {
                if (sourceMember.IsValueType)
                {
                    sb.AppendLine($"            {member.Name} = this.{member.Name}.GetValueOrDefault(){comma}");
                }
                else
                {
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

        GenerateFileHeader(sb);
        sb.AppendLine("using System;");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(model.TargetNamespace))
        {
            sb.AppendLine($"namespace {model.TargetNamespace};");
            sb.AppendLine();
        }

        var keyword = GetKind(model.OutputType) switch
        {
            OutputType.Class => "class",
            OutputType.Record => "record",
            OutputType.RecordStruct => "record struct",
            OutputType.Struct => "struct",
            _ => "record"
        };

        if (IsPartial(model.OutputType))
        {
            keyword = "partial " + keyword;
        }

        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated Patch DTO for {sourceTypeName} that supports partial updates.");
        sb.AppendLine($"/// Uses Optional&lt;T&gt; to distinguish between unspecified values and explicit null values.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public {keyword} {dtoName}");
        sb.AppendLine("{");

        foreach (var member in members)
        {
            if (member.Kind == FacetMemberKind.Property)
            {
                sb.AppendLine($"    /// <summary>Optional value for {member.Name}.</summary>");
                sb.AppendLine($"    public global::Facet.Optional<{member.TypeName}> {member.Name} {{ get; set; }}");
            }
        }

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

    private static FacetMember CreateGenerateDtoMember(
        string name,
        ITypeSymbol typeSymbol,
        FacetMemberKind kind,
        bool isInitOnly,
        bool isRequired,
        bool isReadOnly,
        string? convertEnumsTo)
    {
        var isCollection = GeneratorUtilities.TryGetCollectionElementType(typeSymbol, out var elementType, out var collectionWrapper);
        var originalTypeName = GeneratorUtilities.GetTypeNameWithNullability(typeSymbol);
        var typeName = originalTypeName;
        bool isEnumConversion = false;
        string? originalEnumTypeName = null;

        if (IsSupportedEnumConversion(convertEnumsTo))
        {
            if (isCollection && elementType != null)
            {
                var underlyingElement = elementType;
                bool elementIsNullable = false;
                if (underlyingElement is INamedTypeSymbol namedElement &&
                    namedElement.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    underlyingElement = namedElement.TypeArguments[0];
                    elementIsNullable = true;
                }

                if (underlyingElement.TypeKind == TypeKind.Enum && collectionWrapper != null)
                {
                    isEnumConversion = true;
                    originalEnumTypeName = underlyingElement.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var convertedElementType = GetConvertedEnumType(convertEnumsTo!, elementIsNullable);
                    typeName = GeneratorUtilities.WrapInCollectionType(convertedElementType, collectionWrapper);
                    if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
                    {
                        typeName += "?";
                    }
                }
            }
            else
            {
                var underlyingType = typeSymbol;
                bool isNullableEnum = false;
                if (underlyingType is INamedTypeSymbol namedType &&
                    namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    underlyingType = namedType.TypeArguments[0];
                    isNullableEnum = true;
                }

                if (underlyingType.TypeKind == TypeKind.Enum)
                {
                    isEnumConversion = true;
                    originalEnumTypeName = underlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    typeName = GetConvertedEnumType(convertEnumsTo!, isNullableEnum);
                }
            }
        }

        return new FacetMember(
            name,
            typeName,
            kind,
            typeSymbol.IsValueType,
            isInitOnly,
            isRequired,
            isReadOnly,
            null,
            false,
            null,
            null,
            isCollection,
            collectionWrapper,
            collectionWrapper,
            originalTypeName,
            null,
            false,
            true,
            name,
            false,
            null,
            null,
            true,
            null,
            null,
            isEnumConversion,
            originalEnumTypeName,
            false,
            false,
            false);
    }

    private static string? ExtractConvertEnumsTo(ImmutableArray<KeyValuePair<string, TypedConstant>> args)
    {
        var arg = args.FirstOrDefault(kvp => kvp.Key == "ConvertEnumsTo");
        if (arg.Value.Value is INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.SpecialType switch
            {
                SpecialType.System_String => "string",
                SpecialType.System_Int32 => "int",
                _ => null
            };
        }

        return null;
    }

    private static bool IsSupportedEnumConversion(string? convertEnumsTo)
        => convertEnumsTo is "string" or "int";

    private static string GetConvertedEnumType(string convertEnumsTo, bool isNullable)
    {
        if (convertEnumsTo == "string")
        {
            return isNullable ? "string?" : "string";
        }

        return isNullable ? "int?" : "int";
    }

    private static FacetMember CreateQueryMember(FacetMember member)
    {
        return new FacetMember(
            member.Name,
            GeneratorUtilities.MakeNullable(member.TypeName),
            member.Kind,
            member.IsValueType,
            member.IsInitOnly,
            false,
            member.IsReadOnly,
            null,
            false,
            null,
            null,
            member.IsCollection,
            member.CollectionWrapper,
            member.SourceCollectionWrapper,
            member.SourceMemberTypeName,
            null,
            false,
            true,
            member.SourcePropertyName,
            false,
            null,
            null,
            true,
            null,
            null,
            member.IsEnumConversion,
            member.OriginalEnumTypeName,
            false,
            false,
            false);
    }

    private static string ConvertSourceToDtoExpression(string sourceExpression, FacetMember member, bool forProjection)
    {
        if (!member.IsEnumConversion || member.OriginalEnumTypeName == null)
        {
            return sourceExpression;
        }

        if (member.IsCollection)
        {
            return ConvertSourceEnumCollectionToDto(sourceExpression, member);
        }

        bool isNullableEnum = member.SourceMemberTypeName?.EndsWith("?") ?? false;
        var targetType = member.TypeName.TrimEnd('?');
        if (targetType == "string")
        {
            if (isNullableEnum)
            {
                return forProjection
                    ? $"{sourceExpression} != null ? {sourceExpression}.Value.ToString() : null"
                    : $"{sourceExpression}?.ToString()";
            }

            return $"{sourceExpression}.ToString()";
        }

        if (targetType == "int")
        {
            return isNullableEnum ? $"(int?){sourceExpression}" : $"(int){sourceExpression}";
        }

        return sourceExpression;
    }

    private static string ConvertSourceEnumCollectionToDto(string sourceExpression, FacetMember member)
    {
        var targetElementType = GetCollectionElementType(member.TypeName).TrimEnd('?');
        string projection = targetElementType switch
        {
            "string" => $"{sourceExpression}.Select(x => x.ToString())",
            "int" => $"{sourceExpression}.Select(x => (int)x)",
            _ => sourceExpression
        };

        if (projection == sourceExpression)
        {
            return sourceExpression;
        }

        var wrappedProjection = WrapCollectionProjection(projection, member.CollectionWrapper, GetCollectionElementType(member.TypeName));
        bool isNullableCollection = member.TypeName.EndsWith("?");
        return isNullableCollection ? $"{sourceExpression} != null ? {wrappedProjection} : null" : wrappedProjection;
    }

    private static string ConvertDtoToSourceExpression(FacetMember member)
    {
        if (member.IsCollection)
        {
            return ConvertDtoCollectionToEnumSource(member);
        }

        var enumTypeName = member.OriginalEnumTypeName!;
        bool dtoTypeIsNullable = member.TypeName.EndsWith("?");
        bool sourceTypeIsNullable = member.SourceMemberTypeName?.EndsWith("?") ?? false;
        var targetType = member.TypeName.TrimEnd('?');

        if (targetType == "string")
        {
            if (dtoTypeIsNullable && sourceTypeIsNullable)
                return $"this.{member.Name} != null ? ({enumTypeName}?)System.Enum.Parse<{enumTypeName}>(this.{member.Name}) : null";
            if (dtoTypeIsNullable)
                return $"System.Enum.Parse<{enumTypeName}>(this.{member.Name}!)";
            return $"System.Enum.Parse<{enumTypeName}>(this.{member.Name})";
        }

        if (targetType == "int")
        {
            if (dtoTypeIsNullable && sourceTypeIsNullable)
                return $"this.{member.Name} != null ? ({enumTypeName}?)({enumTypeName})this.{member.Name}.Value : null";
            if (dtoTypeIsNullable)
                return $"({enumTypeName})(this.{member.Name} ?? default)";
            return $"({enumTypeName})this.{member.Name}";
        }

        return $"this.{member.Name}";
    }

    private static string ConvertDtoCollectionToEnumSource(FacetMember member)
    {
        var enumTypeName = member.OriginalEnumTypeName!;
        var targetElementType = GetCollectionElementType(member.TypeName).TrimEnd('?');

        string projection = targetElementType switch
        {
            "string" => $"this.{member.Name}.Select(x => System.Enum.Parse<{enumTypeName}>(x))",
            "int" => $"this.{member.Name}.Select(x => ({enumTypeName})x)",
            _ => $"this.{member.Name}"
        };

        if (projection == $"this.{member.Name}")
        {
            return projection;
        }

        var sourceWrapper = member.SourceCollectionWrapper ?? member.CollectionWrapper;
        var wrappedProjection = WrapCollectionProjection(projection, sourceWrapper, enumTypeName);
        bool dtoCollectionIsNullable = member.TypeName.EndsWith("?");
        bool sourceCollectionIsNullable = member.SourceMemberTypeName?.EndsWith("?") ?? false;

        if (dtoCollectionIsNullable && sourceCollectionIsNullable)
            return $"this.{member.Name} != null ? {wrappedProjection} : null";
        if (dtoCollectionIsNullable)
            return $"{wrappedProjection}!";

        return wrappedProjection;
    }

    private static string GetCollectionElementType(string collectionTypeName)
    {
        var typeName = collectionTypeName.TrimEnd('?');
        if (!typeName.Contains("<") || !typeName.Contains(">"))
        {
            return typeName.EndsWith("[]", StringComparison.Ordinal)
                ? typeName.Substring(0, typeName.Length - 2)
                : typeName;
        }

        var start = typeName.IndexOf('<') + 1;
        var end = typeName.LastIndexOf('>');
        return typeName.Substring(start, end - start).Trim();
    }

    private static string WrapCollectionProjection(string projection, string? collectionWrapper, string elementTypeName)
    {
        return collectionWrapper switch
        {
            "array" => $"{projection}.ToArray()",
            "IEnumerable" => projection,
            "Collection" => $"new global::System.Collections.ObjectModel.Collection<{elementTypeName}>({projection}.ToList())",
            "ImmutableArray" => $"{projection}.ToImmutableArray()",
            "ImmutableList" => $"{projection}.ToImmutableList()",
            "ImmutableHashSet" => $"{projection}.ToImmutableHashSet()",
            "ImmutableSortedSet" => $"{projection}.ToImmutableSortedSet()",
            "ImmutableQueue" => $"global::System.Collections.Immutable.ImmutableQueue.CreateRange({projection})",
            "ImmutableStack" => $"global::System.Collections.Immutable.ImmutableStack.CreateRange({projection})",
            "IImmutableList" => $"{projection}.ToImmutableList()",
            "IImmutableSet" => $"{projection}.ToImmutableHashSet()",
            "IImmutableQueue" => $"global::System.Collections.Immutable.ImmutableQueue.CreateRange({projection})",
            "IImmutableStack" => $"global::System.Collections.Immutable.ImmutableStack.CreateRange({projection})",
            _ => $"{projection}.ToList()"
        };
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

    private static readonly OutputType[] OutputKinds =
    {
        OutputType.Class,
        OutputType.Record,
        OutputType.Struct,
        OutputType.RecordStruct,
        OutputType.Interface,
    };

    /// <summary>The kind bits of an <see cref="OutputType"/>, with the Partial modifier stripped.</summary>
    private static OutputType GetKind(OutputType value) => value & ~OutputType.Partial;

    /// <summary>Whether the <see cref="OutputType.Partial"/> modifier is set.</summary>
    private static bool IsPartial(OutputType value) => (value & OutputType.Partial) != 0;

    /// <summary>
    /// Splits a [Flags] <see cref="OutputType"/> value into its individual output kinds,
    /// re-applying the <see cref="OutputType.Partial"/> modifier to each (so PartialClass —
    /// the Class | Partial alias — decomposes to itself, and Record | Interface | Partial
    /// decomposes to a partial record plus a partial interface).
    /// <see cref="OutputType.None"/> (or a kindless value) yields an empty list.
    /// </summary>
    private static List<OutputType> DecomposeOutputTypes(OutputType value)
    {
        var result = new List<OutputType>();
        var modifier = value & OutputType.Partial;

        foreach (var kind in OutputKinds)
        {
            if ((value & kind) != 0)
            {
                result.Add(kind | modifier);
            }
        }

        return result;
    }
}
