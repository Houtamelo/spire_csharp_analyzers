using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Spire.Tool;

public static class PackageWorkflow
{
    internal static readonly (string Name, string Folder, bool Analyzer)[] Assemblies =
    {
        ("Houtamelo.Spire", "Runtime", false), ("Houtamelo.Spire.Analyzers", "Analyzers", true),
        ("Houtamelo.Spire.PatternAnalysis", "Analyzers", true), ("Houtamelo.Spire.CodeFixes", "Analyzers", true),
    };
    internal static string Guid(string relative) => FileOps.HashText("Houtamelo.Spire.Unity/" + relative).Substring(0, 32);

    public static async Task<string> BuildAsync(ToolContext context, string output, string configuration, bool build)
    {
        var versions = new[] { "Houtamelo.Spire", "Houtamelo.Spire.Analyzers", "Houtamelo.Spire.CodeFixes" }
            .Select(name => XDocument.Load(context.PathOf("src/" + name + "/" + name + ".csproj"))
                .Root?.Element("PropertyGroup")?.Element("Version")?.Value).Distinct().ToArray();
        if (versions.Length != 1 || string.IsNullOrEmpty(versions[0]))
            throw new InvalidOperationException("The three Spire package versions must match.");
        if (build) await context.BuildAsync("src/Houtamelo.Spire/Houtamelo.Spire.csproj", configuration).ConfigureAwait(false);
        var content = CustomerContent(context, configuration);
        var textFiles = new[] { "package.json", "README.md", "LICENSE", "Third Party Notices.md", "Runtime/Houtamelo.Spire.xml", "Samples~/Showcase/README.md" };
        var expected = new HashSet<string>(content.Keys, StringComparer.Ordinal);
        expected.UnionWith(textFiles.SelectMany(path => new[] { path, path + ".meta" }));
        foreach (var item in Assemblies)
        {
            FileOps.RequireFile(context.PathOf("src/" + item.Name + "/bin/" + configuration + "/netstandard2.0/" + item.Name + ".dll"));
            var relative = item.Folder + "/" + item.Name + ".dll";
            expected.UnionWith(new[] { relative, relative + ".meta", item.Folder + ".meta" });
        }
        output = FileOps.FullPath(output);
        if (Directory.Exists(output))
        {
            var unexpected = Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
                .Select(path => FileOps.Relative(output, path)).Except(expected).ToArray();
            if (unexpected.Length != 0) throw new InvalidOperationException("Output contains unrelated files: " + string.Join(", ", unexpected));
        }
        Directory.CreateDirectory(output);
        foreach (var item in Assemblies)
        {
            var relative = item.Folder + "/" + item.Name + ".dll";
            var target = Path.Combine(output, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(context.PathOf("src/" + item.Name + "/bin/" + configuration + "/netstandard2.0/" + item.Name + ".dll"), target, true);
            FileOps.Write(target + ".meta", PluginMetadata(relative, item.Analyzer));
        }
        foreach (var folder in Assemblies.Select(item => item.Folder).Distinct())
            FileOps.Write(Path.Combine(output, folder + ".meta"), "fileFormatVersion: 2\nguid: " + Guid(folder) +
                "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n");
        foreach (var item in content)
        {
            var target = Path.Combine(output, item.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(item.Value, target, true);
        }
        FileOps.WriteJson(Path.Combine(output, "package.json"), new JsonObject
        {
            ["name"] = "com.houtamelo.spire",
            ["version"] = versions[0],
            ["displayName"] = "Houtamelo.Spire",
            ["description"] = "Spire analyzers and discriminated unions for Unity 6000.5.6f1; validated with Mono and IL2CPP on Linux x64.",
            ["unity"] = "6000.5",
            ["unityRelease"] = "6f1",
            ["dependencies"] = new JsonObject { ["com.unity.modules.imgui"] = "1.0.0", ["com.unity.modules.uielements"] = "1.0.0" },
            ["samples"] = new JsonArray
            {
                new JsonObject
                {
                    ["displayName"] = "Unity Showcase (URP)",
                    ["description"] = "Inspector, serialization, matching, and runtime examples. Requires URP 17.5.0 and Unity Test Framework 1.7.0 before import.",
                    ["path"] = "Samples~/Showcase",
                },
            },
            ["author"] = new JsonObject { ["name"] = "Houtamelo" },
            ["license"] = "MIT",
        });
        foreach (var name in textFiles)
            FileOps.Write(Path.Combine(output, name + ".meta"), "fileFormatVersion: 2\nguid: " + Guid(name) +
                "\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n");
        return output;
    }

    private static Dictionary<string, string> CustomerContent(ToolContext context, string configuration)
    {
        var content = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string relative, string source)
        {
            var path = context.PathOf(source);
            FileOps.RequireFile(path);
            content.Add(relative, path);
        }

        Add("LICENSE", "LICENSE");
        Add("README.md", "docs/unity-package/README.md");
        Add("Third Party Notices.md", "docs/unity-package/Third Party Notices.md");
        Add("Runtime/Houtamelo.Spire.xml", "src/Houtamelo.Spire/bin/" + configuration + "/netstandard2.0/Houtamelo.Spire.xml");
        var documents = context.PathOf("docs/unity-package/documentation-files.txt");
        FileOps.RequireFile(documents);
        foreach (var relative in ReadInventory(documents))
            Add("Documentation~/" + relative, relative.StartsWith("Reference/rules/", StringComparison.Ordinal)
                ? "docs/rules/" + Path.GetFileName(relative)
                : "docs/unity-package/Documentation~/" + relative);

        const string sampleRoot = "examples/UnityShowcase/Assets/";
        var inventory = context.PathOf("examples/UnityShowcase/sample-files.txt");
        FileOps.RequireFile(inventory);
        foreach (var relative in ReadInventory(inventory))
            Add("Samples~/Showcase/" + relative, sampleRoot + relative);
        Add("Samples~/Showcase/README.md", "docs/unity-package/Showcase.md");
        return content;
    }

    private static string[] ReadInventory(string path)
    {
        var files = File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        if (files.Length == 0)
            throw new InvalidDataException("The package input inventory is empty: " + path);
        foreach (var relative in files)
            if (Path.IsPathRooted(relative) || relative.Split('/').Any(part => part is ".." or ".") || relative.Contains('\\'))
                throw new InvalidDataException("Invalid package inventory path: " + relative);
        return files;
    }

    internal static string PluginMetadata(string relative, bool analyzer)
    {
        var enabled = analyzer ? 0 : 1;
        return "fileFormatVersion: 2\nguid: " + Guid(relative) + "\n" + (analyzer ? "labels:\n- RoslynAnalyzer\n" : "") +
            "PluginImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  iconMap: {}\n  executionOrder: {}\n" +
            "  defineConstraints: []\n  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 0\n  validateReferences: 1\n" +
            "  platformData:\n  - first:\n      Any:\n    second:\n      enabled: " + enabled + "\n      settings: {}\n" +
            "  - first:\n      Editor: Editor\n    second:\n      enabled: " + enabled + "\n      settings:\n        DefaultValueInitialized: true\n" +
            "  userData:\n  assetBundleName:\n  assetBundleVariant:\n";
    }

    internal static string TestPluginMetadata(string guid, bool player = false) => "fileFormatVersion: 2\nguid: " + guid + "\nPluginImporter:\n" +
        "  externalObjects: {}\n  serializedVersion: 2\n  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 1\n  validateReferences: 1\n" +
        "  platformData:\n  - first:\n      Any:\n    second:\n      enabled: " + (player ? 1 : 0) + "\n      settings: {}\n" +
        "  - first:\n      Editor: Editor\n    second:\n      enabled: 1\n      settings:\n        DefaultValueInitialized: true\n" +
        "  userData:\n  assetBundleName:\n  assetBundleVariant:\n";
}
