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
            var generateProjection = GetNamedArg(attribute.NamedArguments, "GenerateProjection", true);
            var preserveInitOnly = GetNamedArg(attribute.NamedArguments, "PreserveInitOnlyProperties", false);
            var preserveRequired = GetNamedArg(attribute.NamedArguments, "PreserveRequiredProperties", false);
            var configurationTypeName = attribute.NamedArguments
                                                 .FirstOrDefault(kvp => kvp.Key == "Configuration")
                                                 .Value.Value?
                                                 .ToString();
            var kind = attribute.NamedArguments
                               .FirstOrDefault(kvp => kvp.Key == "Kind")
                               .Value.Value is int k
                ? (FacetKind)k
                : FacetKind.Auto;

            // If Auto is specified, infer the kind from the target type
            if (kind == FacetKind.Auto)
            {
                kind = InferFacetKind(targetSymbol);
            }

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

            return new FacetTargetModel(
                targetSymbol.Name,
                ns,
                kind,
                generateConstructor,
                generateProjection,
                sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                configurationTypeName,
                members.ToImmutableArray());
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
            // Check if it's a struct
            if (targetSymbol.TypeKind == TypeKind.Struct)
            {
                // Check if it's a record struct by looking at syntax
                var syntax = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                if (syntax != null && syntax.ToString().Contains("record struct"))
                {
                    return FacetKind.RecordStruct;
                }
                return FacetKind.Struct;
            }

            // Check if it's a class
            if (targetSymbol.TypeKind == TypeKind.Class)
            {
                // Check if it's a record by looking for compiler-generated members or syntax
                var syntax = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                if (syntax != null && syntax.ToString().Contains("record "))
                {
                    return FacetKind.Record;
                }
                
                // Alternative check: records have compiler-generated methods like <Clone>$
                if (targetSymbol.GetMembers().Any(m => m.Name.Contains("Clone") && m.IsImplicitlyDeclared))
                {
                    return FacetKind.Record;
                }
                
                return FacetKind.Class;
            }

            // Default to class if we can't determine
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

            var isPositional = model.Kind is FacetKind.Record or FacetKind.RecordStruct;
            var hasInitOnlyProperties = model.Members.Any(m => m.IsInitOnly);
            var hasCustomMapping = !string.IsNullOrWhiteSpace(model.ConfigurationTypeName);

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

            if (!isPositional)
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

            if (model.GenerateConstructor)
            {
                var ctorSig = $"public {model.Name}({model.SourceTypeName} source)";
                if (isPositional)
                {
                    var args = string.Join(", ",
                        model.Members.Select(m => $"source.{m.Name}"));
                    ctorSig += $" : this({args})";
                }
                sb.AppendLine($"    {ctorSig}");
                sb.AppendLine("    {");
                
                if (!isPositional)
                {
                    if (hasInitOnlyProperties && hasCustomMapping)
                    {
                        // For init-only properties with custom mapping, we need to call the mapping method
                        // and let it return a new instance with all properties set
                        sb.AppendLine($"        var mapped = {model.ConfigurationTypeName}.Map(source, this);");
                        foreach (var m in model.Members.Where(x => !x.IsInitOnly))
                        {
                            sb.AppendLine($"        this.{m.Name} = mapped.{m.Name};");
                        }
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
                        // No custom mapping - just copy properties
                        foreach (var m in model.Members)
                            sb.AppendLine($"        this.{m.Name} = source.{m.Name};");
                    }
                }
                else if (hasCustomMapping)
                {
                    // For positional records/record structs with custom mapping
                    sb.AppendLine($"        {model.ConfigurationTypeName}.Map(source, this);");
                }
                
                sb.AppendLine("    }");
            }

            if (model.GenerateExpressionProjection)
            {
                sb.AppendLine();
                sb.AppendLine($"    public static Expression<Func<{model.SourceTypeName}, {model.Name}>> Projection =>");
                sb.AppendLine($"        source => new {model.Name}(source);");
            }

            sb.AppendLine("}");

            if (!string.IsNullOrWhiteSpace(model.Namespace))
                sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
