using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Facet.Extensions.EFCore.Tasks;

/// <summary>
/// MSBuild task that exports EF Core model metadata to JSON for source generation.
/// </summary>
public sealed class ExportEfModelTask : Task
{
    /// <summary>
    /// Path to the assembly containing DbContext types.
    /// </summary>
    [Required]
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of DbContext type names to export. If empty, exports all found contexts.
    /// </summary>
    public string? ContextTypes { get; set; }

    /// <summary>
    /// Output path for the efmodel.json file.
    /// </summary>
    [Required]
    public string OutputPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        try
        {
            if (!File.Exists(AssemblyPath))
            {
                Log.LogMessage(MessageImportance.Low, $"Assembly not found at {AssemblyPath}, skipping EF model export");
                return true; // Don't fail the build if the assembly doesn't exist yet
            }

            var alc = new AssemblyLoadContext("Facet-EF-Exporter", isCollectible: true);
            try
            {
                var assembly = alc.LoadFromAssemblyPath(AssemblyPath);

                var contextTypes = ResolveDbContextTypes(assembly, ContextTypes);
                var contexts = new List<object>();

            foreach (var contextType in contextTypes)
            {
                try
                {
                    using var context = CreateDbContext(assembly, contextType);
                    if (context == null)
                    {
                        Log.LogWarning($"Could not create instance of DbContext: {contextType.FullName}");
                        continue;
                    }

                    var model = context.Model;
                    var contextData = new
                    {
                        Context = contextType.FullName ?? contextType.Name,
                        Entities = model.GetEntityTypes().Select(entityType => new
                        {
                            Name = entityType.Name,
                            Clr = entityType.ClrType?.FullName ?? entityType.Name,
                            Keys = entityType.GetKeys().Select(key => 
                                key.Properties.Select(prop => prop.Name).ToArray()).ToArray(),
                            Navigations = entityType.GetNavigations().Select(nav => new
                            {
                                Name = nav.Name,
                                Target = nav.TargetEntityType.ClrType?.FullName ?? nav.TargetEntityType.Name,
                                IsCollection = nav.IsCollection
                            }).ToArray()
                        }).ToArray()
                    };

                    contexts.Add(contextData);
                    Log.LogMessage(MessageImportance.Low, $"Exported EF model for context: {contextType.FullName}");
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Failed to export model for context {contextType.FullName}: {ex.Message}");
                }
            }

            var rootModel = new { Contexts = contexts };
            var json = JsonSerializer.Serialize(rootModel, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
            File.WriteAllText(OutputPath, json);

                Log.LogMessage(MessageImportance.Low, $"EF model exported to: {OutputPath}");
                return true;
            }
            finally
            {
                alc?.Unload();
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to export EF model: {ex}");
            return false;
        }
    }

    private static IEnumerable<Type> ResolveDbContextTypes(Assembly assembly, string? contextTypeNames)
    {
        var allContextTypes = assembly.GetTypes()
            .Where(type => typeof(DbContext).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();

        if (string.IsNullOrWhiteSpace(contextTypeNames))
        {
            return allContextTypes;
        }

        var requestedNames = contextTypeNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return allContextTypes.Where(type => 
            requestedNames.Contains(type.Name) || 
            requestedNames.Contains(type.FullName ?? string.Empty));
    }

    private static DbContext? CreateDbContext(Assembly assembly, Type contextType)
    {
        // Try IDesignTimeDbContextFactory<T> first
        var factoryInterfaceType = typeof(IDesignTimeDbContextFactory<>).MakeGenericType(contextType);
        var factoryType = assembly.GetTypes()
            .FirstOrDefault(type => factoryInterfaceType.IsAssignableFrom(type) && !type.IsAbstract);

        if (factoryType != null)
        {
            try
            {
                var factory = Activator.CreateInstance(factoryType);
                var createMethod = factoryInterfaceType.GetMethod("CreateDbContext");
                return (DbContext?)createMethod?.Invoke(factory, new object[] { Array.Empty<string>() });
            }
            catch
            {
                // Fall through to next approach
            }
        }

        // Try parameterless constructor
        try
        {
            return (DbContext?)Activator.CreateInstance(contextType);
        }
        catch
        {
            // Fall through to next approach
        }

        // Try with InMemoryDatabase options
        try
        {
            var builderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType);
            var builder = Activator.CreateInstance(builderType);
            
            // Use reflection to call UseInMemoryDatabase extension method
            var inMemoryAssembly = System.Reflection.Assembly.Load("Microsoft.EntityFrameworkCore.InMemory");
            var extensionsType = inMemoryAssembly?.GetType("Microsoft.EntityFrameworkCore.InMemoryDbContextOptionsExtensions");
            
            if (extensionsType != null)
            {
                var useInMemoryMethod = extensionsType.GetMethods()
                    .FirstOrDefault(m => m.Name == "UseInMemoryDatabase" && 
                                   m.GetParameters().Length == 2 &&
                                   m.GetParameters()[1].ParameterType == typeof(string));

                if (useInMemoryMethod != null)
                {
                    useInMemoryMethod.Invoke(null, new[] { builder, "FacetDesignTime" });

                    var optionsProperty = builderType.GetProperty("Options");
                    var options = optionsProperty?.GetValue(builder);

                    if (options != null)
                    {
                        return (DbContext?)Activator.CreateInstance(contextType, options);
                    }
                }
            }
        }
        catch
        {
            // Final fallback failed
        }

        return null;
    }
}