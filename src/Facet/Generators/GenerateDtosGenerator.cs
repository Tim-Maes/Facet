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
  /// <summary>
  /// Gets the type name with appropriate nullability annotation for code generation.
  /// Handles the dual representation of nullable types in Roslyn's SymbolDisplayFormat.FullyQualifiedFormat:
  /// - Value types (int?, DateTime?): Include '?' in the display string
  /// - Reference types (string?, object?): Rely on NullableAnnotation metadata, display string omits '?'
  /// </summary>
  /// <param name="typeSymbol">The type symbol to get the nullable representation for</param>
  /// <returns>A string representation of the type with appropriate nullable annotation</returns>
  private static string GetFullyQualifiedName(ITypeSymbol typeSymbol)
  {
    if (typeSymbol == null)
    {
      return "object"; // Safe fallback
    }

    // Handle special types for optimization and correctness - inspired by AArnott's approach
    if (typeSymbol.SpecialType != SpecialType.None)
    {
      return typeSymbol.SpecialType switch
      {
        SpecialType.System_String => "System.String",
        SpecialType.System_Int32 => "System.Int32", 
        SpecialType.System_Boolean => "System.Boolean",
        SpecialType.System_Object => "System.Object",
        SpecialType.System_Byte => "System.Byte",
        SpecialType.System_SByte => "System.SByte",
        SpecialType.System_Int16 => "System.Int16",
        SpecialType.System_UInt16 => "System.UInt16",
        SpecialType.System_UInt32 => "System.UInt32",
        SpecialType.System_Int64 => "System.Int64",
        SpecialType.System_UInt64 => "System.UInt64",
        SpecialType.System_Single => "System.Single",
        SpecialType.System_Double => "System.Double",
        SpecialType.System_Decimal => "System.Decimal",
        SpecialType.System_Char => "System.Char",
        SpecialType.System_DateTime => "System.DateTime",
        SpecialType.System_Void => "void",
        _ => BuildQualifiedName(typeSymbol)
      };
    }

    return BuildQualifiedName(typeSymbol);
  }

  private static string BuildQualifiedName(ITypeSymbol typeSymbol)
  {
    // Handle array types - similar to AArnott's approach
    if (typeSymbol is IArrayTypeSymbol arrayType)
    {
      var elementType = GetFullyQualifiedName(arrayType.ElementType);
      var rankSpecifiers = new string(Enumerable.Repeat(',', arrayType.Rank - 1).ToArray());
      return $"{elementType}[{rankSpecifiers}]";
    }

    // Handle pointer types
    if (typeSymbol is IPointerTypeSymbol pointerType)
    {
      return GetFullyQualifiedName(pointerType.PointedAtType) + "*";
    }

    // Handle generic types recursively - improved version inspired by AArnott
    if (typeSymbol is INamedTypeSymbol namedType)
    {
      var containingSymbol = namedType.ContainingType ?? (INamespaceOrTypeSymbol)namedType.ContainingNamespace;
      var prefix = GetContainingSymbolName(containingSymbol);
      
      if (namedType.IsGenericType && namedType.TypeArguments.Length > 0)
      {
        var typeArguments = string.Join(", ", namedType.TypeArguments.Select(GetFullyQualifiedName));
        var nameWithoutArity = namedType.Name;
        return string.IsNullOrEmpty(prefix) 
          ? $"{nameWithoutArity}<{typeArguments}>"
          : $"{prefix}.{nameWithoutArity}<{typeArguments}>";
      }
      else
      {
        return string.IsNullOrEmpty(prefix) 
          ? namedType.Name 
          : $"{prefix}.{namedType.Name}";
      }
    }

    // Handle error types gracefully
    if (typeSymbol is IErrorTypeSymbol)
    {
      return "object"; // Safe fallback instead of "Unknown.Type" 
    }

    // Final fallback - should rarely be reached
    return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
  }

  private static string GetContainingSymbolName(INamespaceOrTypeSymbol symbol)
  {
    if (symbol == null || (symbol is INamespaceSymbol ns && ns.IsGlobalNamespace))
    {
      return string.Empty;
    }

    var parentName = GetContainingSymbolName(symbol.ContainingNamespace);
    var currentName = symbol.Name;
    
    if (string.IsNullOrEmpty(parentName))
    {
      return currentName;
    }
    
    return $"{parentName}.{currentName}";
  }

  internal static string GetTypeWithNullability(ITypeSymbol typeSymbol)
  {
    var baseType = GetFullyQualifiedName(typeSymbol);
    
    // For nullable value types (int?, DateTime?, etc.), check if it's a nullable value type
    if (typeSymbol is INamedTypeSymbol { IsValueType: true, OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
    {
      // For nullable value types, extract the underlying type and add '?'
      var underlyingType = ((INamedTypeSymbol)typeSymbol).TypeArguments[0];
      return GetFullyQualifiedName(underlyingType) + "?";
    }
    
    // For nullable reference types, check the NullableAnnotation metadata
    if (typeSymbol is { IsReferenceType: true } && typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
    {
      return baseType + "?";
    }
    
    // Non-nullable types or types where nullability is not explicitly annotated
    return baseType;
  }
  private const string GenerateDtosAttributeName = "Facet.GenerateDtosAttribute";
  private const string GenerateAuditableDtosAttributeName = "Facet.GenerateAuditableDtosAttribute";

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

    context.RegisterSourceOutput(allTargets, static (spc, models) =>
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
          catch (Exception ex) when (ex is not OperationCanceledException)
          {
            spc.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.GenerateDtosError,
                Location.None,
                ex.Message));
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

      // InterfaceContracts handling
      var interfaceContracts = new List<string>();
      var interfaceContractsArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "InterfaceContracts");
      if (interfaceContractsArg.Value.Kind != TypedConstantKind.Error && !interfaceContractsArg.Value.IsNull)
      {
        if (interfaceContractsArg.Value.Kind == TypedConstantKind.Array)
        {
          interfaceContracts.AddRange(
              interfaceContractsArg.Value.Values
                  .Where(v => v.Value is INamedTypeSymbol)
                  .Select(v => ((INamedTypeSymbol)v.Value!).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }
        else if (interfaceContractsArg.Value.Value is INamedTypeSymbol singleContract)
        {
          interfaceContracts.Add(singleContract.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
      }

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

      // ExcludeMembersFromType handling
      var excludeMembersFromType = new List<string>();
      var excludeMembersFromTypeArg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "ExcludeMembersFromType");
      if (excludeMembersFromTypeArg.Value.Kind != TypedConstantKind.Error && !excludeMembersFromTypeArg.Value.IsNull)
      {
        if (excludeMembersFromTypeArg.Value.Kind == TypedConstantKind.Array)
        {
          excludeMembersFromType.AddRange(
              excludeMembersFromTypeArg.Value.Values
                  .Where(v => v.Value is INamedTypeSymbol)
                  .Select(v => ((INamedTypeSymbol)v.Value!).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }
        else if (excludeMembersFromTypeArg.Value.Value is INamedTypeSymbol singleExcludeType)
        {
          excludeMembersFromType.Add(singleExcludeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
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

      var allMembersWithModifiers = GetAllMembersWithModifiers(sourceSymbol);

      foreach (var (member, isInitOnly, isRequired) in allMembersWithModifiers)
      {
        token.ThrowIfCancellationRequested();
        if (excludeProperties.Contains(member.Name)) continue;
        if (addedMembers.Contains(member.Name)) continue;

        // Check if member should be excluded based on declaring type
        if (excludeMembersFromType.Count > 0)
        {
          var declaringTypeFullName = member.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
          if (excludeMembersFromType.Contains(declaringTypeFullName))
            continue;
        }

        if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } p)
        {
          members.Add(new FacetMember(
              p.Name,
  GetTypeWithNullability(p.Type),
              FacetMemberKind.Property,
              isInitOnly,
              isRequired,
              null)); // No XML documentation for GenerateDtos
          addedMembers.Add(p.Name);
        }
        else if (includeFields && member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public } f)
        {
          members.Add(new FacetMember(
              f.Name,
  GetTypeWithNullability(f.Type),
              FacetMemberKind.Field,
              false, // Fields don't have init-only
              isRequired,
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
          excludeMembersFromType.ToImmutableArray(),
          members.ToImmutableArray(),
          interfaceContracts.ToImmutableArray());
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      // Store error information to be reported later in the pipeline
      // We can't directly emit diagnostics here because we don't have SourceProductionContext
      // The error will be handled by the RegisterSourceOutput exception handler
      throw new InvalidOperationException(
          $"Failed to process GenerateDtos attribute on type '{sourceSymbol.ToDisplayString()}': {ex.Message}", 
          ex);
    }
  }

  private static void GenerateDtosForModel(SourceProductionContext context, GenerateDtosTargetModel model)
  {
    context.CancellationToken.ThrowIfCancellationRequested();

    var sourceTypeName = GetSimpleTypeName(model.SourceTypeName);

    // Generate Create DTO
    if ((model.Types & DtoTypes.Create) != 0)
    {
      var createExclusions = new HashSet<string>(model.ExcludeProperties, System.StringComparer.OrdinalIgnoreCase);
      foreach (var idField in IdFieldPatterns)
      {
        createExclusions.Add(idField);
      }

      var createMembers = model.Members.Where(m => !createExclusions.Contains(m.Name)).ToImmutableArray();
      var createDtoName = BuildDtoName(sourceTypeName, "Create", "Request", model.Prefix, model.Suffix);

      var createCode = GenerateDtoCode(model, createDtoName, createMembers, "Create");
      context.AddSource($"{createDtoName}.g.cs", SourceText.From(createCode, Encoding.UTF8));
    }

    // Generate Update DTO
    if ((model.Types & DtoTypes.Update) != 0)
    {
      var updateExclusions = new HashSet<string>(model.ExcludeProperties, System.StringComparer.OrdinalIgnoreCase);
      // Don't exclude ID from Update DTOs (needed for identification)

      var updateMembers = model.Members.Where(m => !updateExclusions.Contains(m.Name)).ToImmutableArray();
      var updateDtoName = BuildDtoName(sourceTypeName, "Update", "Request", model.Prefix, model.Suffix);

      var updateCode = GenerateDtoCode(model, updateDtoName, updateMembers, "Update");
      context.AddSource($"{updateDtoName}.g.cs", SourceText.From(updateCode, Encoding.UTF8));
    }

    // Generate Upsert DTO
    if ((model.Types & DtoTypes.Upsert) != 0)
    {
      var upsertExclusions = new HashSet<string>(model.ExcludeProperties, System.StringComparer.OrdinalIgnoreCase);
      // Include ID in Upsert DTOs (can be null for create, populated for update)

      var upsertMembers = model.Members.Where(m => !upsertExclusions.Contains(m.Name)).ToImmutableArray();
      var upsertDtoName = BuildDtoName(sourceTypeName, "Upsert", "Request", model.Prefix, model.Suffix);

      var upsertCode = GenerateDtoCode(model, upsertDtoName, upsertMembers, "Upsert");
      context.AddSource($"{upsertDtoName}.g.cs", SourceText.From(upsertCode, Encoding.UTF8));
    }

    // Generate Response DTO
    if ((model.Types & DtoTypes.Response) != 0)
    {
      var responseExclusions = new HashSet<string>(model.ExcludeProperties, System.StringComparer.OrdinalIgnoreCase);
      // Include all non-excluded properties for Response DTOs

      var responseMembers = model.Members.Where(m => !responseExclusions.Contains(m.Name)).ToImmutableArray();
      var responseDtoName = BuildDtoName(sourceTypeName, "", "Response", model.Prefix, model.Suffix);

      var responseCode = GenerateDtoCode(model, responseDtoName, responseMembers, "Response");
      context.AddSource($"{responseDtoName}.g.cs", SourceText.From(responseCode, Encoding.UTF8));
    }

    // Generate Query DTO
    if ((model.Types & DtoTypes.Query) != 0)
    {
      var queryMembers = model.Members.Select(m => new FacetMember(
          m.Name,
          MakeNullable(m.TypeName),
          m.Kind,
          m.IsInitOnly,
          false)) // Make all properties optional in Query DTOs
          .ToImmutableArray();

      var queryDtoName = BuildDtoName(sourceTypeName, "", "Query", model.Prefix, model.Suffix);

      var queryCode = GenerateDtoCode(model, queryDtoName, queryMembers, "Query");
      context.AddSource($"{queryDtoName}.g.cs", SourceText.From(queryCode, Encoding.UTF8));
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

  private static string GetSimpleTypeName(string fullyQualifiedName)
  {
    var parts = fullyQualifiedName.Split('.');
    return parts[parts.Length - 1];
  }

  private static string MakeNullable(string typeName)
  {
    // Don't make already nullable types more nullable
    if (typeName.EndsWith("?") || typeName.StartsWith("System.Nullable<"))
      return typeName;

    // Handle value types
    if (IsValueType(typeName))
      return typeName + "?";

    // Reference types are already nullable in modern C#
    return typeName + "?";
  }

  private static bool IsValueType(string typeName)
  {
    return typeName switch
    {
      "bool" or "System.Boolean" => true,
      "byte" or "System.Byte" => true,
      "sbyte" or "System.SByte" => true,
      "char" or "System.Char" => true,
      "decimal" or "System.Decimal" => true,
      "double" or "System.Double" => true,
      "float" or "System.Single" => true,
      "int" or "System.Int32" => true,
      "uint" or "System.UInt32" => true,
      "long" or "System.Int64" => true,
      "ulong" or "System.UInt64" => true,
      "short" or "System.Int16" => true,
      "ushort" or "System.UInt16" => true,
      "System.DateTime" => true,
      "System.DateTimeOffset" => true,
      "System.TimeSpan" => true,
      "System.Guid" => true,
      _ when typeName.StartsWith("System.Enum") => true,
      _ => false
    };
  }

  private static string GenerateDtoCode(GenerateDtosTargetModel model, string dtoName, ImmutableArray<FacetMember> members, string purpose)
  {
    var sb = new StringBuilder();
    GenerateFileHeader(sb);
    sb.AppendLine("using System;");
    sb.AppendLine("using System.Linq.Expressions;");
    sb.AppendLine();

    if (!string.IsNullOrWhiteSpace(model.TargetNamespace))
    {
      sb.AppendLine($"namespace {model.TargetNamespace};");
      sb.AppendLine();
    }

    var keyword = model.OutputType switch
    {
      OutputType.Class => "class",
      OutputType.Record => "record",
      OutputType.RecordStruct => "record struct",
      OutputType.Struct => "struct",
      _ => "record"
    };

    var sourceTypeName = GetSimpleTypeName(model.SourceTypeName);

    sb.AppendLine($"/// <summary>");
    sb.AppendLine($"/// Generated {purpose} DTO for {sourceTypeName}.");
    sb.AppendLine($"/// </summary>");

    var isPositional = model.OutputType is OutputType.Record or OutputType.RecordStruct;
    var interfaceClause = model.InterfaceContracts.Length > 0
        ? " : " + string.Join(", ", model.InterfaceContracts)
        : string.Empty;
    var hasInitOnlyProperties = members.Any(m => m.IsInitOnly);

    if (isPositional)
    {
      sb.AppendLine($"public {keyword} {dtoName}(");

      for (int i = 0; i < members.Length; i++)
      {
        var member = members[i];
        var param = $"    {member.TypeName} {member.Name}";

        if (member.IsRequired && model.OutputType == OutputType.RecordStruct)
        {
          param = $"    required {member.TypeName} {member.Name}";
        }

        // Add comma for all but the last parameter
        if (i < members.Length - 1)
        {
          param += ",";
        }

        sb.AppendLine(param);
      }

      sb.AppendLine(")" + interfaceClause + ";");
    }
    else
    {
      // Allow consumer to extend generated DTO via partials (e.g., add parameterless ctor for model binding)
      sb.AppendLine($"public partial {keyword} {dtoName}{interfaceClause}");
      sb.AppendLine("{");

      foreach (var member in members)
      {
        if (member.Kind == FacetMemberKind.Property)
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
        else
        {
          var fieldDef = $"public {member.TypeName} {member.Name};";
          if (member.IsRequired)
          {
            fieldDef = $"required {fieldDef}";
          }
          sb.AppendLine($"    {fieldDef}");
        }
      }

      // Generate constructor if requested
      if (model.GenerateConstructors)
      {
  // Parameterless constructor for model binding / serializers
  sb.AppendLine();
  sb.AppendLine($"    /// <summary>Parameterless constructor for serializers and model binding.</summary>");
  sb.AppendLine($"    public {dtoName}() {{ }}");

        sb.AppendLine();
        sb.AppendLine($"    public {dtoName}({model.SourceTypeName} source)");
        sb.AppendLine("    {");

        foreach (var member in members.Where(x => !x.IsInitOnly))
        {
          sb.AppendLine($"        this.{member.Name} = source.{member.Name};");
        }

        sb.AppendLine("    }");

        // Add static factory method for types with init-only properties
        if (hasInitOnlyProperties)
        {
          sb.AppendLine();
          sb.AppendLine($"    public static {dtoName} FromSource({model.SourceTypeName} source)");
          sb.AppendLine("    {");
          sb.AppendLine($"        return new {dtoName}");
          sb.AppendLine("        {");
          foreach (var member in members)
          {
            var comma = member == members.Last() ? "" : ",";
            sb.AppendLine($"            {member.Name} = source.{member.Name}{comma}");
          }
          sb.AppendLine("        };");
          sb.AppendLine("    }");
        }
      }

      // Generate projection if requested
      if (model.GenerateProjections)
      {
        sb.AppendLine();
        sb.AppendLine($"    public static Expression<Func<{model.SourceTypeName}, {dtoName}>> Projection =>");

        if (hasInitOnlyProperties)
        {
          sb.AppendLine($"        source => new {dtoName}");
          sb.AppendLine("        {");
          foreach (var member in members)
          {
            var comma = member == members.Last() ? "" : ",";
            sb.AppendLine($"            {member.Name} = source.{member.Name}{comma}");
          }
          sb.AppendLine("        };");
        }
        else
        {
          sb.AppendLine($"        source => new {dtoName}(source);");
        }
      }

      sb.AppendLine("}");
    }

    return sb.ToString();
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
