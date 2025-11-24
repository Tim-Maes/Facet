using Facet.Generators.Shared;

namespace Facet;

public static class SymbolNameExtensions
{
    public static string GetSafeName(this string symbol)
    {
        return GeneratorUtilities.StripGlobalPrefix(symbol)
            .Replace("<", "_")
            .Replace(">", "_");
    }
}
