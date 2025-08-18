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
            var configurationTypeName = attribute.NamedArguments
                                                 .FirstOrDefault(kvp => kvp.Key == "Configuration")
                                                 .Value.Value?
                                                 .ToString();
            var kind = attribute.NamedArguments
                               .FirstOrDefault(kvp => kvp.Key == "Kind")
                               .Value.Value is int k
                ? (FacetKind)k
                : FacetKind.Class;

            var members = new List<FacetMember>();
            var addedMembers = new HashSet<string>();

            var allMembers = GetAllMembers(sourceType);

            foreach (var m in allMembers)
            {
                token.ThrowIfCancellationRequested();
                if (excluded.Contains(m.Name)) continue;
                if (addedMembers.Contains(m.Name)) continue;

                if (m is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } p)
                {
                    members.Add(new FacetMember(
                        p.Name,
                        p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        FacetMemberKind.Property));
                    addedMembers.Add(p.Name);
                }
                else if (includeFields && m is IFieldSymbol { DeclaredAccessibility: Accessibility.Public } f)
                {
                    members.Add(new FacetMember(
                        f.Name,
                        f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        FacetMemberKind.Field));
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
        private static IEnumerable<ISymbol> GetAllMembers(INamedTypeSymbol type)
        {
            var visited = new HashSet<string>();
            var current = type;

            while (current != null)
            {
                foreach (var member in current.GetMembers())
                {
                    if ((member is IPropertySymbol || member is IFieldSymbol) &&
                        member.DeclaredAccessibility == Accessibility.Public &&
                        !visited.Contains(member.Name))
                    {
                        visited.Add(member.Name);
                        yield return member;
                    }
                }

                current = current.BaseType;

                if (current?.SpecialType == SpecialType.System_Object)
                    break;
            }
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

            if (isPositional)
            {
                var parameters = string.Join(", ",
                    model.Members.Select(m => $"{m.TypeName} {m.Name}"));
                sb.AppendLine($"public partial {keyword} {model.Name}({parameters});");
            }

            sb.AppendLine($"public partial {keyword} {model.Name}");
            sb.AppendLine("{");

            if (!isPositional)
            {
                foreach (var m in model.Members)
                {
                    if (m.Kind == FacetMemberKind.Property)
                        sb.AppendLine($"    public {m.TypeName} {m.Name} {{ get; set; }}");
                    else
                        sb.AppendLine($"    public {m.TypeName} {m.Name};");
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
                    foreach (var m in model.Members)
                        sb.AppendLine($"        this.{m.Name} = source.{m.Name};");
                }
                if (!string.IsNullOrWhiteSpace(model.ConfigurationTypeName))
                    sb.AppendLine($"        {model.ConfigurationTypeName}.Map(source, this);");
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
