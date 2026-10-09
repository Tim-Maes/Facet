using Facet.Generators.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Facet.Generators.MapperGenerators;

/// <summary>
/// Emits one static mapper class per <c>[assembly: FacetMapper(...)]</c> registration.
/// </summary>
/// <remarks>
/// The whole file hinges on one property of Facet's generated facets: their members are settable,
/// so the mapping can be written as an object initializer. An object initializer is legal inside
/// an expression tree, so the <em>same</em> initializer text serves both the runtime mapper and
/// the <c>Projection</c> that EF Core translates to SQL — which is why this generator needs no
/// mapping logic of its own beyond deciding, per member, what the right-hand side is.
/// </remarks>
internal static class FacetMapperCodeBuilder
{
    public static string Generate(MapperRegistration registration, Dictionary<string, MapperRegistration> lookup)
    {
        var hasCollections = registration.Members.Any(m => m.Kind == MappedMemberKind.NestedFacetCollection);
        var sb = new StringBuilder();

        GeneratorUtilities.AppendGeneratedFileHeader(sb);

        sb.AppendLine("using System;");
        if (registration.GenerateProjection || hasCollections)
        {
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Linq.Expressions;");
        }

        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(registration.MapperNamespace))
        {
            sb.AppendLine($"namespace {registration.MapperNamespace};");
            sb.AppendLine();
        }

        GenerateClassDocumentation(sb, registration);
        sb.AppendLine($"public static class {registration.MapperName}");
        sb.AppendLine("{");

        var members = new List<string>();

        if (registration.Configuration.ProjectionTypeName is not null)
            members.Add(BuildProjectionRulesAccessor(registration));

        if (registration.GenerateProjection)
            members.Add(BuildProjection(registration, lookup));

        members.Add(BuildToFacet(registration, lookup));

        if (registration.MappingPackageAvailable)
            members.Add(BuildToFacetWithConfiguration(registration, lookup));

        if (registration.GenerateProjection)
            members.Add(BuildQueryableSelect(registration));

        if (registration.GenerateToSource && registration.SourceHasParameterlessConstructor)
            members.Add(BuildToSource(registration));

        if (registration.GenerateToSource)
            members.Add(BuildApplyTo(registration, lookup));

        sb.Append(string.Join("\n", members));
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string BuildProjection(MapperRegistration registration, Dictionary<string, MapperRegistration> lookup)
    {
        var sb = new StringBuilder();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Gets the projection expression from <see cref=\"{XmlRef(registration.SourceTypeName)}\"/>");
        sb.AppendLine($"    /// to <see cref=\"{XmlRef(registration.FacetTypeName)}\"/>, for use in LINQ and");
        sb.AppendLine("    /// Entity Framework queries so that only the projected columns are read.");
        sb.AppendLine("    /// </summary>");
        var signature = $"Expression<Func<{registration.SourceTypeName}, {registration.FacetTypeName}>>";
        var projectionConfig = registration.Configuration.ProjectionTypeName;

        if (projectionConfig is null)
        {
            sb.AppendLine($"    public static {signature} Projection =>");
            sb.Append("        source => ");
            sb.Append(BuildInitializer(registration, "source", lookup, new HashSet<string> { registration.PairKey }, depth: 0, indent: "        "));
            sb.AppendLine(";");

            return sb.ToString();
        }

        // With a projection configuration the expression is assembled once, on first use: the
        // declarative initializer is built here and the configuration's rules are folded into its
        // bindings, so the result is still one object initializer a query provider can translate.
        sb.AppendLine($"    public static {signature} Projection =>");
        sb.AppendLine($"        global::System.Threading.LazyInitializer.EnsureInitialized(ref __projection, static () =>");
        sb.AppendLine("        {");
        sb.Append($"            {signature} __declarative = source => ");
        sb.Append(BuildInitializer(registration, "source", lookup, new HashSet<string> { registration.PairKey }, depth: 0, indent: "            "));
        sb.AppendLine(";");
        sb.AppendLine();
        sb.AppendLine($"            var __builder = new global::Facet.Mapping.FacetProjectionBuilder<{registration.SourceTypeName}, {registration.FacetTypeName}>();");
        sb.AppendLine($"            {projectionConfig}.ConfigureProjection(__builder);");
        sb.AppendLine();
        sb.AppendLine("            return global::Facet.Mapping.FacetProjectionMerge.Apply(__declarative, __builder);");
        sb.AppendLine("        });");
        sb.AppendLine();
        sb.AppendLine($"    private static {signature}? __projection;");

