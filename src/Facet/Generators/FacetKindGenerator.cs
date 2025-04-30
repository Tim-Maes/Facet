using System;
using System.Linq;
using System.Reflection;
using Facet.Util;
using Microsoft.CodeAnalysis;

namespace Facet.Generators;

[Generator]
public class FacetKindGenerator : IIncrementalGenerator {

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        context.RegisterPostInitializationOutput(spc => {
            spc.AddSource(
                hintName: "FacetKind.g.cs",
                source:
                $$"""
                namespace {{FacetConstants.DefaultNamespace}}
                {
                    internal enum FacetKind
                    {
                        Class = 0,
                        Record = 1,
                    }
                }
                """);
        });
    }

}