using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Facet.Attributes;
using Facet.Util;

namespace Facet.Generators
{
    [Generator(LanguageNames.CSharp)]
    public sealed class FacetGenerator : IIncrementalGenerator
    {
        private const string FacetAttributeName = $"{FacetConstants.DefaultNamespace}.FacetAttribute";

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
            if (!targetSymbol.TryGetAttribute<FacetAttribute>(out var attribute)) {
                return null;
            }
            
            token.ThrowIfCancellationRequested();

            var sourceType = attribute.SourceType;
            var excluded = attribute.Exclude.ToHashSet();
            var includeFields = attribute.IncludeFields;
            var generateConstructor = attribute.GenerateConstructor;
            var generateProjection = attribute.GenerateProjection;
            var configurationTypeName = attribute.Configuration?.ToString();
            var kind = attribute.Kind;

            var members = new List<FacetMember>();

            foreach (var m in sourceType.GetMembers())
            {
                token.ThrowIfCancellationRequested();
                if (excluded.Contains(m.Name)) continue;

                if (m is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } p)
                {
                    members.Add(new FacetMember(
                        p.Name,
                        p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        FacetMemberKind.Property));
                }
                else if (includeFields && m is IFieldSymbol { DeclaredAccessibility: Accessibility.Public } f)
                {
                    members.Add(new FacetMember(
                        f.Name,
                        f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        FacetMemberKind.Field));
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

            var keyword = model.Kind == FacetKind.Record ? "record" : "class";

            sb.AppendLine($"public partial {keyword} {model.Name}");
            sb.AppendLine("{");

            foreach (var m in model.Members)
            {
                if (m.Kind == FacetMemberKind.Property)
                    sb.AppendLine($"    public {m.TypeName} {m.Name} {{ get; set; }}");
                else
                    sb.AppendLine($"    public {m.TypeName} {m.Name};");
            }

            if (model.GenerateConstructor)
            {
                sb.AppendLine();
                sb.AppendLine($"    public {model.Name}({model.SourceTypeName} source)");
                sb.AppendLine("    {");

                foreach (var m in model.Members)
                    sb.AppendLine($"        this.{m.Name} = source.{m.Name};");

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