using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;

namespace Facet.Extensions.EFCore.Generators.Shared;

/// <summary>
/// Reads EF model metadata from JSON files.
/// </summary>
public static class EfJsonReader
{
    public static IncrementalValueProvider<ModelRoot?> Configure(IncrementalGeneratorInitializationContext context)
    {
        return context.AdditionalTextsProvider
            .Where(static file => Path.GetFileName(file.Path).Equals("efmodel.json", System.StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) =>
            {
                try
                {
                    var content = file.GetText(cancellationToken)?.ToString();
                    if (string.IsNullOrEmpty(content))
                        return null;

                    return JsonConvert.DeserializeObject<ModelRoot>(content);
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