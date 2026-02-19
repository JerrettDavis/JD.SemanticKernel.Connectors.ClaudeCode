using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace GherkinGenerator.Plugins;

/// <summary>
/// Semantic Kernel plugin for writing generated Gherkin feature files to disk.
/// All paths are validated to stay within the designated output directory.
/// </summary>
public sealed class GherkinWriterPlugin(string allowedOutputDir)
{
    private readonly string _root =
        Path.GetFullPath(allowedOutputDir);

    [KernelFunction("write_feature_file")]
    [Description("Writes a Gherkin feature file to disk. Returns the path.")]
    public string WriteFeatureFile(
        [Description("File name for the feature (e.g. 'login.feature')")] string fileName,
        [Description("Full Gherkin feature content to write")] string content)
    {
        try
        {
            if (!fileName.EndsWith(".feature", StringComparison.OrdinalIgnoreCase))
                fileName += ".feature";

            var fullPath = ResolveSafePath(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);

            return $"Feature file written successfully to: {fullPath}";
        }
        catch (Exception ex)
        {
            return $"Error writing feature file: {ex.Message}";
        }
    }

    [KernelFunction("append_to_feature_file")]
    [Description("Appends scenarios to an existing .feature file.")]
    public string AppendToFeatureFile(
        [Description("Relative path to the .feature file")] string filePath,
        [Description("Gherkin scenario(s) to append")] string scenarioContent)
    {
        try
        {
            var fullPath = ResolveSafePath(filePath);
            if (!File.Exists(fullPath))
                return $"Error: Feature file not found at '{filePath}'.";

            var existing = File.ReadAllText(fullPath);

            if (!existing.EndsWith("\n\n", StringComparison.Ordinal))
                existing = existing.TrimEnd() + "\n\n";

            File.WriteAllText(
                fullPath,
                existing + scenarioContent.TrimEnd() + "\n");

            return $"Scenarios appended successfully to: {fullPath}";
        }
        catch (Exception ex)
        {
            return $"Error appending to feature file: {ex.Message}";
        }
    }

    [KernelFunction("get_gherkin_template")]
    [Description("Returns a Gherkin feature file template.")]
    public static string GetGherkinTemplate() =>
        """
        @tag1 @tag2
        Feature: Feature Name
          As a [role]
          I want [capability]
          So that [benefit]

          Background:
            Given some shared precondition

          @scenario-tag
          Scenario: Scenario Name
            Given some precondition
            And another precondition
            When some action is performed
            And another action
            Then expected outcome
            But not this outcome

          Scenario Outline: Parameterized Scenario
            Given a user with role <role>
            When they access <page>
            Then they should see <result>

            Examples:
              | role    | page     | result          |
              | admin   | settings | admin panel     |
              | viewer  | settings | read-only view  |
        """;

    private string ResolveSafePath(string relativePath)
    {
        var full = Path.GetFullPath(
            Path.Combine(_root, relativePath));

        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                "Path traversal detected — access denied.");

        return full;
    }
}
