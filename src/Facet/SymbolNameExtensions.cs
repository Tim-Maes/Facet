namespace Facet;

public static class SymbolNameExtensions
{
    public static string GetSafeName(this string symbol)
    {
        return symbol
            .Replace("global::", "")
            .Replace("<", "_")
            .Replace(">", "_"); ;
    }
}
