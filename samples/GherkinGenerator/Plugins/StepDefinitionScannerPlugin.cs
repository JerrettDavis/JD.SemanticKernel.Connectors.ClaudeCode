using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using GherkinGenerator.Models;
using Microsoft.SemanticKernel;

namespace GherkinGenerator.Plugins;

/// <summary>
/// Semantic Kernel plugin that scans .NET assemblies for Reqnroll/SpecFlow step definitions.
/// Uses <see cref="MetadataLoadContext"/> for safe, reflection-only loading.
/// </summary>
public sealed class StepDefinitionScannerPlugin
{
    private static readonly string[] StepAttributes =
    [
        "GivenAttribute", "WhenAttribute", "ThenAttribute",
        "AndAttribute", "ButAttribute",
        // SpecFlow legacy names
        "Reqnroll.GivenAttribute", "Reqnroll.WhenAttribute", "Reqnroll.ThenAttribute",
        "TechTalk.SpecFlow.GivenAttribute", "TechTalk.SpecFlow.WhenAttribute", "TechTalk.SpecFlow.ThenAttribute"
    ];

    [KernelFunction("scan_assembly_for_step_definitions")]
    [Description("Scans a .NET assembly DLL for Reqnroll or SpecFlow step definitions ([Given], [When], [Then] attributes). Returns a structured list of discovered step definitions with their keywords and patterns.")]
    public string ScanAssembly(
        [Description("Absolute path to the .NET assembly DLL to scan")] string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
            return $"Error: Assembly not found at '{assemblyPath}'.";

        var steps = new List<StepDefinition>();

        try
        {
            var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
            var resolver = new PathAssemblyResolver(
                Directory.GetFiles(runtimeDir, "*.dll")
                    .Append(assemblyPath));

            using var mlc = new MetadataLoadContext(resolver);
            var assembly = mlc.LoadFromAssemblyPath(assemblyPath);

            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    foreach (var attr in method.GetCustomAttributesData())
                    {
                        var attrName = attr.AttributeType.Name;
                        var attrFullName = attr.AttributeType.FullName ?? attrName;

                        if (!StepAttributes.Any(s =>
                            attrName.Equals(s, StringComparison.OrdinalIgnoreCase) ||
                            attrFullName.Equals(s, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        var keyword = attrName.Replace("Attribute", "");
                        var pattern = attr.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? "(no pattern)";

                        steps.Add(new StepDefinition(keyword, pattern, method.Name, type.FullName ?? type.Name));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return $"Error scanning assembly: {ex.Message}";
        }

        if (steps.Count == 0)
            return "No step definitions found in the assembly.";

        var result = $"Found {steps.Count} step definition(s):\n\n";
        foreach (var step in steps)
        {
            result += $"  [{step.Keyword}(\"{step.Pattern}\")] → {step.SourceType}.{step.MethodName}()\n";
        }

        return result;
    }

    [KernelFunction("list_step_keywords")]
    [Description("Returns the Gherkin step keywords and their usage context for reference.")]
    public static string ListStepKeywords()
    {
        return """
            Gherkin Step Keywords:
              Given  — Preconditions, initial context or state
              When   — Actions performed by the user or system
              Then   — Expected outcomes or assertions
              And    — Continuation of the previous step type
              But    — Negative continuation (often after Then)

            Example:
              Given the user is logged in
              And the user has admin privileges
              When the user navigates to the settings page
              Then the user should see the admin panel
              But the user should not see the super-admin section
            """;
    }
}
