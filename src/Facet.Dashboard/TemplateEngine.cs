using System.Reflection;
using System.Text;

namespace Facet.Dashboard;

/// <summary>
/// Simple template engine for rendering HTML templates with token replacement.
/// </summary>
internal static class TemplateEngine
{
    private static readonly Dictionary<string, string> _templateCache = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Loads a template from embedded resources.
    /// </summary>
    public static string LoadTemplate(string templateName)
    {
        lock (_lock)
        {
            if (_templateCache.TryGetValue(templateName, out var cached))
                return cached;

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"Facet.Dashboard.Templates.{templateName}";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException($"Template '{templateName}' not found in embedded resources.");

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            _templateCache[templateName] = content;
            return content;
        }
    }

    /// <summary>
    /// Renders a template by replacing tokens with values.
    /// </summary>
    public static string Render(string template, Dictionary<string, string> tokens)
    {
        var result = template;
        foreach (var kvp in tokens)
        {
            result = result.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        }
        return result;
    }

    /// <summary>
    /// Renders a template by name with token replacement.
    /// </summary>
    public static string RenderTemplate(string templateName, Dictionary<string, string> tokens)
    {
        var template = LoadTemplate(templateName);
        return Render(template, tokens);
    }

    /// <summary>
    /// Clears the template cache (useful for development/testing).
    /// </summary>
    public static void ClearCache()
    {
        lock (_lock)
        {
            _templateCache.Clear();
        }
    }
}
