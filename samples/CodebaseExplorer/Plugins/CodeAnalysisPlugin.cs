using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.SemanticKernel;

namespace CodebaseExplorer.Plugins;

/// <summary>
/// Semantic Kernel plugin for static code analysis — dependency extraction,
/// entry point detection, namespace/type enumeration, and pattern recognition.
/// </summary>
public sealed class CodeAnalysisPlugin
{
    [KernelFunction("analyze_dotnet_project")]
    [Description("Analyzes a .csproj file to extract target frameworks, NuGet dependencies, and project references.")]
    public string AnalyzeDotnetProject(
        [Description("Absolute path to the .csproj file")] string csprojPath)
    {
        if (!File.Exists(csprojPath))
            return $"Error: File not found at '{csprojPath}'.";

        try
        {
            var doc = XDocument.Load(csprojPath);
            var sb = new StringBuilder();
            sb.AppendLine($"Project: {Path.GetFileNameWithoutExtension(csprojPath)}");

            // Target frameworks
            var tfm = doc.Descendants("TargetFramework").FirstOrDefault()?.Value
                   ?? doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
            sb.AppendLine($"Target Framework(s): {tfm ?? "(not specified)"}");

            // Output type
            var outputType = doc.Descendants("OutputType").FirstOrDefault()?.Value;
            if (outputType != null)
                sb.AppendLine($"Output Type: {outputType}");

            // Package references
            var packages = doc.Descendants("PackageReference").ToList();
            if (packages.Count > 0)
            {
                sb.AppendLine($"\nNuGet Dependencies ({packages.Count}):");
                foreach (var pkg in packages)
                {
                    var name = pkg.Attribute("Include")?.Value ?? "?";
                    var version = pkg.Attribute("Version")?.Value ?? "*";
                    sb.AppendLine($"  {name} v{version}");
                }
            }

            // Project references
            var projRefs = doc.Descendants("ProjectReference").ToList();
            if (projRefs.Count > 0)
            {
                sb.AppendLine($"\nProject References ({projRefs.Count}):");
                foreach (var pr in projRefs)
                    sb.AppendLine($"  {pr.Attribute("Include")?.Value}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error analyzing project: {ex.Message}";
        }
    }

    [KernelFunction("analyze_package_json")]
    [Description("Analyzes a package.json to extract name, dependencies, dev dependencies, and scripts.")]
    public string AnalyzePackageJson(
        [Description("Absolute path to the package.json file")] string packageJsonPath)
    {
        if (!File.Exists(packageJsonPath))
            return $"Error: File not found at '{packageJsonPath}'.";

        try
        {
            var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(packageJsonPath));
            var root = json.RootElement;
            var sb = new StringBuilder();

            var name = root.TryGetProperty("name", out var n) ? n.GetString() : "(unnamed)";
            sb.AppendLine($"Package: {name}");

            if (root.TryGetProperty("version", out var v))
                sb.AppendLine($"Version: {v.GetString()}");

            if (root.TryGetProperty("description", out var d))
                sb.AppendLine($"Description: {d.GetString()}");

            void ListDeps(string section)
            {
                if (!root.TryGetProperty(section, out var deps)) return;
                var items = deps.EnumerateObject().ToList();
                if (items.Count == 0) return;
                sb.AppendLine($"\n{section} ({items.Count}):");
                foreach (var dep in items)
                    sb.AppendLine($"  {dep.Name}: {dep.Value.GetString()}");
            }

            ListDeps("dependencies");
            ListDeps("devDependencies");

            if (root.TryGetProperty("scripts", out var scripts))
            {
                sb.AppendLine("\nScripts:");
                foreach (var script in scripts.EnumerateObject())
                    sb.AppendLine($"  {script.Name}: {script.Value.GetString()}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error analyzing package.json: {ex.Message}";
        }
    }

    [KernelFunction("find_entry_points")]
    [Description("Searches for common entry points in a codebase — Main methods, Startup classes, Program.cs files, index files, etc.")]
    public string FindEntryPoints(
        [Description("Root directory path to scan")] string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return $"Error: Directory not found at '{rootPath}'.";

        var entryPointPatterns = new[]
        {
            ("Program.cs", "C# entry point"),
            ("Startup.cs", "ASP.NET Startup"),
            ("index.ts", "TypeScript entry"),
            ("index.js", "JavaScript entry"),
            ("main.ts", "TypeScript main"),
            ("main.js", "JavaScript main"),
            ("main.py", "Python main"),
            ("app.py", "Python app"),
            ("main.go", "Go main"),
            ("Main.java", "Java main"),
            ("main.rs", "Rust main")
        };

        var sb = new StringBuilder();
        sb.AppendLine("Entry points found:");

        foreach (var (pattern, label) in entryPointPatterns)
        {
            var files = Directory.GetFiles(rootPath, pattern, SearchOption.AllDirectories)
                .Where(f => !IsInIgnoredDir(f, rootPath))
                .ToList();

            foreach (var file in files)
                sb.AppendLine($"  [{label}] {Path.GetRelativePath(rootPath, file)}");
        }

        return sb.ToString();
    }

    [KernelFunction("extract_namespaces_and_types")]
    [Description("Scans C# source files to extract namespaces, classes, interfaces, and records using regex-based parsing.")]
    public string ExtractNamespacesAndTypes(
        [Description("Root directory to scan for .cs files")] string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return $"Error: Directory not found at '{rootPath}'.";

        var nsRegex = new Regex(@"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline);
        var typeRegex = new Regex(
            @"^\s*(?:public|internal|private|protected)?\s*(?:sealed|abstract|static|partial)?\s*(?:sealed|abstract|static|partial)?\s*(class|interface|record|struct|enum)\s+(\w+)",
            RegexOptions.Multiline);

        var namespaces = new Dictionary<string, List<string>>();

        foreach (var file in Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories))
        {
            if (IsInIgnoredDir(file, rootPath)) continue;

            try
            {
                var content = File.ReadAllText(file);
                var ns = nsRegex.Match(content);
                var nsName = ns.Success ? ns.Groups[1].Value : "(global)";

                foreach (Match match in typeRegex.Matches(content))
                {
                    var kind = match.Groups[1].Value;
                    var name = match.Groups[2].Value;

                    if (!namespaces.TryGetValue(nsName, out var types))
                    {
                        types = [];
                        namespaces[nsName] = types;
                    }
                    types.Add($"{kind} {name}");
                }
            }
            catch
            {
                // Skip unreadable files
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("Namespaces and Types:");
        foreach (var (ns, types) in namespaces.OrderBy(kv => kv.Key))
        {
            sb.AppendLine($"\n  {ns}:");
            foreach (var type in types.Distinct().OrderBy(t => t))
                sb.AppendLine($"    {type}");
        }

        return sb.ToString();
    }

    [KernelFunction("find_pattern_in_files")]
    [Description("Searches for a regex pattern across source files. Returns matching lines with file paths and line numbers.")]
    public string FindPatternInFiles(
        [Description("Root directory to search")] string rootPath,
        [Description("The regex pattern to search for")] string pattern,
        [Description("File extension filter (e.g., '.cs', '.ts'). Leave empty for all text files.")] string? extensionFilter = null)
    {
        if (!Directory.Exists(rootPath))
            return $"Error: Directory not found at '{rootPath}'.";

        Regex regex;
        try { regex = new Regex(pattern, RegexOptions.Multiline); }
        catch (Exception ex) { return $"Invalid regex: {ex.Message}"; }

        var searchPattern = string.IsNullOrEmpty(extensionFilter) ? "*" : $"*{extensionFilter}";
        var sb = new StringBuilder();
        var matchCount = 0;
        const int maxMatches = 50;

        foreach (var file in Directory.EnumerateFiles(rootPath, searchPattern, SearchOption.AllDirectories))
        {
            if (IsInIgnoredDir(file, rootPath)) continue;
            if (matchCount >= maxMatches) break;

            try
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length && matchCount < maxMatches; i++)
                {
                    if (regex.IsMatch(lines[i]))
                    {
                        sb.AppendLine($"  {Path.GetRelativePath(rootPath, file)}:{i + 1}: {lines[i].Trim()}");
                        matchCount++;
                    }
                }
            }
            catch { /* skip */ }
        }

        if (matchCount == 0)
            return $"No matches found for pattern '{pattern}'.";

        var result = $"Found {matchCount} match(es):\n{sb}";
        if (matchCount >= maxMatches)
            result += $"\n  (limited to first {maxMatches} matches)";

        return result;
    }

    private static bool IsInIgnoredDir(string filePath, string rootPath)
    {
        var ignoredDirs = new HashSet<string>
        {
            "node_modules", ".git", "bin", "obj", ".vs", ".idea",
            "__pycache__", "dist", "build", "target", "packages"
        };
        var relative = Path.GetRelativePath(rootPath, filePath);
        return relative.Split(Path.DirectorySeparatorChar)
            .Any(segment => ignoredDirs.Contains(segment));
    }
}
