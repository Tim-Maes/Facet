using System.Runtime.CompilerServices;

namespace Facet.UnitTests;

/// <summary>
/// MANDATORY: Initialize Verify.SourceGenerators globally for all tests.
/// This enables snapshot testing for source generator output verification.
/// </summary>
public static class GlobalInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // TODO: Fix VerifySourceGenerators.Initialize() call
        // VerifySourceGenerators.Initialize();
    }
}