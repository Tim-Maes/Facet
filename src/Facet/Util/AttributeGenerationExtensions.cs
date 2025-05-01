using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Facet.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Facet.Util;

/// <summary>
/// A set of extensions to support generating attributes into the target project, from DTOs in the source generator
/// </summary>
public static class AttributeGenerationExtensions
{
    private static readonly NullabilityInfoContext _nullabilityInfoContext = new();

    public static void GenerateAttribute(this IncrementalGeneratorInitializationContext context, Type attributeMetadataType)
    {
        context.RegisterPostInitializationOutput(spc =>
        {
            var metadata = new AttributeGenerationMetadata(attributeMetadataType);
            var constructors = metadata.Constructors
                .Select(c => new
                {
                    Parameters = c.Parameters.Select(x => new
                    {
                        Type = x.Type.IsAssignableFrom(typeof(INamedTypeSymbol))
                            ? typeof(Type)
                            : x.Type,
                        x.Name,
                        LiteralString = x.Default.HasValue
                            ? LiteralString(x.Default.Value)
                            : null,
                        x.IsParams,
                    }).Select(x => new
                    {
                        ArgDefinition = x.LiteralString is not null
                            ? $"{(x.IsParams ? "params " : "")}{x.Type.Fqn()} {x.Name} = {x.LiteralString}"
                            : $"{(x.IsParams ? "params " : "")}{x.Type.Fqn()} {x.Name}",
                    }).ToList(),
                }).ToList();

            var properties = metadata.Properties
                .Select(x => new
                {
                    x.Name,
                    Type = GetPropertyTypeSymbol(x.PropertyInfo),
                    x.Comment,
                }).Select(x => x with
                {
                    // If the type is INamedTypeSymbol on the generator side, accept a `System.Type` on the consumer side.
                    // This is because typeof(...) in an attribute arg, will be given as an INamedTypeSymbol when parsed by the generator
                    Type = x.Type with
                    {
                        Type = x.Type.Type.IsAssignableFrom(typeof(INamedTypeSymbol))
                            ? typeof(Type)
                            : x.Type.Type,
                    },
                })
                .Select(x => new
                {
                    PropDefinition = $"public {x.Type.Fqn()} {x.Name} {{ get; init; }}",
                    x.Comment,
                }).ToList();

            var sb = new StringBuilder();
            sb.AppendLine(
                $$"""
                  #nullable enable
                  using global::System;

                  namespace {{FacetConstants.DefaultNamespace}} {
                  """);

            if (!String.IsNullOrEmpty(metadata.Comment))
            {
                sb.AppendLine(metadata.Comment!.WithIndent("    "));
            }
            
            sb.AppendLine($"    {MakeAttributeUsageLine(metadata)}");
            sb.AppendLine($"    internal sealed class {metadata.AttributeClassName} : Attribute {{");

            foreach (var ctor in constructors)
            {
                var p = ctor.Parameters;
                sb.AppendLine($"        public {metadata.AttributeClassName}({p.Select(x => x.ArgDefinition).JoinStrings(", ")}) {{}}");
            }

            foreach (var prop in properties)
            {
                if (!String.IsNullOrEmpty(prop.Comment))
                {
                    sb.AppendLine(prop.Comment!.WithIndent("        "));
                }
                
                sb.AppendLine($"        {prop.PropDefinition}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource(
                $"{metadata.AttributeClassName}.g.cs",
                sb.ToString()
            );
        });
        return;

        static PropertyType GetPropertyTypeSymbol(PropertyInfo pi)
        {
            var ni = _nullabilityInfoContext.Create(pi);
            var isNullable = ni.ReadState != NullabilityState.NotNull;
            return new PropertyType()
            {
                Type = pi.PropertyType,
                Nullable = isNullable,
            };
        }

        static string MakeAttributeUsageLine(AttributeGenerationMetadata metadata)
        {
            var attributeTargetsString = metadata.AttributeTargets.Select(x => $"AttributeTargets.{x}")
                .JoinStrings(" | ");
            var allowMultiple = metadata.AllowMultiple.ToString().ToLower();
            var inherited = metadata.Inherited.ToString().ToLower();

            return $"[AttributeUsage({attributeTargetsString}, Inherited = {inherited}, AllowMultiple = {allowMultiple})]";
        }

        static string LiteralString(object? value)
        {
            if (value is null)
            {
                return "null";
            }

            if (value is bool b)
            {
                return b ? "true" : "false";
            }

            return $"{value}";
        }
    }

    public static bool TryGetAttribute<T>(this ISymbol symbol, [MaybeNullWhen(false)] out T attr) where T : class
    {
        attr = symbol.GetAttribute<T>();
        return attr is not null;
    }

    public static T? GetAttribute<T>(this ISymbol symbol) where T : class
    {
        return GetAttributes<T>(symbol).FirstOrDefault();
    }

    public static IEnumerable<T> GetAttributes<T>(this ISymbol symbol) where T : class
    {
        var metadata = new AttributeGenerationMetadata(typeof(T));
        var attrs = symbol.GetAttributes()
            .Where(x =>
                (x.AttributeClass is IErrorTypeSymbol && x.AttributeClass?.Name == metadata.AttributeName)
                || x.AttributeClass?.Fqn() == typeof(T).Fqn().Replace(
                    "global::Facet.Attributes.",
                    "global::Facet."));

        foreach (var attr in attrs)
        {
            var ctorArgs = attr.ConstructorArguments
                .Select(GetTypedConstantValue)
                .ToArray();

            var instance = (T)Activator.CreateInstance(
                typeof(T),
                BindingFlags.CreateInstance | BindingFlags.OptionalParamBinding,
                null,
                ctorArgs,
                CultureInfo.InvariantCulture
            );

            foreach (var namedArg in attr.NamedArguments)
            {
                var name = namedArg.Key;
                var value = GetTypedConstantValue(namedArg.Value);

                var pi = typeof(T).GetProperty(name);
                if (pi?.SetMethod is null)
                {
                    continue;
                }

                if (value is null)
                {
                    pi.SetValue(instance, value);
                }
                else if (value.GetType().IsAssignableTo(pi.PropertyType))
                {
                    pi.SetValue(instance, value);
                }
                else if (value is int i && pi.PropertyType.IsEnum)
                {
                    pi.SetValue(instance, i);
                }
            }

            yield return instance;
        }

        yield break;

        static object? GetTypedConstantValue(TypedConstant tc)
        {
            if (tc.Kind == TypedConstantKind.Array)
            {

                // This is really brittle, but I can't think of a very reliable alternative atm..
                // the problem is we need the array to be correctly typed in order to find the appropriate constructor
                // with reflection
                if (tc.Type?.Fqn() == "string[]")
                {
                    return tc.Values
                        .Select(GetTypedConstantValue)
                        .Cast<string>()
                        .ToArray();
                }

                // object[]
                return tc.Values
                    .Select(GetTypedConstantValue)
                    .ToArray();
            }

            return tc.Value;
        }
    }

    public static bool HasAttribute<T>(this ISymbol symbol) where T : class
    {
        return GetAttributes<T>(symbol).Any();
    }

    public static bool HasAttribute<T>(this SyntaxNode syntax)
    {
        var attributeName = typeof(T).Name;
        if (attributeName.EndsWith("Attribute"))
        {
            attributeName = attributeName[..^9];
        }

        if (syntax is MemberDeclarationSyntax memberSyntax)
        {
            var attributes = memberSyntax.AttributeLists
                .SelectMany(x => x.Attributes);

            return attributes.Any(x => x.Name.ToString() == attributeName);
        }

        return false;
    }

    private class AttributeGenerationMetadata
    {

        public AttributeGenerationMetadata(Type type)
        {
            AttributeClassName = type.Name;
            AttributeName = type.Name.EndsWith("Attribute")
                ? type.Name[..^9]
                : type.Name;

            var usage = type.GetCustomAttribute<GenerateAttributeAttribute>();
            AttributeTargets = usage is not null
                ? YieldAttributeTargets(usage.ValidOn).ToArray()
                : [System.AttributeTargets.All];
            Inherited = usage?.Inherited ?? true;
            AllowMultiple = usage?.AllowMultiple ?? false;

            Constructors = BuildConstructors(type).ToArray();
            Properties = BuildProperties(type).ToArray();
            Comment = type.GetCustomAttribute<GenerateCommentAttribute>()
                ?.Content;
        }

        public string AttributeClassName { get; }
        public string AttributeName { get; }
        public AttributeTargets[] AttributeTargets { get; set; }
        public bool Inherited { get; set; }
        public bool AllowMultiple { get; set; }
        public AttributeConstructor[] Constructors { get; set; }
        public AttributeProperty[] Properties { get; set; }
        public string? Comment { get; }

        private static IEnumerable<AttributeTargets> YieldAttributeTargets(AttributeTargets targets)
        {
            var values = Enum.GetValues(typeof(AttributeTargets))
                .Cast<AttributeTargets>();

            foreach (var value in values)
            {
                if (targets.HasFlag(value))
                {
                    yield return value;
                }
            }
        }

        private static IEnumerable<AttributeConstructor> BuildConstructors(Type type)
        {
            var cis = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            foreach (var ci in cis)
            {
                var pis = ci.GetParameters();
                yield return new AttributeConstructor()
                {
                    Parameters = pis.Select(p => new AttributeConstructorParam()
                    {
                        Name = p.Name,
                        Type = p.ParameterType,
                        Default = p.HasDefaultValue
                            ? new Optional<object?>(p.DefaultValue)
                            : new Optional<object?>(),
                        IsParams = p.GetCustomAttribute<ParamArrayAttribute>() is not null,
                    }).ToArray(),
                };
            }
        }

        private static IEnumerable<AttributeProperty> BuildProperties(Type type)
        {
            var pis = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var pi in pis)
            {
                if (pi.SetMethod is null)
                {
                    continue;
                }

                yield return new AttributeProperty()
                {
                    Name = pi.Name,
                    Type = pi.PropertyType,
                    PropertyInfo = pi,
                    Comment = pi.GetCustomAttribute<GenerateCommentAttribute>()
                        ?.Content,
                };
            }
        }

    }

    private record AttributeConstructor
    {

        public required AttributeConstructorParam[] Parameters { get; init; }

    }

    private record AttributeConstructorParam
    {

        public required Type Type { get; init; }
        public required string Name { get; init; }
        public required Optional<object?> Default { get; init; }
        public required bool IsParams { get; set; }

    }

    private record AttributeProperty
    {

        public required PropertyInfo PropertyInfo { get; init; }
        public required Type Type { get; init; }
        public required string Name { get; init; }
        public string? Comment { get; init; }

    }

    private record PropertyType
    {

        public required Type Type { get; init; }
        public required bool Nullable { get; init; }

        public string Fqn()
        {
            // We only want to apply the `?` if the type is not already wrapped in a Nullable<>
            if (Nullable && !Type.IsWrappedNullable())
            {
                return $"{Type.Fqn()}?";
            }

            return Type.Fqn();
        }

    }

}