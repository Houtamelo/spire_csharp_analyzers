using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Spire.Tool;

public static class AsmdefWorkflow
{
    internal const string Marker = "Spire assembly definition integration\n";
    public static async Task<string> PrepareAsync(ToolContext context, string project, string configuration, bool build)
    {
        project = context.RequireOutsideExamples(project);
        var fixture = context.PathOf("tests/UnityAsmdefIntegration");
        project = FileOps.Disposable(project, fixture, ".spire-unity-asmdefs", Marker);
        FileOps.WriteJson(Path.Combine(project, "global.json"), new JsonObject
        { ["sdk"] = new JsonObject { ["version"] = "8.0.318", ["rollForward"] = "latestPatch" } });
        FileOps.DeleteFile(Path.Combine(project, "Library/Spire.UnityAsmdefIntegration-state.json"));
        FileOps.DeleteFile(Path.Combine(project, "Library/Spire.UnityAsmdefIntegration-rider-state.json"));
        FileOps.Archive(project, new[] { "asmdef-results.json", "asmdef-inputs.json", "unity-asmdefs.log",
            "jetbrains-integration-results.json", "jetbrains-inspections.sarif", "jetbrains-inspection.log", "rider-recompile-results.json", "rider-recompile.log" }
            .Select(name => Path.Combine(project, name)));
        var inputs = new JsonArray();
        foreach (var folder in new[] { "Assets", "Packages", "ProjectSettings" })
        {
            var target = Path.Combine(project, folder);
            FileOps.ResetDirectory(target);
            var source = Path.Combine(fixture, folder);
            if (!Directory.Exists(source) && folder == "ProjectSettings") source = context.PathOf("tests/UnityIntegration/ProjectSettings");
            FileOps.CopyDirectory(source, target);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
                inputs.Add(FileOps.Input(context.Root, file));
        }
        var assets = Path.Combine(project, "Assets");
        // Unity reparses response files before invoking csc and can split quoted absolute paths.
        const string ruleset = "Assets/Default.ruleset";
        foreach (var directory in Directory.EnumerateFiles(assets, "*.asmdef", SearchOption.AllDirectories)
                     .Select(path => Path.GetDirectoryName(path)!).Append(assets).Distinct(StringComparer.Ordinal))
            FileOps.Write(Path.Combine(directory, "csc.rsp"), "-langversion:9\n-ruleset:" + ruleset + "\n");
        await PackageWorkflow.BuildAsync(context, Path.Combine(project, "Packages/individual.antonio-pedro-goncalves-ferreira.spire"), configuration, build).ConfigureAwait(false);
        var matrix = context.PathOf("tests/UnityAsmdefIntegration/matrix.json");
        File.Copy(matrix, Path.Combine(project, "matrix.json"), true);
        FileOps.WriteJson(Path.Combine(project, "asmdef-inputs.json"), new JsonObject
        { ["inputs"] = inputs, ["scenarioMatrix"] = FileOps.Input(context.Root, matrix) });
        return project;
    }

    public static JsonObject AnalyzerReferences(string project)
    {
        var result = new JsonObject();
        var bee = Path.Combine(project, "Library/Bee");
        if (!Directory.Exists(bee)) return result;
        foreach (var path in Directory.EnumerateFiles(bee, "*.rsp", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(path).Select(line => line.TrimStart()).ToArray();
            var output = lines.FirstOrDefault(line => line.StartsWith("/out:", StringComparison.Ordinal) || line.StartsWith("-out:", StringComparison.Ordinal));
            if (output == null) continue;
            var assembly = Path.GetFileNameWithoutExtension(output.Substring(output.IndexOf(':') + 1).Trim().Trim('"').Replace('\\', '/'));
            var references = lines.Where(line => line.StartsWith("/analyzer:", StringComparison.Ordinal) || line.StartsWith("-analyzer:", StringComparison.Ordinal))
                .Select(line => line.Substring(line.IndexOf(':') + 1).Trim().Trim('"')).ToArray();
            if (references.Length != 0) result[assembly] = new JsonObject { ["responseFile"] = FileOps.Relative(project, path), ["analyzers"] = FileOps.Array(references) };
        }
        return result;
    }

    public static JsonObject GeneratedProjectReferences(string project)
    {
        var result = new JsonObject();
        foreach (var path in Directory.EnumerateFiles(project, "*.csproj"))
            result[Path.GetFileNameWithoutExtension(path)] = new JsonObject
            {
                ["project"] = Path.GetFullPath(path),
                ["analyzers"] = FileOps.Array(XDocument.Load(path).Descendants()
                    .Where(element => element.Name.LocalName == "Analyzer").Select(element => (string?)element.Attribute("Include") ?? "")),
            };
        return result;
    }

    public static async Task<int> RunAsync(ToolContext context, string project, string editor, string rider, string configuration, bool build, bool prepareOnly, TimeSpan timeout, string? visualStudio = null)
    {
        FileOps.RequireFile(editor);
        if (visualStudio != null) FileOps.RequireFile(visualStudio);
        project = await PrepareAsync(context, project, configuration, build).ConfigureAwait(false);
        if (visualStudio != null)
        {
            var manifestPath = Path.Combine(project, "Packages/manifest.json");
            var manifest = FileOps.ReadObject(manifestPath);
            manifest["dependencies"]!["com.unity.ide.visualstudio"] = "2.0.29";
            FileOps.WriteJson(manifestPath, manifest);
        }
        if (prepareOnly) { Console.WriteLine("Prepared: " + project); return 0; }
        var result = await UnityRunner.MethodAsync(context, project, editor, "Spire.UnityAsmdefIntegration.AsmdefIntegration.Run",
            Path.Combine(project, "unity-asmdefs.log"), timeout, environment: new Dictionary<string, string?>
            {
                ["SPIRE_RIDER_ROOT"] = FileOps.FullPath(rider),
                ["SPIRE_VISUAL_STUDIO"] = visualStudio == null ? null : FileOps.FullPath(visualStudio),
            }).ConfigureAwait(false);
        var report = Path.Combine(project, "asmdef-results.json");
        FileOps.RequireFile(report);
        var data = FileOps.ReadObject(report);
        data["compilerResponses"] = AnalyzerReferences(project);
        data["generatedProjects"] = GeneratedProjectReferences(project);
        data["projectGenerator"] = visualStudio == null ? "Rider" : "VisualStudio";
        FileOps.WriteJson(report, data);
        Console.WriteLine("Report: " + report);
        var expected = FileOps.ReadObject(context.PathOf("tests/UnityAsmdefIntegration/matrix.json"))["scenarios"]!.AsArray();
        var complete = true;
        var required = PackageWorkflow.Assemblies.Where(item => item.Analyzer).Select(item => item.Name + ".dll").ToArray();
        foreach (var scenario in expected)
        {
            var assembly = scenario!["assembly"]!.GetValue<string>();
            foreach (var key in new[] { "compilerResponses", "generatedProjects" })
            {
                var names = FileOps.Strings(data[key]?[assembly]?["analyzers"]).Select(name => Path.GetFileName(name.Replace('\\', '/'))).ToArray();
                var missing = required.Except(names).ToArray();
                if (missing.Length == 0) continue;
                Console.Error.WriteLine("Missing analyzer references in " + key + ": " + assembly + " " + string.Join(", ", missing));
                complete = false;
            }
        }
        return result.ExitCode == 0 && data["status"]?.GetValue<string>() == "pass" && complete ? 0 : 1;
    }
}
