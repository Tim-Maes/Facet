using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Facet.Generators;

[Generator]
public sealed class ClassGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var facetDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Facet.FacetAttribute",
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) => GetTargetModel(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(facetDeclarations, static (context, model) =>
        {
            var generated = Generate(model!);
            context.AddSource(model!.Name + ".g.cs", SourceText.From(generated, Encoding.UTF8));
        });
    }

    private static FacetTargetModel? GetTargetModel(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol targetSymbol)
            return null;

        var attribute = context.Attributes.FirstOrDefault();
        if (attribute == null || attribute.ConstructorArguments.Length == 0)
            return null;

        var sourceType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        if (sourceType == null)
            return null;

        var excluded = new HashSet<string>(attribute.ConstructorArguments.ElementAtOrDefault(1).Values
            .Select(v => v.Value?.ToString())
            .Where(n => n != null));

        var includeFields = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "IncludeFields").Value.Value as bool? ?? false;
        var generateConstructor = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "GenerateConstructor").Value.Value as bool? ?? true;
        var configurationTypeName = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Configuration").Value.Value?.ToString();
        var kind = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Kind").Value.Value is int k
            ? (FacetKind)k
            : FacetKind.Class;

        var members = new List<FacetMember>();

        foreach (var m in sourceType.GetMembers())
        {
            if (!excluded.Contains(m.Name))
            {
                if (m is IPropertySymbol p && p.DeclaredAccessibility == Accessibility.Public)
                {
                    members.Add(new FacetMember(
                        p.Name,
                        p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        FacetMemberKind.Property));
                }
                else if (includeFields && m is IFieldSymbol f && f.DeclaredAccessibility == Accessibility.Public)
                {
                    members.Add(new FacetMember(
                        f.Name,
                        f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        FacetMemberKind.Field));
                }
            }
        }

        var ns = targetSymbol.ContainingNamespace.IsGlobalNamespace ? null : targetSymbol.ContainingNamespace.ToDisplayString();

        return new FacetTargetModel(
            name: targetSymbol.Name,
            @namespace: ns,
            kind: kind,
            generateConstructor: generateConstructor,
            sourceTypeName: sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            configurationTypeName: configurationTypeName,
            members: members.ToImmutableArray()
        );
    }

    private static string Generate(FacetTargetModel model)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace}");
            sb.AppendLine("{");
        }

        var keyword = model.Kind == FacetKind.Record ? "record" : "class";

        sb.AppendLine($"public partial {keyword} {model.Name}");
        sb.AppendLine("{");

        foreach (var member in model.Members)
        {
            if (member.Kind == FacetMemberKind.Property)
            {
                sb.AppendLine($"    public {member.TypeName} {member.Name} {{ get; set; }}");
            }
            else if (member.Kind == FacetMemberKind.Field)
            {
                sb.AppendLine($"    public {member.TypeName} {member.Name};");
            }
        }

        if (model.GenerateConstructor)
        {
            sb.AppendLine();
            sb.AppendLine($"    public {model.Name}({model.SourceTypeName} source)");
            sb.AppendLine("    {");

            foreach (var member in model.Members)
                sb.AppendLine($"        this.{member.Name} = source.{member.Name};");

            if (!string.IsNullOrWhiteSpace(model.ConfigurationTypeName))
                sb.AppendLine($"        {model.ConfigurationTypeName}.Map(source, this);");

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        if (!string.IsNullOrWhiteSpace(model.Namespace))
            sb.AppendLine("}");

        return sb.ToString();
    }
}
