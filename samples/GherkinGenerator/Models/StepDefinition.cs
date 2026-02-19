namespace GherkinGenerator.Models;

/// <summary>
/// Represents a step definition found in a scanned assembly.
/// </summary>
/// <param name="Keyword">The Gherkin keyword (Given, When, Then, And, But).</param>
/// <param name="Pattern">The regex or cucumber-expression pattern for matching.</param>
/// <param name="MethodName">The fully qualified method name implementing this step.</param>
/// <param name="SourceType">The full type name containing the step definition.</param>
public sealed record StepDefinition(
    string Keyword,
    string Pattern,
    string MethodName,
    string SourceType);