        return sb.ToString();
    }

    private static string BuildToFacet(MapperRegistration registration, Dictionary<string, MapperRegistration> lookup)
    {
        var sb = new StringBuilder();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Maps a <see cref=\"{XmlRef(registration.SourceTypeName)}\"/> to a new");
        sb.AppendLine($"    /// <see cref=\"{XmlRef(registration.FacetTypeName)}\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"source\">The instance to map from.</param>");
        sb.AppendLine("    /// <exception cref=\"ArgumentNullException\"><paramref name=\"source\"/> is null.</exception>");
        sb.AppendLine($"    public static {registration.FacetTypeName} To{registration.FacetSimpleName}(this {registration.SourceTypeName} source)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (source is null) throw new ArgumentNullException(nameof(source));");
        sb.AppendLine();

        var config = registration.Configuration;

        if (!config.RequiresInstanceSteps)
        {
            sb.Append("        return ");
            sb.Append(BuildInitializer(registration, "source", lookup, new HashSet<string> { registration.PairKey }, depth: 0, indent: "        "));
            sb.AppendLine(";");
            sb.AppendLine("    }");

            return sb.ToString();
        }

        // A hook has to run against an instance, and BeforeMap has to run before the mapped
        // members are written — neither of which an object initializer allows. So the instance is
        // constructed first and populated in the order the hooks define.
        sb.AppendLine($"        var target = new {registration.FacetTypeName}();");
        sb.AppendLine();

        if (config.BeforeMapTypeName is not null)
        {
            sb.AppendLine($"        {config.BeforeMapTypeName}.BeforeMap(source, target);");
            sb.AppendLine();
        }

        foreach (var member in registration.Members)
        {
            var value = BuildMemberValue(
                member, "source", lookup, new HashSet<string> { registration.PairKey }, depth: 0, indent: "        ");

            if (value is not null)
                sb.AppendLine($"        target.{member.Name} = {value};");
        }

        if (config.ProjectionTypeName is not null)
        {
            sb.AppendLine();
            sb.AppendLine("        // The same rules the projection folds into its initializer, as assignments.");
            sb.AppendLine("        __GetProjectionRules()(source, target);");
        }

        if (config.MapTypeName is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"        {config.MapTypeName}.Map(source, target);");
        }

        if (config.AfterMapTypeName is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"        {config.AfterMapTypeName}.AfterMap(source, target);");
        }

        sb.AppendLine();
        sb.AppendLine("        return target;");
        sb.AppendLine("    }");

        return sb.ToString();
    }

    /// <summary>
    /// The projection configuration's rules, compiled once into assignments for the runtime
    /// mapper, so a rule declared for the query path applies on both.
    /// </summary>
    private static string BuildProjectionRulesAccessor(MapperRegistration registration)
    {
        var src = registration.SourceTypeName;
        var facet = registration.FacetTypeName;
        var sb = new StringBuilder();

        sb.AppendLine($"    private static global::System.Action<{src}, {facet}>? __projectionRules;");
        sb.AppendLine();
        // Matches how Facet.Extensions annotates its own dynamic-code paths, so a trimming or
        // NativeAOT analyzer reports this at build time instead of the consumer meeting a
        // PlatformNotSupportedException. Only this path needs it: Projection itself merely builds
        // an expression tree, which is allowed.
        sb.AppendLine("    [global::System.Diagnostics.CodeAnalysis.RequiresDynamicCode(");
        sb.AppendLine($"        \"Applying {GeneratorUtilities.StripGlobalPrefix(registration.Configuration.ProjectionTypeName!)}'s projection rules inside this method compiles an expression tree. \" +");
        sb.AppendLine("        \"Declare imperative rules in IFacetMapConfiguration.Map to avoid it; the Projection itself is unaffected.\")]");
        sb.AppendLine($"    private static global::System.Action<{src}, {facet}> __GetProjectionRules() =>");
        sb.AppendLine("        global::System.Threading.LazyInitializer.EnsureInitialized(ref __projectionRules, static () =>");
        sb.AppendLine("        {");
        sb.AppendLine($"            var __builder = new global::Facet.Mapping.FacetProjectionBuilder<{src}, {facet}>();");
        sb.AppendLine($"            {registration.Configuration.ProjectionTypeName}.ConfigureProjection(__builder);");
        sb.AppendLine();
        sb.AppendLine("            return global::Facet.Mapping.FacetProjectionMerge.CompileAssignments(__builder);");
        sb.AppendLine("        });");

        return sb.ToString();
    }

    /// <summary>
    /// An overload taking the configuration as an instance, for a rule that needs a service.
    /// </summary>
    /// <remarks>
    /// The attribute-declared hooks are static — <c>static abstract void Map</c> cannot take
    /// constructor injection — so a rule depending on a clock, a localiser or a repository has
    /// nowhere to get it. Passing the configuration in instead leaves that to the caller, which is
    /// where a container already lives. It runs after everything the attribute declared, so an
    /// injected rule has the last word.
    /// </remarks>
    private static string BuildToFacetWithConfiguration(
        MapperRegistration registration,
        Dictionary<string, MapperRegistration> lookup)
    {
        var src = registration.SourceTypeName;
        var facet = registration.FacetTypeName;
        var sb = new StringBuilder();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Maps a <see cref=\"{XmlRef(src)}\"/> to a new <see cref=\"{XmlRef(facet)}\"/>,");
        sb.AppendLine("    /// then applies <paramref name=\"configuration\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// For a rule that needs a dependency: the hooks declared on the attribute are static and");
        sb.AppendLine("    /// cannot be injected, so resolve the configuration and pass it here. It runs last, after");
        sb.AppendLine("    /// any attribute-declared configuration.");
        sb.AppendLine("    /// </remarks>");
        sb.AppendLine("    /// <param name=\"source\">The instance to map from.</param>");
        sb.AppendLine("    /// <param name=\"configuration\">Custom mapping applied to the result.</param>");
        sb.AppendLine("    /// <exception cref=\"ArgumentNullException\">Either argument is null.</exception>");
        sb.AppendLine($"    public static {facet} To{registration.FacetSimpleName}(");
        sb.AppendLine($"        this {src} source,");
        sb.AppendLine($"        global::Facet.Mapping.IFacetMapConfigurationInstance<{src}, {facet}> configuration)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (configuration is null) throw new ArgumentNullException(nameof(configuration));");
        sb.AppendLine();
        sb.AppendLine($"        var target = To{registration.FacetSimpleName}(source);");
        sb.AppendLine("        configuration.Map(source, target);");
        sb.AppendLine();
        sb.AppendLine("        return target;");
        sb.AppendLine("    }");

        return sb.ToString();
    }

    private static string BuildQueryableSelect(MapperRegistration registration)
    {
        var sb = new StringBuilder();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Projects a query of <see cref=\"{XmlRef(registration.SourceTypeName)}\"/> to");
        sb.AppendLine($"    /// <see cref=\"{XmlRef(registration.FacetTypeName)}\"/> using <see cref=\"Projection\"/>,");
        sb.AppendLine("    /// so the projection is translated by the query provider rather than run in memory.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static IQueryable<{registration.FacetTypeName}> Select{registration.FacetSimpleName}(this IQueryable<{registration.SourceTypeName}> source)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (source is null) throw new ArgumentNullException(nameof(source));");
        sb.AppendLine();
        sb.AppendLine("        return source.Select(Projection);");
        sb.AppendLine("    }");

        return sb.ToString();
    }

    /// <summary>
    /// Emits the reverse mapping over the members that can actually carry a value back: a direct
    /// member whose source property is publicly settable. Nested facets are skipped, because
    /// reversing them needs the nested source instance's identity, which a facet does not carry.
    /// </summary>
    private static string BuildToSource(MapperRegistration registration)
    {
        var reversible = registration.ReverseMappableMembers.ToList();
        var skipped = registration.ReverseSkippedMembers.Select(m => m.Name).ToList();

        var sb = new StringBuilder();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Maps a <see cref=\"{XmlRef(registration.FacetTypeName)}\"/> back to a new");
        sb.AppendLine($"    /// <see cref=\"{XmlRef(registration.SourceTypeName)}\"/>.");
        sb.AppendLine("    /// </summary>");

        if (skipped.Count > 0)
        {
            sb.AppendLine("    /// <remarks>");
            sb.AppendLine($"    /// Left at its default on the result: {string.Join(", ", skipped)}.");
            sb.AppendLine("    /// Nested facets and read-only source properties cannot be mapped back.");
            sb.AppendLine("    /// </remarks>");
        }

        sb.AppendLine("    /// <param name=\"facet\">The instance to map from.</param>");
        sb.AppendLine("    /// <exception cref=\"ArgumentNullException\"><paramref name=\"facet\"/> is null.</exception>");
        sb.AppendLine($"    public static {registration.SourceTypeName} To{registration.SourceSimpleName}(this {registration.FacetTypeName} facet)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (facet is null) throw new ArgumentNullException(nameof(facet));");
        sb.AppendLine();
        var toSourceConfig = registration.Configuration.ToSourceTypeName;
        var resultKeyword = toSourceConfig is null ? "return new" : "var target = new";

        sb.AppendLine($"        {resultKeyword} {registration.SourceTypeName}");
        sb.AppendLine("        {");

        foreach (var member in reversible)
            sb.AppendLine($"            {member.Name} = facet.{member.Name},");

        sb.AppendLine("        };");

        if (toSourceConfig is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"        {toSourceConfig}.Map(facet, target);");
            sb.AppendLine();
            sb.AppendLine("        return target;");
        }

        sb.AppendLine("    }");

        return sb.ToString();
    }

    /// <summary>
    /// Emits the update-shaped reverse mapping: the mapped members written onto a source instance
    /// the caller already has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the reverse direction that can handle a nested member, and the reason is identity.
    /// <c>To{Source}</c> constructs, so a nested facet would have to become a brand-new child
    /// object — which an ORM reads as an insert, with the child that was there before left an
    /// orphan. Writing onto the instance already on the target changes its values and nothing else.
    /// </para>
    /// <para>
    /// A nested member is therefore updated only where the target already has one; a null stays
    /// null rather than being filled in, because creating the child is a decision about the
    /// aggregate, not about mapping. Collections are not touched at all: reconciling one needs a
    /// key and a policy for added and removed elements. <c>ToSourceConfiguration</c> runs last and
    /// is where either belongs.
    /// </para>
    /// </remarks>
    private static string BuildApplyTo(
        MapperRegistration registration,
        Dictionary<string, MapperRegistration> lookup)
    {
        var skipped = registration.ReverseSkippedMembers.Select(m => m.Name).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Writes this <see cref=\"{XmlRef(registration.FacetTypeName)}\"/> onto an existing");
        sb.AppendLine($"    /// <see cref=\"{XmlRef(registration.SourceTypeName)}\"/>, leaving its identity intact.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// Prefer this to <c>To" + registration.SourceSimpleName + "</c> when updating something that already");
        sb.AppendLine("    /// exists: it assigns to the instance you pass rather than constructing a replacement, so a");
        sb.AppendLine("    /// change-tracked entity stays the entity it was.");

        if (skipped.Count > 0)
        {
            sb.AppendLine("    /// <para>");
            sb.AppendLine($"    /// Collections are never reconciled here. Left untouched: {string.Join(", ", skipped)}.");
            sb.AppendLine("    /// </para>");
        }

        sb.AppendLine("    /// </remarks>");
        sb.AppendLine("    /// <param name=\"facet\">The values to write.</param>");
        sb.AppendLine("    /// <param name=\"target\">The instance to write them onto.</param>");
        sb.AppendLine("    /// <returns><paramref name=\"target\"/>, for chaining.</returns>");
        sb.AppendLine("    /// <exception cref=\"ArgumentNullException\">Either argument is null.</exception>");
        sb.AppendLine($"    public static {registration.SourceTypeName} ApplyTo(");
        sb.AppendLine($"        this {registration.FacetTypeName} facet,");
        sb.AppendLine($"        {registration.SourceTypeName} target)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (facet is null) throw new ArgumentNullException(nameof(facet));");
        sb.AppendLine("        if (target is null) throw new ArgumentNullException(nameof(target));");
        sb.AppendLine();

        foreach (var member in registration.ReverseMappableMembers)
            sb.AppendLine($"        target.{member.Name} = facet.{member.Name};");

        foreach (var member in registration.Members.Where(m => m.Kind == MappedMemberKind.NestedFacet))
            AppendNestedApply(sb, member, lookup, new HashSet<string> { registration.PairKey }, depth: 0);

        if (registration.Configuration.ToSourceTypeName is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"        {registration.Configuration.ToSourceTypeName}.Map(facet, target);");
        }

        sb.AppendLine();
        sb.AppendLine("        return target;");
        sb.AppendLine("    }");

        return sb.ToString();
    }

    /// <summary>
    /// Writes a nested facet onto the nested instance the target already holds, recursing while
    /// both sides have something to write to.
    /// </summary>
    private static void AppendNestedApply(
        StringBuilder sb,
        MappedMember member,
        Dictionary<string, MapperRegistration> lookup,
        HashSet<string> path,
        int depth,
        string facetPath = "facet",
        string targetPath = "target")
    {
        var key = MapperRegistration.MakePairKey(member.NestedSourceTypeName!, member.NestedFacetTypeName!);

        if (depth >= FacetConstants.DefaultMaxMapperInlineDepth || path.Contains(key) || !lookup.TryGetValue(key, out var nested))
            return;

        var reversible = nested.ReverseMappableMembers.ToList();
        var nestedFacet = $"{facetPath}.{member.Name}";
        var nestedTarget = $"{targetPath}.{member.Name}";
        var nestedChildren = nested.Members.Where(m => m.Kind == MappedMemberKind.NestedFacet).ToList();

        if (reversible.Count == 0 && nestedChildren.Count == 0)
            return;

        var indent = new string(' ', 8 + (depth * 4));

        sb.AppendLine();
        sb.AppendLine($"{indent}// Updated in place: replacing it would detach the instance already there.");
        sb.AppendLine($"{indent}if ({nestedFacet} is not null && {nestedTarget} is not null)");
        sb.AppendLine($"{indent}{{");

        foreach (var nestedMember in reversible)
            sb.AppendLine($"{indent}    {nestedTarget}.{nestedMember.Name} = {nestedFacet}.{nestedMember.Name};");

        path.Add(key);
        try
        {
            foreach (var child in nestedChildren)
                AppendNestedApply(sb, child, lookup, path, depth + 1, nestedFacet, nestedTarget);
        }
        finally
        {
            path.Remove(key);
        }

        sb.AppendLine($"{indent}}}");
    }

    /// <summary>
    /// Builds <c>new Facet { ... }</c> against <paramref name="receiver"/>, inlining nested facet
    /// initializers so the result stays a single expression an ORM can translate.
    /// </summary>
    private static string BuildInitializer(
        MapperRegistration registration,
        string receiver,
        Dictionary<string, MapperRegistration> lookup,
        HashSet<string> path,
        int depth,
        string indent)
    {
        var sb = new StringBuilder();
        var memberIndent = indent + "    ";

        sb.AppendLine($"new {registration.FacetTypeName}");
        sb.AppendLine($"{indent}{{");

        foreach (var member in registration.Members)
        {
            var value = BuildMemberValue(member, receiver, lookup, path, depth, memberIndent);
            if (value is null)
                continue;

            sb.AppendLine($"{memberIndent}{member.Name} = {value},");
        }

        sb.Append($"{indent}}}");

        return sb.ToString();
    }

    /// <summary>
    /// The right-hand side for one member, or null when it cannot be emitted at this depth
    /// (a cycle in the facet graph, or past <see cref="FacetConstants.DefaultMaxMapperInlineDepth"/>) and is left at its default.
    /// </summary>
    private static string? BuildMemberValue(
        MappedMember member,
        string receiver,
        Dictionary<string, MapperRegistration> lookup,
        HashSet<string> path,
        int depth,
        string indent)
    {
        var access = $"{receiver}.{member.Name}";

        if (member.Kind == MappedMemberKind.Direct)
            return access;

        var nestedKey = MapperRegistration.MakePairKey(member.NestedSourceTypeName!, member.NestedFacetTypeName!);

        if (depth >= FacetConstants.DefaultMaxMapperInlineDepth || path.Contains(nestedKey) || !lookup.TryGetValue(nestedKey, out var nested))
            return null;

        path.Add(nestedKey);
        try
        {
            if (member.Kind == MappedMemberKind.NestedFacet)
            {
                var initializer = BuildInitializer(nested, access, lookup, path, depth + 1, indent);

                return member.SourceCanBeNull
                    ? Guard(access, initializer, member.FacetTypeName)
                    : initializer;
            }

            // NestedFacetCollection. The materialising call comes from the shared helper, so the
            // set of collection shapes a mapper accepts is the same set [Facet] accepts.
            var element = $"x{depth}";
            var projected = GeneratorUtilities.WrapCollectionProjection(
                $"{access}.Select({element} => {BuildInitializer(nested, element, lookup, path, depth + 1, indent)})",
                member.CollectionWrapper!,
                member.NestedFacetTypeName);

            return member.SourceCanBeNull
                ? Guard(access, projected, member.FacetTypeName)
                : projected;
        }
        finally
        {
            path.Remove(nestedKey);
        }
    }

    /// <summary>
    /// Null-guards a nested initializer. The conditional is expression-tree legal, so this works
    /// in <c>Projection</c> as well as in the runtime mapper. When the facet property is not itself
    /// nullable, the result is null-forgiven rather than left to warn: the generated file runs under
    /// <c>#nullable enable</c>, and the alternative — propagating the source's nullability onto the
    /// facet — is the contract assembly's call to make, not this generator's.
    /// </summary>
    private static string Guard(string access, string initializer, string facetTypeName)
    {
        var conditional = $"{access} == null ? null : {initializer}";

        if (facetTypeName.EndsWith("?"))
            return conditional;

        // The null-forgiving operator has to cover the whole conditional, not just its second
        // branch: the conditional's type is nullable either way because the first branch is null,
        // so `cond ? null : x!` still warns CS8601 at the assignment.
        return $"({conditional})!";
    }

    private static void GenerateClassDocumentation(StringBuilder sb, MapperRegistration registration)
    {
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Maps between <see cref=\"{XmlRef(registration.SourceTypeName)}\"/> and");
        sb.AppendLine($"/// <see cref=\"{XmlRef(registration.FacetTypeName)}\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine("/// Generated from an <c>[assembly: FacetMapper(...)]</c> registration in this assembly, so");
        sb.AppendLine("/// that neither type's assembly needs to reference the other.");
        sb.AppendLine("/// </remarks>");
    }

    /// <summary>
    /// Turns a globally qualified type name into a cref the compiler can bind. Nullable
    /// annotations and the <c>global::</c> prefix are not valid in a cref.
    /// </summary>
    private static string XmlRef(string globalTypeName)
    {
        var name = GeneratorUtilities.StripGlobalPrefix(globalTypeName);

        return name.EndsWith("?") ? name.Substring(0, name.Length - 1) : name;
    }

}
