using System.Text.Json.Nodes;

namespace Spire.Tool;

public static class AssetPackageWorkflow
{
    private const string Marker = "Spire unitypackage export\n";

    public static async Task<string> PrepareAsync(ToolContext context, string project, string? output,
        string configuration, bool build)
    {
        project = context.RequireOutsideExamples(project);
        ValidateOutput(context, project, output);
        var version = PackageWorkflow.ReadVersion(context);
        output = ValidateOutput(context, project, output ?? context.PathOf("tmp/unity-package/Spire-" + version + ".unitypackage"))!;
        project = FileOps.Disposable(project, context.PathOf("tests/UnityPackageSample"), ".spire-unitypackage-export", Marker);
        var package = await PackageWorkflow.BuildAsync(context, Path.Combine(project, "upm"), configuration, build).ConfigureAwait(false);

        foreach (var folder in new[] { "Assets", "Packages", "ProjectSettings" })
            FileOps.ResetDirectory(Path.Combine(project, folder));
        FileOps.WriteJson(Path.Combine(project, "Packages/manifest.json"), new JsonObject
        {
            ["dependencies"] = new JsonObject
            {
                ["com.unity.modules.imgui"] = "1.0.0",
                ["com.unity.modules.uielements"] = "1.0.0",
                ["com.unity.modules.jsonserialize"] = "1.0.0",
            },
        });
        File.Copy(context.PathOf("tests/UnityTarballAcceptance/ProjectSettings/ProjectVersion.txt"),
            Path.Combine(project, "ProjectSettings/ProjectVersion.txt"), true);
        // Unity's bundled SDK must not inherit the repository's .NET 10 SDK selection.
        FileOps.WriteJson(Path.Combine(project, "global.json"), new JsonObject
        {
            ["sdk"] = new JsonObject { ["version"] = "8.0.318", ["rollForward"] = "latestPatch" },
        });
        var assets = Path.Combine(project, "Assets/Spire");
        foreach (var folder in new[] { "Runtime", "Analyzers" })
        {
            FileOps.CopyDirectory(Path.Combine(package, folder), Path.Combine(assets, folder));
            File.Copy(Path.Combine(package, folder + ".meta"), Path.Combine(assets, folder + ".meta"), true);
        }
        FileOps.CopyDirectory(Path.Combine(package, "Documentation~"), Path.Combine(assets, "Documentation"));
        foreach (var file in new[] { "LICENSE.md", "Third Party Notices.md", "CHANGELOG.md" })
            foreach (var suffix in new[] { "", ".meta" })
                File.Copy(Path.Combine(package, file + suffix), Path.Combine(assets, file + suffix), true);
        FileOps.Write(Path.Combine(assets, "README.txt"),
            "Houtamelo.Spire " + version + "\n\n" +
            "Import this .unitypackage using Assets > Import Package > Custom Package.\n" +
            "Keep every DLL and its metadata selected. The imported files live in Assets/Spire; the archive can be deleted afterward.\n" +
            "Remove an existing Spire UPM installation before importing to avoid duplicate DLLs.\n" +
            "The package contains the runtime, compiler extensions, and offline documentation. Samples are available in the UPM package.\n" +
            "See Documentation for union usage and serialization guidance. Requires Unity 2022.3.62f3 or newer.\n");
        WriteMissingMetadata(assets, assets);
        FileOps.WriteJson(Path.Combine(project, "unitypackage-inputs.json"), new JsonObject
        {
            ["output"] = output,
            ["version"] = version,
            ["assetPath"] = "Assets/Spire",
        });
        return project;
    }

    public static async Task<int> RunAsync(ToolContext context, string project, string? output, string editor,
        string configuration, bool build, bool prepareOnly, TimeSpan timeout)
    {
        if (!prepareOnly) FileOps.RequireFile(editor);
        project = await PrepareAsync(context, project, output, configuration, build).ConfigureAwait(false);
        if (prepareOnly)
        {
            Console.WriteLine("Unity asset export project: " + project);
            return 0;
        }

        output = FileOps.ReadObject(Path.Combine(project, "unitypackage-inputs.json"))["output"]!.GetValue<string>();
        // Publish only a completed export so a failed Editor run preserves the previous artifact.
        var pending = Path.Combine(project, "export.unitypackage");
        FileOps.DeleteFile(pending);
        var log = Path.Combine(project, "unitypackage-export.log");
        var result = await UnityRunner.ExportPackageAsync(context, project, editor, "Assets/Spire", pending, log, timeout).ConfigureAwait(false);
        result.EnsureSuccess("Unity asset export; log: " + log);
        FileOps.RequireFile(pending);
        if (new FileInfo(pending).Length == 0)
            throw new InvalidOperationException("Unity exported an empty asset package; log: " + log);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.Copy(pending, output, true);
        Console.WriteLine("Unity asset package: " + output);
        Console.WriteLine("Import with Assets > Import Package > Custom Package. The archive can be deleted after import.");
        return 0;
    }

    private static string? ValidateOutput(ToolContext context, string project, string? output)
    {
        if (output == null) return null;
        output = context.RequireOutsideExamples(output);
        if (!output.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output must have the .unitypackage extension.");
        var comparison = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (output.StartsWith(project + Path.DirectorySeparatorChar, comparison))
            throw new ArgumentException("The output archive must stay outside the disposable export project.");
        return output;
    }

    private static void WriteMissingMetadata(string root, string path)
    {
        var directory = Directory.Exists(path);
        if (!File.Exists(path + ".meta"))
        {
            var guid = PackageWorkflow.Guid("AssetPackage/" + FileOps.Relative(root, path));
            FileOps.Write(path + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n" +
                (directory ? "folderAsset: yes\nDefaultImporter:\n" : "TextScriptImporter:\n") +
                "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n");
        }
        if (!directory) return;
        foreach (var file in Directory.EnumerateFiles(path).Where(file => !file.EndsWith(".meta", StringComparison.Ordinal)))
            WriteMissingMetadata(root, file);
        foreach (var child in Directory.EnumerateDirectories(path))
            WriteMissingMetadata(root, child);
    }
}
