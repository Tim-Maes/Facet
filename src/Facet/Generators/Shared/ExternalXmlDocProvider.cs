using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#pragma warning disable RS1035 // File IO is intentionally used to read XML documentation from external assemblies

namespace Facet.Generators.Shared;

/// <summary>
/// Provides XML documentation for symbols from external assemblies (metadata references)
/// by reading the XML documentation files alongside the referenced DLLs.
/// This is needed because <see cref="ISymbol.GetDocumentationCommentXml"/> returns empty
/// when the documentation provider is not set up on the metadata reference (e.g., when
/// the referenced project does not have GenerateDocumentationFile enabled, but an XML
/// file exists at a discoverable path).
/// </summary>
internal sealed class ExternalXmlDocProvider
{
    private readonly Compilation _compilation;
    private readonly Dictionary<string, Dictionary<string, string>?> _assemblyDocCache = new();

    public ExternalXmlDocProvider(Compilation compilation)
    {
        _compilation = compilation;
    }

    /// <summary>
    /// Attempts to get XML documentation for a symbol from external assembly XML files.
    /// Returns the raw XML content for the member, or null if not found.
    /// </summary>
    public string? GetDocumentationForSymbol(ISymbol symbol)
    {
        var containingAssembly = symbol.ContainingAssembly;
        if (containingAssembly == null)
            return null;

        if (SymbolEqualityComparer.Default.Equals(containingAssembly, _compilation.Assembly))
            return null;

        var docs = GetOrLoadAssemblyDocs(containingAssembly);
        if (docs == null)
            return null;

        var memberId = symbol.GetDocumentationCommentId();
        if (memberId == null)
            return null;

        return docs.TryGetValue(memberId, out var xml) ? xml : null;
    }

    private Dictionary<string, string>? GetOrLoadAssemblyDocs(IAssemblySymbol assembly)
    {
        var assemblyName = assembly.Identity.Name;
        if (_assemblyDocCache.TryGetValue(assemblyName, out var cached))
            return cached;

        var docs = TryLoadDocsForAssembly(assembly);
        _assemblyDocCache[assemblyName] = docs;
        return docs;
    }

    private Dictionary<string, string>? TryLoadDocsForAssembly(IAssemblySymbol assembly)
    {
        var reference = _compilation.GetMetadataReference(assembly) as PortableExecutableReference;
        if (reference?.FilePath == null)
            return null;

        var xmlPath = Path.ChangeExtension(reference.FilePath, ".xml");
        if (File.Exists(xmlPath))
            return ParseXmlDocFile(xmlPath);

        var directory = Path.GetDirectoryName(reference.FilePath);
        if (directory != null)
        {
            var dirName = Path.GetFileName(directory);
            if (string.Equals(dirName, "ref", StringComparison.OrdinalIgnoreCase))
            {
                var parentDir = Path.GetDirectoryName(directory);
                if (parentDir != null)
                {
                    var parentXmlPath = Path.Combine(parentDir, Path.GetFileNameWithoutExtension(reference.FilePath) + ".xml");
                    if (File.Exists(parentXmlPath))
                        return ParseXmlDocFile(parentXmlPath);
                }
            }
        }

        return null;
    }

    private static Dictionary<string, string>? ParseXmlDocFile(string xmlPath)
    {
        try
        {
            var docs = new Dictionary<string, string>(StringComparer.Ordinal);
            var doc = System.Xml.Linq.XDocument.Load(xmlPath);
            var members = doc.Root?.Element("members");
            if (members == null)
                return null;

            foreach (var member in members.Elements("member"))
            {
                var name = member.Attribute("name")?.Value;
                if (name == null)
                    continue;

                docs[name] = member.ToString();
            }

            return docs;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
