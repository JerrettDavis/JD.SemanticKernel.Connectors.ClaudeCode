using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;

namespace CodebaseExplorer.Plugins;

/// <summary>
/// Semantic Kernel plugin for filesystem exploration — enumerate files, read contents,
/// detect project types and gather structural information about a codebase.
/// All operations are restricted to the configured codebase root.
/// </summary>
public sealed class FileSystemPlugin(string codebaseRoot)
{
    private readonly string _root =
        Path.GetFullPath(codebaseRoot);
    private static readonly HashSet<string> BinaryExtensions =
    [
        ".exe", ".dll", ".pdb", ".bin", ".obj", ".o", ".so", ".dylib",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg",
        ".zip", ".tar", ".gz", ".7z", ".rar",
        ".woff", ".woff2", ".ttf", ".eot", ".otf"
    ];

    private static readonly HashSet<string> IgnoredDirs =
    [
        "node_modules", ".git", "bin", "obj", ".vs", ".idea",
        "__pycache__", ".mypy_cache", ".pytest_cache", "dist",
        "build", "target", "packages", ".nuget", "coverage"
    ];

    [KernelFunction("get_directory_tree")]
    [Description("Returns a tree view of the directory structure up to a specified depth. Ignores common build/dependency directories.")]
    public string GetDirectoryTree(
        [Description("Root directory path to scan")] string rootPath,
        [Description("Maximum depth to traverse (default 3)")] int maxDepth = 3)
    {
        if (!Directory.Exists(rootPath))
            return $"Error: Directory not found at '{rootPath}'.";

        var sb = new StringBuilder();
        sb.AppendLine(Path.GetFileName(rootPath) + "/");
        BuildTree(rootPath, sb, "", maxDepth, 0);
        return sb.ToString();
    }

    [KernelFunction("detect_project_files")]
    [Description("Scans for project/manifest files to identify the tech stack. Detects .csproj, package.json, pom.xml, Cargo.toml, go.mod, requirements.txt, etc.")]
    public string DetectProjectFiles(
        [Description("Root directory path to scan")] string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return $"Error: Directory not found at '{rootPath}'.";

        var projectPatterns = new Dictionary<string, string>
        {
            ["*.csproj"] = ".NET (C#)",
            ["*.fsproj"] = ".NET (F#)",
            ["*.vbproj"] = ".NET (VB)",
            ["*.sln"] = ".NET Solution",
            ["*.slnx"] = ".NET Solution (XML)",
            ["package.json"] = "Node.js",
            ["tsconfig.json"] = "TypeScript",
            ["pom.xml"] = "Java (Maven)",
            ["build.gradle"] = "Java (Gradle)",
            ["Cargo.toml"] = "Rust",
            ["go.mod"] = "Go",
            ["requirements.txt"] = "Python (pip)",
            ["pyproject.toml"] = "Python (modern)",
            ["setup.py"] = "Python (setuptools)",
            ["Gemfile"] = "Ruby",
            ["composer.json"] = "PHP",
            ["CMakeLists.txt"] = "C/C++ (CMake)",
            ["Makefile"] = "Make",
            ["Dockerfile"] = "Docker",
            ["docker-compose.yml"] = "Docker Compose"
        };

        var sb = new StringBuilder();
        sb.AppendLine("Detected project files:");

        foreach (var (pattern, label) in projectPatterns)
        {
            var files = Directory.GetFiles(rootPath, pattern, SearchOption.AllDirectories)
                .Where(f => !IsInIgnoredDir(f, rootPath))
                .ToArray();

            if (files.Length > 0)
            {
                sb.AppendLine($"\n  {label}:");
                foreach (var file in files)
                    sb.AppendLine($"    {Path.GetRelativePath(rootPath, file)}");
            }
        }

        return sb.ToString();
    }

