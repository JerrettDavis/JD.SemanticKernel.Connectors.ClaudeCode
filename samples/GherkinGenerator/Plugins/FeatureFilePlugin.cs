using System.ComponentModel;
using System.Text.RegularExpressions;
using GherkinGenerator.Models;
using Microsoft.SemanticKernel;

namespace GherkinGenerator.Plugins;

/// <summary>
/// Semantic Kernel plugin for reading and listing existing Gherkin .feature files.
/// </summary>
public sealed class FeatureFilePlugin
{
    private static readonly Regex FeatureNameRegex = new(
        @"^\s*Feature:\s*(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new(
        @"^\s*(@\S+)", RegexOptions.Multiline | RegexOptions.Compiled);

    [KernelFunction("list_feature_files")]
    [Description("Lists all .feature files found under the specified directory, returning their paths and feature names.")]
    public string ListFeatureFiles(
        [Description("Directory path to search for .feature files")] string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return $"Error: Directory not found at '{directoryPath}'.";

        var files = Directory.GetFiles(directoryPath, "*.feature", SearchOption.AllDirectories);

        if (files.Length == 0)
            return "No .feature files found in the directory.";

        var result = $"Found {files.Length} feature file(s):\n\n";
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var nameMatch = FeatureNameRegex.Match(content);
            var featureName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "(unnamed)";
            result += $"  {Path.GetRelativePath(directoryPath, file)} — {featureName}\n";
        }

        return result;
    }

    [KernelFunction("read_feature_file")]
    [Description("Reads the full content of a specific .feature file, returning its feature name, tags, and all scenarios.")]
    public string ReadFeatureFile(
        [Description("Absolute path to the .feature file to read")] string filePath)
    {
        if (!File.Exists(filePath))
            return $"Error: Feature file not found at '{filePath}'.";

        var content = File.ReadAllText(filePath);
        var parsed = ParseFeatureFile(filePath, content);

        return $"""
            File: {parsed.FilePath}
            Feature: {parsed.FeatureName}
            Tags: {(parsed.Tags.Length > 0 ? string.Join(", ", parsed.Tags) : "(none)")}

            --- Content ---
            {parsed.Content}
            """;
    }

    private static FeatureFile ParseFeatureFile(string path, string content)
    {
        var nameMatch = FeatureNameRegex.Match(content);
        var featureName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "(unnamed)";
        var tags = TagRegex.Matches(content)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToArray();

        return new FeatureFile(path, featureName, content, tags);
    }
}
