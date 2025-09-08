using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace Facet.Extensions.EFCore.Generators;

/// <summary>
/// Reads EF model JSON files produced by the MSBuild task.
/// </summary>
internal static class EfJsonReader
{
    public static IncrementalValueProvider<ModelRoot?> Configure(IncrementalGeneratorInitializationContext context)
    {
        return context.AdditionalTextsProvider
            .Where(static file => Path.GetFileName(file.Path).Equals("efmodel.json", System.StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => 
            {
                var text = file.GetText(cancellationToken);
                if (text == null) return null;

                var json = text.ToString();
                if (string.IsNullOrWhiteSpace(json)) return null;

                try
                {
                    return JsonSerializer.Deserialize<ModelRoot>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch
                {
                    return null;
                }
            })
            .Where(static model => model != null)
            .Collect()
            .Select(static (models, _) => models.FirstOrDefault());
    }
}