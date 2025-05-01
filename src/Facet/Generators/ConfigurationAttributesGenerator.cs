using System;
using System.Linq;
using System.Reflection;
using Facet.Util;
using Microsoft.CodeAnalysis;

namespace Facet.Generators;

[Generator]
public class ConfigurationAttributesGenerator : IIncrementalGenerator {

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        // Get the attribute types to generate from this assembly. In this assembly, they are simply DTOs.
        // In the target assembly, they will be generated as attributes.
        var attributeTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.GetCustomAttribute<GenerateAttributeAttribute>() is not null)
            .ToList();

        foreach (var attrType in attributeTypes) {
            context.GenerateAttribute(attrType);
        }
    }

}

/// <summary>
/// This attribute tells the attribute generation code that a DTO should be generated into the target project as an attribute
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class GenerateAttributeAttribute : Attribute {

    public GenerateAttributeAttribute(AttributeTargets validOn) {
        ValidOn = validOn;
    }

    public bool AllowMultiple { get; set; }
    public bool Inherited { get; set; }
    public AttributeTargets ValidOn { get; }

}

public class GenerateCommentAttribute : Attribute {

    public string Content { get; }

    public GenerateCommentAttribute(string content) {
        Content = content;
    }

}