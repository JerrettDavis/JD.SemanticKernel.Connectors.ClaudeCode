using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;

namespace CodebaseExplorer.Plugins;

/// <summary>
/// Semantic Kernel plugin for writing knowledgebase markdown documents.
/// All paths are validated to stay within the designated output directory.
/// </summary>
public sealed class KnowledgeBaseWriterPlugin(string allowedOutputDir)
{
    private readonly string _root =
        Path.GetFullPath(allowedOutputDir);

    [KernelFunction("write_knowledgebase_document")]
    [Description("Writes a markdown document to the knowledgebase output directory.")]
    public string WriteDocument(
        [Description("File name (e.g. 'README.md')")] string fileName,
        [Description("Full markdown content to write")] string? content = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "Error: the 'content' argument is required and must not be empty. " +
                   "Please provide the full markdown content to write and call this function again.";

        try
        {
            var fullPath = ResolveSafePath(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            return $"Document written: {fullPath}";
        }
        catch (Exception ex)
        {
            return $"Error writing document: {ex.Message}";
        }
    }

    [KernelFunction("append_to_document")]
    [Description("Appends content to an existing knowledgebase document.")]
    public string AppendToDocument(
        [Description("Relative path to the markdown file")] string filePath,
        [Description("Content to append")] string? content = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "Error: the 'content' argument is required and must not be empty. " +
                   "Please provide the content to append and call this function again.";

        try
        {
            var fullPath = ResolveSafePath(filePath);
            if (!File.Exists(fullPath))
                return $"Error: File not found at '{filePath}'.";

            File.AppendAllText(fullPath, "\n\n" + content);
            return $"Content appended to: {fullPath}";
        }
        catch (Exception ex)
        {
            return $"Error appending: {ex.Message}";
        }
    }

    [KernelFunction("list_knowledgebase_documents")]
    [Description("Lists all markdown documents in the knowledgebase output.")]
    public string ListDocuments()
    {
        if (!Directory.Exists(_root))
            return "Knowledgebase directory does not exist yet.";

        var files = Directory.GetFiles(
            _root, "*.md", SearchOption.AllDirectories);
        if (files.Length == 0)
            return "No knowledgebase documents found.";

        var sb = new StringBuilder();
        sb.AppendLine($"Knowledgebase documents ({files.Length}):");
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            sb.AppendLine(
                $"  {Path.GetRelativePath(_root, file)} " +
                $"({info.Length:N0} bytes)");
        }

        return sb.ToString();
    }

    [KernelFunction("get_knowledgebase_template")]
    [Description("Returns a recommended structure template for a codebase knowledgebase, listing the documents to generate.")]
    public static string GetKnowledgeBaseTemplate()
    {
        return """
            Recommended Knowledgebase Structure:

            1. README.md — High-level overview
               - Project name, purpose, and description
               - Quick start / getting started
               - Key technologies and frameworks

            2. ARCHITECTURE.md — System architecture
               - High-level architecture diagram (mermaid)
               - Component descriptions
               - Data flow and communication patterns
               - Design decisions and rationale

            3. DEPENDENCIES.md — Dependency analysis
               - External packages with versions and purposes
               - Internal project references
               - Dependency graph (mermaid)

            4. API-SURFACE.md — Public API surface
               - Public types, interfaces, and records
               - Key methods and their signatures
               - Extension points and plugin interfaces

            5. PATTERNS.md — Code patterns and conventions
               - Naming conventions
               - Architectural patterns used
               - Error handling approach
               - Testing strategy
            """;
    }

    private string ResolveSafePath(string relativePath)
    {
        var full = Path.GetFullPath(
            Path.Combine(_root, relativePath));

        return full.StartsWith(
            _root, StringComparison.OrdinalIgnoreCase)
            ? full
            : throw new UnauthorizedAccessException(
                "Path traversal detected — access denied.");
    }
}
