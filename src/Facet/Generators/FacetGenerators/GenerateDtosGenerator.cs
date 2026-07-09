using Facet.Generators.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SGF;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace Facet.Generators;

// SGF (SourceGenerator.Foundations) hoists this class behind a generated internal
// GenerateDtosGeneratorHoist that carries [Generator]: callbacks get exception isolation and
// a logger, and assembly-embedded dependencies (System.Text.Json for the EF model manifest)
// are resolved before this code runs — analyzers cannot otherwise carry NuGet dependencies
// into compiler hosts.
[IncrementalGenerator]
public sealed class GenerateDtosGenerator : IncrementalGenerator
{
    public GenerateDtosGenerator() : base(nameof(GenerateDtosGenerator))
    {
    }

    private const string GenerateDtosAttributeName = "Facet.GenerateDtosAttribute";
    
    private const string GenerateAuditableDtosAttributeName = "Facet.GenerateAuditableDtosAttribute";

    private const string GenerateDtosForAttributeName = "Facet.GenerateDtosForAttribute";

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

    private static readonly DiagnosticDescriptor ManifestMalformedRule = new DiagnosticDescriptor(
        "FAC103",
        "EF model manifest could not be read",
        "EF model manifest '{0}' could not be read: {1}. The file is ignored in full; regenerate it (dotnet ef migrations add/remove) or remove it from AdditionalFiles.",
        "Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A committed *.facetmodel.json file wired up as an AdditionalFile is not readable as a manifest. It is ignored in full, and any ExcludeNavigationProperties type it should have covered then surfaces as FAC105 — the failure is never silent.");

    private static readonly DiagnosticDescriptor ManifestVersionRule = new DiagnosticDescriptor(
        "FAC104",
        "EF model manifest version is not supported",
        "EF model manifest '{0}' declares {1}. The file is ignored in full; align the Facet and Facet.Extensions.EFCore package versions and regenerate the manifest.",
        "Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The manifest was written by a Facet.Extensions.EFCore version whose format this generator does not read — a package version mismatch. It is ignored in full; any ExcludeNavigationProperties type it should have covered then surfaces as FAC105.");

    private static readonly DiagnosticDescriptor TypeNotInManifestRule = new DiagnosticDescriptor(
        "FAC105",
        "GenerateDtos source type is not in the EF model manifest",
        "'{0}' sets ExcludeNavigationProperties, which requires an EF model manifest entry for the type, but none was found. Wire up Facet.Extensions.EFCore's design-time services and add the manifest to AdditionalFiles — note that an AdditionalFiles glob matching nothing is silently empty, so double-check the path — then run 'dotnet ef migrations add' to generate it.",
        "Generator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ExcludeNavigationProperties is driven entirely by the EF model manifest — there is no heuristic fallback. A source type with no manifest entry (because no manifest was supplied at all, or because this type is absent from the manifests present) cannot be shaped and is a hard error. If the type is not an EF entity, list its navigation-like properties in ExcludeProperties instead of using ExcludeNavigationProperties.");

    private static readonly DiagnosticDescriptor PropertyNotInManifestRule = new DiagnosticDescriptor(
        "FAC106",
        "Property is unknown to the EF model manifest",
        "Property '{0}' on '{1}' does not appear in the EF model manifest entry for the type — the manifest most likely predates the property, and it will be dropped from generated DTOs. Regenerate the manifest (dotnet ef migrations add/remove), or mark the property [NotMapped]/Ignore() if the model genuinely does not map it.",
        "Generator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The manifest records every member the model has an opinion on (mapped, navigation, owned, skip navigation, ignored, service). A settable property outside that set is unknown to the model — almost always one added after the manifest was last generated, which would otherwise silently vanish from DTOs. Escalate with WarningsAsErrors for strict builds.");