    [KernelFunction("read_file")]
    [Description("Reads a source file within the codebase. Skips binary files.")]
    public string ReadFile(
        [Description("Path to the file (relative to codebase root)")] string filePath,
        [Description("Maximum lines to return (default 500)")] int maxLines = 500)
    {
        var fullPath = ResolveSafePath(filePath);
        if (fullPath is null)
            return "Error: Path outside codebase root — access denied.";

        if (!File.Exists(fullPath))
            return $"Error: File not found at '{filePath}'.";

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        if (BinaryExtensions.Contains(ext))
            return $"Binary file ({ext}): {filePath}";

        var lines = File.ReadLines(fullPath).Take(maxLines).ToList();
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
            sb.AppendLine($"{i + 1,4}: {lines[i]}");

        if (lines.Count >= maxLines)
            sb.AppendLine($"  ... (truncated at {maxLines} lines)");

        return sb.ToString();
    }

    [KernelFunction("count_lines_by_extension")]
    [Description("Counts lines of code grouped by file extension. Provides a breakdown of the codebase size.")]
    public string CountLinesByExtension(
        [Description("Root directory path to scan")] string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return $"Error: Directory not found at '{rootPath}'.";

        var counts = new Dictionary<string, (int Files, long Lines)>();

        foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            if (IsInIgnoredDir(file, rootPath)) continue;

            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || BinaryExtensions.Contains(ext)) continue;

            try
            {
                var lineCount = File.ReadLines(file).Count();
                if (counts.TryGetValue(ext, out var existing))
                    counts[ext] = (existing.Files + 1, existing.Lines + lineCount);
                else
                    counts[ext] = (1, lineCount);
            }
            catch
            {
                // Skip files we can't read
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("Lines of code by extension:");
        foreach (var (ext, (files, lines)) in counts.OrderByDescending(kv => kv.Value.Lines))
        {
            sb.AppendLine($"  {ext,-12} {files,6} files  {lines,10:N0} lines");
        }

        sb.AppendLine($"\n  Total: {counts.Values.Sum(v => v.Files)} files, {counts.Values.Sum(v => v.Lines):N0} lines");
        return sb.ToString();
    }

    [KernelFunction("search_files")]
    [Description("Searches for files matching a name pattern under the given directory.")]
    public string SearchFiles(
        [Description("Root directory path")] string rootPath,
        [Description("File name pattern (e.g., '*.cs', 'Program.*', '*.test.*')")] string pattern)
    {
        if (!Directory.Exists(rootPath))
            return $"Error: Directory not found at '{rootPath}'.";

        var files = Directory.GetFiles(rootPath, pattern, SearchOption.AllDirectories)
            .Where(f => !IsInIgnoredDir(f, rootPath))
            .Select(f => Path.GetRelativePath(rootPath, f))
            .Take(100)
            .ToList();

        if (files.Count == 0)
            return $"No files matching '{pattern}' found.";

        var sb = new StringBuilder();
        sb.AppendLine($"Files matching '{pattern}' ({files.Count}):");
        foreach (var f in files)
            sb.AppendLine($"  {f}");
        return sb.ToString();
    }

    private void BuildTree(string dir, StringBuilder sb, string indent, int maxDepth, int currentDepth)
    {
        if (currentDepth >= maxDepth) return;

        var entries = Directory.GetFileSystemEntries(dir)
            .Where(e =>
            {
                var name = Path.GetFileName(e);
                return !name.StartsWith('.') && !IgnoredDirs.Contains(name);
            })
            .OrderBy(e => !Directory.Exists(e))
            .ThenBy(Path.GetFileName)
            .ToList();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var isLast = i == entries.Count - 1;
            var name = Path.GetFileName(entry);
            var connector = isLast ? "└── " : "├── ";
            var extension = isLast ? "    " : "│   ";

            if (Directory.Exists(entry))
            {
                sb.AppendLine($"{indent}{connector}{name}/");
                BuildTree(entry, sb, indent + extension, maxDepth, currentDepth + 1);
            }
            else
            {
                sb.AppendLine($"{indent}{connector}{name}");
            }
        }
    }

    private static bool IsInIgnoredDir(string filePath, string rootPath)
    {
        var relative = Path.GetRelativePath(rootPath, filePath);
        return relative.Split(Path.DirectorySeparatorChar)
            .Any(segment => IgnoredDirs.Contains(segment));
    }

    private string? ResolveSafePath(string path)
    {
        var full = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(_root, path));

        return full.StartsWith(
            _root, StringComparison.OrdinalIgnoreCase)
            ? full
            : null;
    }
}
