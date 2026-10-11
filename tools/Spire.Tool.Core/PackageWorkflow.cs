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
        var expected = new HashSet<string>(new[] { "package.json", "package.json.meta", "README.md", "README.md.meta", "LICENSE", "LICENSE.meta" }, StringComparer.Ordinal);
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
        File.Copy(context.PathOf("LICENSE"), Path.Combine(output, "LICENSE"), true);
        FileOps.WriteJson(Path.Combine(output, "package.json"), new JsonObject
        {
            ["name"] = "com.houtamelo.spire",
            ["version"] = versions[0],
            ["displayName"] = "Houtamelo.Spire",
            ["description"] = "Spire analyzers and discriminated unions for Unity 6000.5 with Mono and IL2CPP.",
            ["unity"] = "6000.5",
            ["dependencies"] = new JsonObject { ["com.unity.modules.imgui"] = "1.0.0", ["com.unity.modules.uielements"] = "1.0.0" },
            ["author"] = new JsonObject { ["name"] = "Houtamelo" },
            ["license"] = "MIT",
        });
        FileOps.Write(Path.Combine(output, "README.md"), "# Houtamelo.Spire for Unity\n\n" +
            "Built from the unity-6000 branch; validated on Unity 6000.5.6f1, Linux, with Mono and IL2CPP.\n" +
            "Copy this folder into Packages/com.houtamelo.spire or add it as a local package using package.json.\n" +
            "Apply System.Serializable to a mutable struct union to enable Unity serialization.\n" +
            "Serialization defaults to Additive; explicit BoxedFields, BoxedTuple, UnsafeOverlap, and Overlap are supported.\n" +
            "Variant fields must use native Unity-supported serialization types.\n" +
            "Use [UnitySerializeReference] on a variant parameter to preserve nulls, derived types, and shared references.\n" +
            "Changing a field between inline and managed-reference serialization requires handling existing data separately.\n" +
            "UnsafeOverlap requires unsafe code; Overlap cannot be generic or nested in a generic type.\n" +
            "IL2CPP serialization is tested with High managed stripping; Spire preserves Unity's required type-resolution method.\n" +
            "Other Unity versions and player platforms require separate validation.\n" +
            "Rider is the supported IDE; compiler-host DLLs are supplied by the host.\n");
        foreach (var name in new[] { "package.json", "README.md", "LICENSE" })
            FileOps.Write(Path.Combine(output, name + ".meta"), "fileFormatVersion: 2\nguid: " + Guid(name) +
                "\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n");
        return output;
    }

    internal static string PluginMetadata(string relative, bool analyzer)
    {
        var enabled = analyzer ? 0 : 1;
        return "fileFormatVersion: 2\nguid: " + Guid(relative) + "\n" + (analyzer ? "labels:\n- RoslynAnalyzer\n" : "") +
            "PluginImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  iconMap: {}\n  executionOrder: {}\n" +
            "  defineConstraints: []\n  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 0\n  validateReferences: 1\n" +
            "  platformData:\n  - first:\n      : Any\n    second:\n      enabled: " + enabled + "\n      settings: {}\n" +
            "  - first:\n      Editor: Editor\n    second:\n      enabled: " + enabled + "\n      settings:\n        DefaultValueInitialized: true\n" +
            "  userData:\n  assetBundleName:\n  assetBundleVariant:\n";
    }

    internal static string TestPluginMetadata(string guid, bool player = false) => "fileFormatVersion: 2\nguid: " + guid + "\nPluginImporter:\n" +
        "  externalObjects: {}\n  serializedVersion: 2\n  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 1\n  validateReferences: 1\n" +
        "  platformData:\n  - first:\n      '': Any\n    second:\n      enabled: " + (player ? 1 : 0) + "\n      settings: {}\n" +
        "  - first:\n      Editor: Editor\n    second:\n      enabled: 1\n      settings:\n        DefaultValueInitialized: true\n" +
        "  userData:\n  assetBundleName:\n  assetBundleVariant:\n";
}
