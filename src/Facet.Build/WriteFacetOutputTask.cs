using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Text;

namespace Facet.Build.Tasks
{
    /// <summary>
    /// MSBuild task to write generated facet files to specified output paths.
    /// This task is invoked by the Facet source generator infrastructure to write
    /// generated files to disk when OutputPath is specified.
    /// </summary>
    public class WriteFacetOutputTask : Task
    {
        /// <summary>
        /// The name of the generated facet.
        /// </summary>
        [Required]
        public string FacetName { get; set; } = string.Empty;

        /// <summary>
        /// The generated source code content.
        /// </summary>
        [Required]
        public string SourceCode { get; set; } = string.Empty;

        /// <summary>
        /// The output path where the file should be written.
        /// Can be a directory or a full file path.
        /// </summary>
        [Required]
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// The project directory for resolving relative paths.
        /// </summary>
        public string? ProjectDirectory { get; set; }

        public override bool Execute()
        {
            try
            {
                var outputPath = OutputPath;

                // Normalize path separators for cross-platform compatibility
                outputPath = outputPath.Replace('\\', Path.DirectorySeparatorChar)
                                      .Replace('/', Path.DirectorySeparatorChar);

                // Resolve relative paths against the project directory
                if (!Path.IsPathRooted(outputPath) && !string.IsNullOrEmpty(ProjectDirectory))
                {
                    outputPath = Path.Combine(ProjectDirectory, outputPath);
                }

                // Determine the final file path
                string finalFilePath;
                if (Directory.Exists(outputPath) || outputPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    // OutputPath is a directory, append the generated filename
                    var directory = outputPath.TrimEnd(Path.DirectorySeparatorChar);
                    finalFilePath = Path.Combine(directory, $"{FacetName}.g.cs");
                }
                else if (Path.HasExtension(outputPath))
                {
                    // OutputPath is a file path
                    finalFilePath = outputPath;
                }
                else
                {
                    // OutputPath is ambiguous, treat as directory and append filename
                    finalFilePath = Path.Combine(outputPath, $"{FacetName}.g.cs");
                }

                // Ensure the directory exists
                var targetDirectory = Path.GetDirectoryName(finalFilePath);
                if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                {
                    Log.LogMessage(MessageImportance.Normal, $"Creating directory: {targetDirectory}");
                    Directory.CreateDirectory(targetDirectory);
                }

                // Write the file with UTF-8 encoding
                Log.LogMessage(MessageImportance.Normal, $"Writing facet '{FacetName}' to: {finalFilePath}");
                File.WriteAllText(finalFilePath, SourceCode, Encoding.UTF8);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to write facet '{FacetName}' to '{OutputPath}': {ex.Message}");
                return false;
            }
        }
    }
}