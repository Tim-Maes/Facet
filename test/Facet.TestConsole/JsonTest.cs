using System;
using System.IO;
using System.Reflection;
using Facet.Extensions.EFCore.Tasks;
using Facet.TestConsole.Data;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging.Abstractions;

class JsonTest
{
    static void Main()
    {
        Console.WriteLine("=== Direct JSON Export Test ===");
        
        var tempJsonFile = Path.Combine(Path.GetTempPath(), "direct_test_efmodel.json");
        var testAssemblyPath = Assembly.GetAssembly(typeof(FacetTestDbContext))?.Location;
        
        if (string.IsNullOrEmpty(testAssemblyPath))
        {
            Console.WriteLine("Could not determine test assembly path");
            return;
        }

        Console.WriteLine($"Using assembly: {testAssemblyPath}");

        var exportTask = new ExportEfModelTask
        {
            AssemblyPath = testAssemblyPath,
            ContextTypes = "Facet.TestConsole.Data.FacetTestDbContext",
            OutputPath = tempJsonFile
        };

        exportTask.BuildEngine = new SimpleBuildEngine();

        Console.WriteLine("Executing export...");
        var success = exportTask.Execute();
        
        Console.WriteLine($"Export successful: {success}");

        if (File.Exists(tempJsonFile))
        {
            var jsonContent = File.ReadAllText(tempJsonFile);
            Console.WriteLine($"JSON file size: {jsonContent.Length} bytes");
            Console.WriteLine("JSON Content:");
            Console.WriteLine("=====================================");
            Console.WriteLine(jsonContent);
            Console.WriteLine("=====================================");
            
            File.Delete(tempJsonFile);
        }
        else
        {
            Console.WriteLine("JSON file was not created");
        }
    }
}

class SimpleBuildEngine : IBuildEngine
{
    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => "Test";

    public bool BuildProjectFile(string projectFileName, string[] targetNames, System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs)
        => true;

    public void LogCustomEvent(CustomBuildEventArgs e)
        => Console.WriteLine($"Custom: {e.Message}");

    public void LogErrorEvent(BuildErrorEventArgs e)
        => Console.WriteLine($"Error: {e.Message}");

    public void LogMessageEvent(BuildMessageEventArgs e)
        => Console.WriteLine($"Message: {e.Message}");

    public void LogWarningEvent(BuildWarningEventArgs e)
        => Console.WriteLine($"Warning: {e.Message}");
}