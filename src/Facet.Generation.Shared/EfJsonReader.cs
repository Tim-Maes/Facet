using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;

namespace Facet.Extensions.EFCore.Generators;

/// <summary>
/// Reads EF model metadata from efmodel.json files included as AdditionalFiles.
/// </summary>
public static class EfJsonReader
{
  /// <summary>
  /// Configures the incremental provider to read EF model from JSON files.
  /// </summary>
  public static IncrementalValueProvider<ModelRoot?> Configure(IncrementalGeneratorInitializationContext context)
  {
    return context.AdditionalTextsProvider
      .Where(static file => Path.GetFileName(file.Path).Equals("efmodel.json", StringComparison.OrdinalIgnoreCase))
      .Select(static (file, cancellationToken) =>
      {
        try
        {
          var content = file.GetText(cancellationToken);
          if (content == null)
            return null;

          var json = content.ToString();
          if (string.IsNullOrWhiteSpace(json))
            return null;

          var settings = new JsonSerializerSettings
          {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
          };

          return JsonConvert.DeserializeObject<ModelRoot>(json, settings);
        }
        catch (JsonException jsonEx)
        {
          // Specific JSON parsing error - provide detailed context
          throw new InvalidOperationException(
            $"Failed to parse EF model JSON from '{file.Path}': {jsonEx.Message}. " +
            "Ensure the efmodel.json file contains valid JSON and was generated correctly.",
            jsonEx);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
          // Other errors during file processing
          throw new InvalidOperationException(
            $"Failed to read EF model from '{file.Path}': {ex.Message}. " +
            "This will cause MSBuild to fail - no silent fallbacks.",
            ex);
        }
      })
      .Where(static model => model != null)
      .Select(static (model, _) => model!)
      .Collect()
      .Select(static (models, _) => models.FirstOrDefault());
  }
}