    private static readonly HashSet<string> DefaultAuditFields = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "CreatedDate", "UpdatedDate", "CreatedAt", "UpdatedAt",
        "CreatedBy", "UpdatedBy", "CreatedById", "UpdatedById"
    };

    private static readonly HashSet<string> IdFieldPatterns = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "Id"
    };

    public override void OnInitialize(SgfInitializationContext context)
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

        var generateDtosForTargets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateDtosForAttributeName,
                predicate: static (node, _) => node is CompilationUnitSyntax,
                transform: static (ctx, token) => GetGenerateDtosForModels(ctx, token))
            .Where(static m => m is not null)
            .SelectMany(static (models, _) => models!);

        // EF model manifests (*.facetmodel.json, written beside the model snapshot by
        // Facet.Extensions.EFCore on every migrations add/remove) drive
        // ExcludeNavigationProperties: they carry the EF model's own navigation designation.
        // AdditionalFiles are invisible to the syntax transform, so the member set is resolved
        // here, against the manifest.
        var efModelManifest = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(EfModelManifest.FileExtension, StringComparison.OrdinalIgnoreCase))
            .Select(static (file, token) => (file.Path, Text: file.GetText(token)?.ToString() ?? string.Empty))
            .Collect()
            .Select((files, _) =>
            {
                var manifest = EfModelManifest.Parse(files);
                if (files.Length > 0)
                {
                    Logger.Debug($"EF model manifest: {manifest.EntityCount} entity types from {files.Length} file(s), {manifest.Issues.Length} rejected");
                }

                return manifest;
            });

        var allTargets = generateDtosTargets.Collect()
            .Combine(generateAuditableDtosTargets.Collect())
            .Combine(generateDtosForTargets.Collect())
            .Select(static (combined, _) => combined.Left.Left.Concat(combined.Left.Right).Concat(combined.Right))
            .Combine(efModelManifest);

        context.RegisterSourceOutput(allTargets, (spc, pair) =>
        {
            var (models, manifest) = pair;
            var modelList = models.Where(m => m != null).Cast<GenerateDtosTargetModel>().ToList();

            // The Optional<T> JSON converter is generated (not shipped in Facet.Attributes)
            // so Facet adds no System.Text.Json package dependency; emit it once per
            // compilation when any Patch DTO needs it.
            bool NeedsPatchWireSupport(GenerateDtosTargetModel m) =>
                m.Issue == OutputTypeIssue.None
                && (m.Types & DtoTypes.Patch) != 0
                && GetKind(m.OutputType) != OutputType.Interface;

            if (modelList.Any(m => NeedsPatchWireSupport(m) && m.SupportsSystemTextJson))
            {
                spc.AddSource("FacetOptionalJsonSupport.g.cs", SourceText.From(OptionalJsonSupportSource, Encoding.UTF8));
            }

            if (modelList.Any(m => NeedsPatchWireSupport(m) && m.SupportsNewtonsoftJson))
            {
                spc.AddSource("FacetOptionalNewtonsoftJsonSupport.g.cs", SourceText.From(OptionalNewtonsoftJsonSupportSource, Encoding.UTF8));
            }

            // A rejected manifest file is a broken build input: report it once per
            // compilation. Types it should have covered then surface as FAC105.
            foreach (var issue in manifest.Issues)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    issue.Kind == ManifestIssueKind.UnsupportedVersion ? ManifestVersionRule : ManifestMalformedRule,
                    Location.None,
                    issue.FilePath,
                    issue.Detail));
            }

            // Attribute expansion yields several models per attribute (one per output kind),
            // so manifest-coverage diagnostics deduplicate per source type / property.
            var reportedCoverage = new HashSet<string>(StringComparer.Ordinal);

            foreach (var pendingModel in modelList)
            {
                if (pendingModel != null)
                {
                    spc.CancellationToken.ThrowIfCancellationRequested();

                    var model = ResolveNavigationExclusions(spc, pendingModel, manifest, reportedCoverage);

                    if (model.Issue != OutputTypeIssue.None)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            model.Issue == OutputTypeIssue.PartialWithoutKind ? PartialWithoutKindRule : ConflictingOutputTypesRule,
                            model.AttributeLocation?.ToLocation() ?? Location.None,
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
                        Logger.Error(ex, $"Error generating DTOs for '{GetSimpleTypeName(model.SourceTypeName)}'");
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

        return BuildModels(context.Attributes, _ => sourceSymbol, context.SemanticModel.Compilation, forceExcludeAuditFields, token);
    }

    /// <summary>
    /// Assembly-level entry point: <c>[assembly: GenerateDtosFor(typeof(Entity), ...)]</c>
    /// generates into the DECLARING assembly, with the source entity resolved from the
    /// attribute's constructor argument — typically a type from a referenced assembly, so
    /// contract DTOs can live downstream of the domain project.
    /// </summary>
    private static IEnumerable<GenerateDtosTargetModel>? GetGenerateDtosForModels(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not IAssemblySymbol) return null;

        return BuildModels(
            context.Attributes,
            static attr => attr.ConstructorArguments.Length == 1
                ? attr.ConstructorArguments[0].Value as INamedTypeSymbol
                : null,
            context.SemanticModel.Compilation,
            forceExcludeAuditFields: false,
            token);
    }

    private static IEnumerable<GenerateDtosTargetModel>? BuildModels(
        ImmutableArray<AttributeData> attributes,
        Func<AttributeData, INamedTypeSymbol?> sourceSelector,
        Compilation compilation,
        bool forceExcludeAuditFields,
        CancellationToken token)
    {
        if (attributes.Length == 0) return null;

        var models = new List<GenerateDtosTargetModel>();

        foreach (var attribute in attributes)
        {
            token.ThrowIfCancellationRequested();

            if (sourceSelector(attribute) is not INamedTypeSymbol sourceSymbol) continue;

            var model = GetDtosModel(attribute, sourceSymbol, compilation, forceExcludeAuditFields, token);
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
                if (iface.SourceTypeName != model.SourceTypeName) continue;
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
                model.ExcludeNavigationProperties,
                model.IncludeProperties,
                model.SettableProperties,
                model.AttributeLocation,
                siblingMask,
                model.Issue,
                model.SupportsSystemTextJson,
                model.SupportsNewtonsoftJson);
        }

        return models;
    }

    private static GenerateDtosTargetModel? GetDtosModel(AttributeData attribute, INamedTypeSymbol sourceSymbol, Compilation compilation, bool forceExcludeAuditFields, CancellationToken token)
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
            var supportsSystemTextJson = compilation
                .GetTypeByMetadataName("System.Text.Json.Serialization.JsonConverterAttribute") is not null;
            var supportsNewtonsoftJson = compilation
                .GetTypeByMetadataName("Newtonsoft.Json.JsonConverterAttribute") is not null;
            var excludeNavigationProperties = GetNamedArg(attribute.NamedArguments, "ExcludeNavigationProperties", false);

            var includeProperties = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var includePropertiesArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "IncludeProperties");
            if (includePropertiesArg.Value.Kind == TypedConstantKind.Array && !includePropertiesArg.Value.IsNull)
            {
                foreach (var v in includePropertiesArg.Value.Values)
                {
                    if (v.Value?.ToString() is { } name) includeProperties.Add(name);
                }
            }

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

            // IncludeProperties is the escape hatch: names listed there survive every
            // automatic and explicit exclusion (the Create-DTO Id convention excepted).
            excludeProperties.ExceptWith(includeProperties);

            var members = new List<FacetMember>();
            var addedMembers = new HashSet<string>();

            var allMembersWithModifiers = GeneratorUtilities.GetAllMembersWithModifiers(sourceSymbol);

            // Properties EF could plausibly map (settable, or get-only collections), for the
            // manifest completeness check (FAC106). Computed get-only properties are excluded:
            // the model never maps them, so their absence from a manifest means nothing.
            var settableProperties = new List<string>();

            foreach (var (member, isInitOnly, isRequired) in allMembersWithModifiers)
            {
                token.ThrowIfCancellationRequested();
                if (excludeProperties.Contains(member.Name)) continue;
                if (addedMembers.Contains(member.Name)) continue;

                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } p)
                {
                    if (excludeNavigationProperties
                        && (p.SetMethod != null || GeneratorUtilities.TryGetCollectionElementType(p.Type, out _, out _)))
                    {
                        settableProperties.Add(p.Name);
                    }

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
                useFullName,
                excludeNavigationProperties,
                includeProperties.ToImmutableArray(),
                settableProperties.ToImmutableArray(),
                SourceLocationInfo.FromAttribute(attribute),
                supportsSystemTextJson: supportsSystemTextJson,
                supportsNewtonsoftJson: supportsNewtonsoftJson);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GenerateDtos error for {sourceSymbol.Name}: {ex.Message}");
            return null;
        }
    }

    private static void GenerateDtosForModel(SgfSourceProductionContext context, GenerateDtosTargetModel model)
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
                // RFC 7396 (JSON Merge Patch) wire semantics: an absent property never
                // reaches a converter, so the Optional stays unspecified; an explicit
                // null becomes a specified null (or a 400 for non-nullable value types).
                // Unspecified values are skipped when serializing. Both serializers honor
                // per-property converter attributes, so no startup registration is needed.
                if (model.SupportsSystemTextJson)
                {
                    sb.AppendLine($"    [global::System.Text.Json.Serialization.JsonConverter(typeof(global::Facet.Generated.OptionalJsonConverterFactory))]");
                    sb.AppendLine($"    [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]");
                }
                if (model.SupportsNewtonsoftJson)
                {
                    sb.AppendLine($"    [global::Newtonsoft.Json.JsonConverter(typeof(global::Facet.Generated.OptionalNewtonsoftJsonConverter))]");
                    sb.AppendLine($"    [global::Newtonsoft.Json.JsonProperty(DefaultValueHandling = global::Newtonsoft.Json.DefaultValueHandling.Ignore)]");
                }
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
    /// Generated System.Text.Json support for <c>Facet.Optional&lt;T&gt;</c> giving Patch DTOs
    /// RFC 7396 (JSON Merge Patch) wire semantics. Generated into the consuming assembly —
    /// like strongly-typed-ID libraries do for their converters — so Facet.Attributes takes
    /// no System.Text.Json package dependency.
    /// </summary>
    private const string OptionalJsonSupportSource = """
// <auto-generated>
//     This code was generated by the Facet GenerateDtos source generator.
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>

#nullable enable

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Facet.Generated;

/// <summary>
/// Creates converters giving <see cref="global::Facet.Optional{T}"/> JSON Merge Patch
/// (RFC 7396) semantics: an absent property stays unspecified, an explicit null becomes
/// a specified null (or a JsonException — surfaced by ASP.NET Core as HTTP 400 — for
/// non-nullable value types), and a value becomes a specified value.
/// </summary>
internal sealed class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType
           && typeToConvert.GetGenericTypeDefinition() == typeof(global::Facet.Optional<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(
            typeof(OptionalJsonConverter<>).MakeGenericType(valueType))!;
    }
}

internal sealed class OptionalJsonConverter<T> : JsonConverter<global::Facet.Optional<T>>
{
    // A null token must reach Read so it can become a *specified* null; without this,
    // System.Text.Json would reject null for the non-nullable Optional<T> struct itself.
    public override bool HandleNull => true;

    public override global::Facet.Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // An absent property never invokes a converter — the field keeps
        // default(Optional<T>), i.e. unspecified. Reaching this method means the
        // property was present, so the result is always specified. Null into a
        // non-nullable value type throws JsonException here.
        var value = JsonSerializer.Deserialize<T>(ref reader, options);
        return new global::Facet.Optional<T>(value!);
    }

    public override void Write(Utf8JsonWriter writer, global::Facet.Optional<T> value, JsonSerializerOptions options)
    {
        // Unspecified values are normally skipped via [JsonIgnore(WhenWritingDefault)]
        // on the generated properties; if one is serialized directly anyway, null is
        // the closest wire representation.
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value.Value, options);
    }
}
""";

    /// <summary>
    /// Generated Newtonsoft.Json support for <c>Facet.Optional&lt;T&gt;</c> — the Json.NET
    /// counterpart of <see cref="OptionalJsonSupportSource"/>, for apps whose MVC pipeline
    /// binds bodies through Json.NET (AddNewtonsoftJson). Json.NET honors per-property
    /// [JsonConverter] attributes, so no serializer registration is needed.
    /// </summary>
    private const string OptionalNewtonsoftJsonSupportSource = """
// <auto-generated>
//     This code was generated by the Facet GenerateDtos source generator.
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>

#nullable enable

using System;
using Newtonsoft.Json;

namespace Facet.Generated;

/// <summary>
/// Gives <see cref="global::Facet.Optional{T}"/> JSON Merge Patch (RFC 7396) semantics
/// under Newtonsoft.Json: an absent property never invokes a converter (the field keeps
/// default(Optional&lt;T&gt;), i.e. unspecified); an explicit null becomes a specified null,
/// or a JsonSerializationException — surfaced by ASP.NET Core as HTTP 400 — for
/// non-nullable value types.
/// </summary>
internal sealed class OptionalNewtonsoftJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
        => objectType.IsGenericType
           && objectType.GetGenericTypeDefinition() == typeof(global::Facet.Optional<>);

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var valueType = objectType.GetGenericArguments()[0];

        if (reader.TokenType == JsonToken.Null
            && valueType.IsValueType
            && Nullable.GetUnderlyingType(valueType) == null)
        {
            throw new JsonSerializationException(
                $"Cannot convert null to non-nullable {valueType.Name}. Omit the property to leave the value unchanged.");
        }

        var value = reader.TokenType == JsonToken.Null ? null : serializer.Deserialize(reader, valueType);
        return Activator.CreateInstance(objectType, value)!;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        // Unspecified values are normally skipped via [JsonProperty(DefaultValueHandling =
        // Ignore)] on the generated properties; if one is serialized directly anyway, null
        // is the closest wire representation.
        var hasValue = value is not null
            && (bool)value.GetType().GetProperty("HasValue")!.GetValue(value)!;
        if (!hasValue)
        {
            writer.WriteNull();
            return;
        }

        serializer.Serialize(writer, value!.GetType().GetProperty("Value")!.GetValue(value));
    }
}
""";

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

    /// <summary>
    /// Resolves ExcludeNavigationProperties into a final member list from the EF model
    /// manifest — the sole source of truth; there is no heuristic fallback. When the source
    /// type has a manifest entry, exactly the mapped scalar and complex properties are kept,
    /// so navigations, skip navigations, owned references, and EF-ignored properties all drop.
    /// A property the model has no opinion on is reported as FAC106 (stale manifest). A type
    /// with no manifest entry at all is reported as FAC105 (error): the DTO cannot be shaped.
    /// IncludeProperties always wins.
    /// </summary>
    private static GenerateDtosTargetModel ResolveNavigationExclusions(
        SgfSourceProductionContext spc,
        GenerateDtosTargetModel model,
        EfModelManifest manifest,
        HashSet<string> reportedCoverage)
    {
        if (!model.ExcludeNavigationProperties)
        {
            return model;
        }

        var includeProperties = new HashSet<string>(model.IncludeProperties, StringComparer.OrdinalIgnoreCase);

        var sourceClrName = Shared.GeneratorUtilities.StripGlobalPrefix(model.SourceTypeName);
        if (manifest.TryGetEntity(sourceClrName, out var entity))
        {
            foreach (var propertyName in model.SettableProperties)
            {
                if (entity!.Known.Contains(propertyName)) continue;
                if (includeProperties.Contains(propertyName)) continue;
                if (!reportedCoverage.Add($"{sourceClrName}.{propertyName}")) continue;

                spc.ReportDiagnostic(Diagnostic.Create(
                    PropertyNotInManifestRule,
                    model.AttributeLocation?.ToLocation() ?? Location.None,
                    propertyName,
                    GetSimpleTypeName(model.SourceTypeName)));
            }

            // Fields are not EF-mapped members; the manifest has no opinion on them, so they
            // keep the behavior IncludeFields already gave them.
            return model.WithResolvedMembers(model.Members
                .Where(m => m.Kind != FacetMemberKind.Property
                    || entity!.Keep.Contains(m.Name)
                    || includeProperties.Contains(m.Name))
                .ToImmutableArray());
        }

        // No manifest entry — the model has said nothing about this type, so the DTO shape is
        // undefined. This is a hard error, not a silent guess: emit no exclusion (keep every
        // member so downstream code still compiles) and let FAC105 be the signal.
        if (reportedCoverage.Add(sourceClrName))
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                TypeNotInManifestRule,
                model.AttributeLocation?.ToLocation() ?? Location.None,
                GetSimpleTypeName(model.SourceTypeName)));
        }

        return model.WithResolvedMembers(model.Members);
    }
}
