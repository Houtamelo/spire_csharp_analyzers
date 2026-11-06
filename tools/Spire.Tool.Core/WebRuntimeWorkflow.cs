using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Spire.Tool;

public static class WebRuntimeWorkflow
{
    public static async Task<int> RunAsync(ToolContext context, string project, string editor,
        string configuration, bool build, bool prepareOnly, TimeSpan timeout)
    {
        project = context.RequireOutsideExamples(project);
        FileOps.RequireFile(editor);
        var count = await BehavioralWorkflow.PrepareAsync(context, project, configuration, build, editor, "IL2CPP")
            .ConfigureAwait(false);
        project = FileOps.FullPath(project);
        var inputs = FileOps.ReadObject(Path.Combine(project, "behavioral-inputs.json"));
        var runtime = Path.Combine(project, "Assets/WebBehavioral");
        FileOps.ResetDirectory(runtime);
        Directory.Move(Path.Combine(project, "Assets/Tests/Existing"), Path.Combine(runtime, "Existing"));
        Directory.Move(Path.Combine(project, "Assets/Tests/Dependencies"), Path.Combine(runtime, "Dependencies"));
        File.Copy(Path.Combine(project, "Assets/Tests/IsExternalInit.cs"), Path.Combine(runtime, "IsExternalInit.cs"));
        FileOps.ResetDirectory(Path.Combine(project, "Assets/Tests"));
        var dependencies = FileOps.Strings(inputs["testDependencies"]);
        const string assembly = "Spire.UnityWebBehavioral";
        FileOps.WriteJson(Path.Combine(runtime, assembly + ".asmdef"), new JsonObject
        {
            ["name"] = assembly,
            ["references"] = new JsonArray(),
            ["includePlatforms"] = new JsonArray(),
            ["allowUnsafeCode"] = true,
            ["overrideReferences"] = true,
            ["precompiledReferences"] = FileOps.Array(dependencies.Prepend("Houtamelo.Spire.dll")),
            ["autoReferenced"] = true,
            ["noEngineReferences"] = false,
        });
        var linker = new XElement("linker", dependencies.Select(Path.GetFileNameWithoutExtension)
            .Prepend(assembly).Select(name => new XElement("assembly",
                new XAttribute("fullname", name!), new XAttribute("preserve", "all"))));
        FileOps.Write(Path.Combine(runtime, "link.xml"), linker + "\n");
        FileOps.Write(Path.Combine(runtime, "csc.rsp"), "-langversion:preview\n-nullable:enable\n");
        foreach (var fixture in new[] { "tests/UnityVariantPayloadCore", "tests/UnityWebRuntime" })
            FileOps.CopyDirectory(context.PathOf(fixture + "/Assets"), Path.Combine(project, "Assets"));
        // The production fixture has no dependency on NUnit or Unity Test Framework.
        File.Copy(context.PathOf("tests/UnityVariantPayloadCore/Packages/manifest.json"),
            Path.Combine(project, "Packages/manifest.json"), true);
        FileOps.DeleteFile(Path.Combine(project, "Packages/packages-lock.json"));
        var version = Directory.GetParent(Path.GetDirectoryName(editor)!)!.Name;
        FileOps.Write(Path.Combine(project, "ProjectSettings/ProjectVersion.txt"), "m_EditorVersion: " + version + "\n");
        FileOps.WriteJson(Path.Combine(project, "Assets/Resources/spire-web-facts.json"), new JsonObject
        {
            ["names"] = inputs["expectedFactNames"]!.DeepClone(),
        });
        inputs["backend"] = "IL2CPP";
        inputs["platform"] = "WebGL";
        inputs["testAssembly"] = assembly;
        inputs["fixtureInputs"] = new JsonArray(new[] { "tests/UnityVariantPayloadCore/Assets", "tests/UnityWebRuntime/Assets" }
            .SelectMany(folder => Directory.EnumerateFiles(context.PathOf(folder), "*", SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => (JsonNode)FileOps.Input(context.Root, path)).ToArray());
        FileOps.WriteJson(Path.Combine(project, "web-runtime-inputs.json"), inputs);
        Console.WriteLine("Web runtime project: " + project);
        Console.WriteLine("Original behavioral Facts: " + count);
        if (prepareOnly) return 0;
        var log = Path.Combine(project, "unity-web-build.log");
        FileOps.Archive(project, new[] { log, Path.Combine(project, "unity-web-build-report.json") });
        var result = await UnityRunner.MethodAsync(context, project, editor,
            "Spire.UnityWebRuntime.WebRuntimeBuild.Build", log, timeout, buildTarget: "WebGL").ConfigureAwait(false);
        if (result.ExitCode != 0)
            Console.Error.WriteLine("Unity exit code: " + result.ExitCode + "\n" + result.StandardOutput + result.StandardError);
        var reportPath = Path.Combine(project, "unity-web-build-report.json");
        FileOps.RequireFile(reportPath);
        var report = FileOps.ReadObject(reportPath);
        Console.WriteLine("Build report: " + reportPath);
        Console.WriteLine("Run in a browser: node tools/unity-web-runtime/run.mjs --project \"" + project + "\"");
        return result.ExitCode == 0 && report["status"]?.GetValue<string>() == "pass" ? 0 : 1;
    }
}
