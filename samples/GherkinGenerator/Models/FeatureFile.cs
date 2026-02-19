namespace GherkinGenerator.Models;

/// <summary>
/// Represents an existing Gherkin feature file on disk.
/// </summary>
/// <param name="FilePath">Absolute path to the .feature file.</param>
/// <param name="FeatureName">The Feature: title extracted from the file.</param>
/// <param name="Content">The full text content of the feature file.</param>
/// <param name="Tags">Any @tags declared on the feature.</param>
public sealed record FeatureFile(
    string FilePath,
    string FeatureName,
    string Content,
    string[] Tags);
