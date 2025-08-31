using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace Facet.Generators
{
    [Generator(LanguageNames.CSharp)]
    public sealed class FacetGenerator : IIncrementalGenerator
    {
        private const string FacetAttributeName = "Facet.FacetAttribute";
        private const string GenerateDtosAttributeName = "Facet.GenerateDtosAttribute";
        private const string GenerateAuditableDtosAttributeName = "Facet.GenerateAuditableDtosAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Facets
            var facets = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    FacetAttributeName,
                    predicate: static (node, _) => node is TypeDeclarationSyntax,
                    transform: static (ctx, token) => GetTargetModel(ctx, token))
                .Where(static m => m is not null);

            // DTO
            var multiDtos = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    GenerateDtosAttributeName,
                    predicate: static (node, _) => node is TypeDeclarationSyntax,
                    transform: static (ctx, token) => GetMultiDtoModels(ctx, token))
                .Where(static models => models is not null)
                .SelectMany(static (models, _) => models ?? ImmutableArray<FacetTargetModel>.Empty);

            var auditableDtos = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    GenerateAuditableDtosAttributeName,
                    predicate: static (node, _) => node is TypeDeclarationSyntax,
                    transform: static (ctx, token) => GetMultiDtoModels(ctx, token))
                .Where(static models => models is not null)
                .SelectMany(static (models, _) => models ?? ImmutableArray<FacetTargetModel>.Empty);

            // Register facet
            context.RegisterSourceOutput(facets, static (spc, model) =>
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                var code = Generate(model!);
                spc.AddSource($"{model!.Name}.g.cs", SourceText.From(code, Encoding.UTF8));
            });

            // Register multi-DTO
            context.RegisterSourceOutput(multiDtos, static (spc, model) =>
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                var code = Generate(model);
                spc.AddSource($"{model.Name}.g.cs", SourceText.From(code, Encoding.UTF8));
            });

            context.RegisterSourceOutput(auditableDtos, static (spc, model) =>
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                var code = Generate(model);
                spc.AddSource($"{model.Name}.g.cs", SourceText.From(code, Encoding.UTF8));
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

            foreach (var (member, isInitOnly, isRequired) in allMembersWithModifiers)
            {
                token.ThrowIfCancellationRequested();
                if (excluded.Contains(member.Name)) continue;
                if (addedMembers.Contains(member.Name)) continue;

                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } p)
                {
                    var shouldPreserveInitOnly = preserveInitOnly && isInitOnly;
                    var shouldPreserveRequired = preserveRequired && isRequired;
                    
                    members.Add(new FacetMember(
                        p.Name,
                        p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        FacetMemberKind.Property,
                        shouldPreserveInitOnly,
                        shouldPreserveRequired));
                    addedMembers.Add(p.Name);
                }
                else if (includeFields && member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public } f)
                {
                    var shouldPreserveRequired = preserveRequired && isRequired;
                    
                    members.Add(new FacetMember(
                        f.Name,
                        f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        FacetMemberKind.Field,
                        false, // Fields don't have init-only
                        shouldPreserveRequired));
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
                generateProjection,
                sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                configurationTypeName,
                members.ToImmutableArray(),
                hasExistingPrimaryConstructor);
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

        private static string Generate(FacetTargetModel model)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Linq.Expressions;");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(model.Namespace))
            {
                sb.AppendLine($"namespace {model.Namespace}");
                sb.AppendLine("{");
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
                    sb.AppendLine($"    public static Expression<Func<{model.SourceTypeName}, {model.Name}>> Projection =>");
                    sb.AppendLine($"        source => new {model.Name}(source);");
                }
            }

            sb.AppendLine("}");

            if (!string.IsNullOrWhiteSpace(model.Namespace))
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

        // multi-DTO generation
        private static ImmutableArray<FacetTargetModel>? GetMultiDtoModels(GeneratorAttributeSyntaxContext context, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (context.TargetSymbol is not INamedTypeSymbol sourceSymbol) return null;
            if (context.Attributes.Length == 0) return null;

            var attribute = context.Attributes[0];
            
            var types = GetNamedArg(attribute.NamedArguments, "Types", DtoTypes.All);
            var outputType = GetNamedArg(attribute.NamedArguments, "OutputType", FacetKind.Record);
            var excludeProperties = GetStringArrayArg(attribute.NamedArguments, "ExcludeProperties") ?? System.Array.Empty<string>();
            var targetNamespace = GetNamedArg<string?>(attribute.NamedArguments, "Namespace", null);
            var namingConvention = GetNamedArg(attribute.NamedArguments, "NamingConvention", DtoNamingConvention.Convention);
            var customPrefix = GetNamedArg<string?>(attribute.NamedArguments, "CustomPrefix", null);
            var customSuffix = GetNamedArg<string?>(attribute.NamedArguments, "CustomSuffix", null);
            var includeFields = GetNamedArg(attribute.NamedArguments, "IncludeFields", false);
            var generateConstructor = GetNamedArg(attribute.NamedArguments, "GenerateConstructor", true);
            var generateProjection = GetNamedArg(attribute.NamedArguments, "GenerateProjection", true);
            var preserveInitOnlyDefault = outputType is FacetKind.Record or FacetKind.RecordStruct;
            var preserveRequiredDefault = outputType is FacetKind.Record or FacetKind.RecordStruct;
            var preserveInitOnly = GetNamedArg(attribute.NamedArguments, "PreserveInitOnlyProperties", preserveInitOnlyDefault);
            var preserveRequired = GetNamedArg(attribute.NamedArguments, "PreserveRequiredProperties", preserveRequiredDefault);

            var ns = !string.IsNullOrWhiteSpace(targetNamespace) 
                ? targetNamespace 
                : (sourceSymbol.ContainingNamespace.IsGlobalNamespace 
                    ? null 
                    : sourceSymbol.ContainingNamespace.ToDisplayString());

            var models = new List<FacetTargetModel>();
            var sourceTypeName = sourceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Generate each requested DTO type
            if (types.HasFlag(DtoTypes.Create))
            {
                models.Add(CreateDtoVariant(sourceSymbol, sourceTypeName, ns, outputType, excludeProperties, DtoTypes.Create, 
                    namingConvention, customPrefix, customSuffix, includeFields, generateConstructor, generateProjection, 
                    preserveInitOnly, preserveRequired, token));
            }
            
            if (types.HasFlag(DtoTypes.Update))
            {
                models.Add(CreateDtoVariant(sourceSymbol, sourceTypeName, ns, outputType, excludeProperties, DtoTypes.Update, 
                    namingConvention, customPrefix, customSuffix, includeFields, generateConstructor, generateProjection, 
                    preserveInitOnly, preserveRequired, token));
            }
            
            if (types.HasFlag(DtoTypes.Response))
            {
                models.Add(CreateDtoVariant(sourceSymbol, sourceTypeName, ns, outputType, excludeProperties, DtoTypes.Response, 
                    namingConvention, customPrefix, customSuffix, includeFields, generateConstructor, generateProjection, 
                    preserveInitOnly, preserveRequired, token));
            }
            
            if (types.HasFlag(DtoTypes.Query))
            {
                models.Add(CreateDtoVariant(sourceSymbol, sourceTypeName, ns, outputType, excludeProperties, DtoTypes.Query, 
                    namingConvention, customPrefix, customSuffix, includeFields, generateConstructor, generateProjection, 
                    preserveInitOnly, preserveRequired, token));
            }

            return models.ToImmutableArray();
        }

        private static FacetTargetModel CreateDtoVariant(
            INamedTypeSymbol sourceSymbol, 
            string sourceTypeName, 
            string? ns, 
            FacetKind outputType, 
            string[] baseExcludeProperties, 
            DtoTypes dtoType, 
            DtoNamingConvention namingConvention,
            string? customPrefix,
            string? customSuffix,
            bool includeFields,
            bool generateConstructor,
            bool generateProjection,
            bool preserveInitOnly,
            bool preserveRequired,
            CancellationToken token)
        {
            var excludeProperties = new HashSet<string>(baseExcludeProperties, System.StringComparer.OrdinalIgnoreCase);
            
            // Apply smart defaults based on DTO type
            ApplyDtoTypeDefaults(excludeProperties, dtoType, sourceSymbol);

            var members = new List<FacetMember>();
            var addedMembers = new HashSet<string>();

            foreach (var (member, isInitOnly, isRequired) in GetAllMembersWithModifiers(sourceSymbol))
            {
                token.ThrowIfCancellationRequested();
                if (excludeProperties.Contains(member.Name)) continue;
                if (addedMembers.Contains(member.Name)) continue;

                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } p)
                {
                    var facetMember = CreateFacetMemberForDto(p, isInitOnly, isRequired, dtoType, outputType, preserveInitOnly, preserveRequired);
                    members.Add(facetMember);
                    addedMembers.Add(p.Name);
                }
                else if (includeFields && member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public } f)
                {
                    var facetMember = CreateFacetMemberForDto(f, false, isRequired, dtoType, outputType, preserveInitOnly, preserveRequired);
                    members.Add(facetMember);
                    addedMembers.Add(f.Name);
                }
            }

            var dtoName = GenerateDtoName(sourceSymbol.Name, dtoType, namingConvention, customPrefix, customSuffix);

            return new FacetTargetModel(
                dtoName,
                ns,
                outputType,
                generateConstructor,
                generateProjection,
                sourceTypeName,
                null, // configurationTypeName - not supported for generated DTOs
                members.ToImmutableArray(),
                false); // hasExistingPrimaryConstructor - always false for generated DTOs
        }

        private static void ApplyDtoTypeDefaults(HashSet<string> excludeProperties, DtoTypes dtoType, INamedTypeSymbol sourceSymbol)
        {
            switch (dtoType)
            {
                case DtoTypes.Create:
                    excludeProperties.Add("Id");
                    AddIdPropertiesToExclude(excludeProperties, sourceSymbol);
                    AddAuditFieldsToExclude(excludeProperties);
                    break;
                    
                case DtoTypes.Update:
                    AddAuditFieldsToExclude(excludeProperties);
                    break;
                    
                case DtoTypes.Response:
                    break;
                    
                case DtoTypes.Query:
                    break;
            }
        }

        private static void AddIdPropertiesToExclude(HashSet<string> excludeProperties, INamedTypeSymbol sourceSymbol)
        {
            foreach (var member in sourceSymbol.GetMembers())
            {
                if (member is IPropertySymbol prop && string.Equals(prop.Name, "Id", System.StringComparison.OrdinalIgnoreCase))
                {
                    excludeProperties.Add(prop.Name);
                    break; // Only exclude the first "Id" property
                }
            }
        }

        private static void AddAuditFieldsToExclude(HashSet<string> excludeProperties)
        {
            var auditFields = new[]
            {
                "CreatedAt", "CreatedDate", "CreatedOn", "CreatedBy",
                "UpdatedAt", "UpdatedDate", "UpdatedOn", "UpdatedBy", "ModifiedBy",
                "DeletedAt", "DeletedDate", "DeletedOn", "DeletedBy"
            };
            
            foreach (var field in auditFields)
            {
                excludeProperties.Add(field);
            }
        }

        private static FacetMember CreateFacetMemberForDto(ISymbol symbol, bool isInitOnly, bool isRequired, DtoTypes dtoType, FacetKind outputType, bool preserveInitOnly, bool preserveRequired)
        {
            var typeName = symbol switch
            {
                IPropertySymbol prop => prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                IFieldSymbol field => field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                _ => "object"
            };
            
            if (dtoType == DtoTypes.Query)
            {
                typeName = MakeNullable(typeName, symbol);
                isRequired = false; // Query properties are never required
            }

            var memberKind = symbol is IPropertySymbol ? FacetMemberKind.Property : FacetMemberKind.Field;
            
            var shouldPreserveInitOnly = preserveInitOnly && outputType is FacetKind.Record or FacetKind.RecordStruct && 
                              dtoType != DtoTypes.Query && isInitOnly;
            
            var shouldPreserveRequired = preserveRequired && outputType is FacetKind.Record or FacetKind.RecordStruct && 
                              dtoType != DtoTypes.Query && isRequired;

            return new FacetMember(symbol.Name, typeName, memberKind, shouldPreserveInitOnly, shouldPreserveRequired);
        }

        private static string MakeNullable(string typeName, ISymbol symbol)
        {
            var type = symbol switch
            {
                IPropertySymbol prop => prop.Type,
                IFieldSymbol field => field.Type,
                _ => null
            };

            if (type == null) return $"{typeName}?";

            if (type.CanBeReferencedByName && type.Name == "Nullable" && type.ContainingNamespace?.Name == "System")
                return typeName;

            if (type.IsReferenceType)
                return typeName.EndsWith("?") ? typeName : $"{typeName}?";

            if (type.IsValueType)
                return $"{typeName}?";

            return $"{typeName}?";
        }

        private static string GenerateDtoName(string sourceTypeName, DtoTypes dtoType, DtoNamingConvention convention, string? customPrefix, string? customSuffix)
        {
            return convention switch
            {
                DtoNamingConvention.Convention => dtoType switch
                {
                    DtoTypes.Create => $"Create{sourceTypeName}Request",
                    DtoTypes.Update => $"Update{sourceTypeName}Request", 
                    DtoTypes.Response => $"{sourceTypeName}Response",
                    DtoTypes.Query => $"{sourceTypeName}Query",
                    _ => $"{sourceTypeName}Dto"
                },
                DtoNamingConvention.Custom => $"{customPrefix ?? ""}{sourceTypeName}{customSuffix ?? ""}",
                _ => $"{sourceTypeName}Dto"
            };
        }

        private static string[]? GetStringArrayArg(ImmutableArray<KeyValuePair<string, TypedConstant>> args, string name)
        {
            var arg = args.FirstOrDefault(kv => kv.Key == name);
            if (arg.Value.IsNull || arg.Value.Values.IsDefaultOrEmpty)
                return null;

            return arg.Value.Values
                .Select(v => v.Value?.ToString())
                .Where(s => s != null)
                .ToArray()!;
        }
    }
}
