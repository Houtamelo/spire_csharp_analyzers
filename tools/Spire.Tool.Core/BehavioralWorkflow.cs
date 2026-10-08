using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Spire.Tool;

public static class BehavioralWorkflow
{
    internal const string Assembly = "Spire.UnityBehavioralTests";
    internal const string Marker = "Spire runtime behavioral tests\n";

    public static async Task<int> PrepareAsync(ToolContext context, string project, string configuration, bool build, string? editor = null)
    {
        project = FileOps.Disposable(project, context.PathOf("tests/UnityBehavioral"), ".spire-unity-behavioral", Marker);
        FileOps.Archive(project, new[] { "behavioral-results.xml", "unity-behavioral.log", "behavioral-inputs.json" }.Select(name => Path.Combine(project, name)));
        foreach (var folder in new[] { "Assets", "Packages", "ProjectSettings" }) FileOps.ResetDirectory(Path.Combine(project, folder));
        FileOps.CopyDirectory(context.PathOf("tests/UnityBehavioral/Assets/Tests"), Path.Combine(project, "Assets/Tests"));
        FileOps.CopyDirectory(context.PathOf("tests/UnityBehavioral/Packages"), Path.Combine(project, "Packages"));
        FileOps.CopyDirectory(context.PathOf("tests/UnityIntegration/ProjectSettings"), Path.Combine(project, "ProjectSettings"));
        await PackageWorkflow.BuildAsync(context, Path.Combine(project, "Packages/com.houtamelo.spire"), configuration, build).ConfigureAwait(false);
        if (build) await context.BuildAsync("tools/UnityTestDependencies/UnityTestDependencies.csproj", configuration).ConfigureAwait(false);
        var dependencyOutput = context.PathOf("tools/UnityTestDependencies/bin/" + configuration + "/netstandard2.1");
        var dependencies = Path.Combine(project, "Assets/Tests/Dependencies");
        FileOps.ResetDirectory(dependencies);
        var names = new List<string>();
        var editorDependencies = new JsonObject();
        foreach (var dll in Directory.EnumerateFiles(dependencyOutput, "*.dll").OrderBy(path => path, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(dll);
            if (name == "UnityTestDependencies.dll") continue;
            if (name.Contains("CodeAnalysis") || name.Contains("Workspaces"))
                throw new InvalidOperationException("Compiler-host dependencies are not permitted: " + name);
            var source = dll;
            if (name == "Newtonsoft.Json.dll" && editor != null)
            {
                source = Path.Combine(Path.GetDirectoryName(FileOps.FullPath(editor))!, "Data/Managed/Newtonsoft.Json.dll");
                FileOps.RequireFile(source);
                editorDependencies[name] = new JsonObject { ["source"] = source, ["sha256"] = FileOps.Hash(source) };
            }
            File.Copy(source, Path.Combine(dependencies, name), true);
            FileOps.Write(Path.Combine(dependencies, name + ".meta"), PackageWorkflow.TestPluginMetadata(FileOps.HashText("unity-behavioral/" + name).Substring(0, 32)));
            names.Add(name);
        }
        foreach (var required in new[] { "xunit.assert.dll", "xunit.core.dll", "Newtonsoft.Json.dll", "System.Text.Json.dll" })
            if (!names.Contains(required)) throw new InvalidOperationException("Missing test dependency: " + required + "; run without --no-build.");
        var asmdef = Path.Combine(project, "Assets/Tests/" + Assembly + ".asmdef");
        AddReferences(asmdef, names);
        FileOps.Write(Path.Combine(Path.GetDirectoryName(asmdef)!, "csc.rsp"), "-langversion:12\n-nullable:enable\n");
        var inputs = new JsonArray();
        var expectedNames = new List<string>();
        var factCount = 0;
        foreach (var folder in new[] { "Types", "Tests" })
        {
            var target = Path.Combine(project, "Assets/Tests/Existing/" + folder);
            FileOps.ResetDirectory(target);
            foreach (var source in Directory.EnumerateFiles(context.PathOf("tests/Houtamelo.Spire.BehavioralTests/" + folder), "*.cs").OrderBy(path => path, StringComparer.Ordinal))
            {
                File.Copy(source, Path.Combine(target, Path.GetFileName(source)), true);
                if (folder == "Tests")
                {
                    var text = File.ReadAllText(source);
                    var facts = Regex.Matches(text, @"\[Fact(?:\([^\]]*\))?\]");
                    var namespaces = Regex.Matches(text, @"^namespace\s+([\w.]+)\s*;", RegexOptions.Multiline);
                    var classes = Regex.Matches(text, @"^public\s+class\s+(\w+)\b", RegexOptions.Multiline);
                    var methods = Regex.Matches(text, @"\[Fact(?:\([^\]]*\))?\]\s+public\s+(?:async\s+)?(?:void|Task|ValueTask)\s+(\w+)\s*\(");
                    if (namespaces.Count != 1 || classes.Count != 1 || methods.Count != facts.Count)
                        throw new InvalidOperationException("Unsupported behavioral source shape; cannot verify every Fact name: " + source);
                    factCount += facts.Count;
                    foreach (Match method in methods)
                        expectedNames.Add(namespaces[0].Groups[1].Value + "." + classes[0].Groups[1].Value + "." + method.Groups[1].Value);
                }
                inputs.Add(FileOps.Input(context.Root, source));
            }
        }
        if (factCount == 0) throw new InvalidOperationException("No original behavioral test assertions were found.");
        FileOps.WriteJson(Path.Combine(project, "behavioral-inputs.json"), new JsonObject
        {
            ["expectedFacts"] = factCount, ["expectedFactNames"] = FileOps.Array(expectedNames.OrderBy(name => name, StringComparer.Ordinal)),
            ["inputs"] = inputs, ["testDependencies"] = FileOps.Array(names), ["editorProvidedDependencies"] = editorDependencies,
        });
        return factCount;
    }

    internal static void AddReferences(string asmdef, IEnumerable<string> names)
    {
        var definition = FileOps.ReadObject(asmdef);
        definition["precompiledReferences"] = FileOps.Array(FileOps.Strings(definition["precompiledReferences"])
            .Concat(names).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal));
        FileOps.WriteJson(asmdef, definition);
    }

    public static async Task<int> RunAsync(ToolContext context, string project, string editor, string configuration, bool build, bool prepareOnly, TimeSpan timeout)
    {
        FileOps.RequireFile(editor);
        var count = await PrepareAsync(context, project, configuration, build, editor).ConfigureAwait(false);
        project = FileOps.FullPath(project);
        Console.WriteLine("Existing behavioral assertions: " + count);
        Console.WriteLine("Project: " + project);
        if (prepareOnly) return 0;
        var report = Path.Combine(project, "behavioral-results.xml");
        var log = Path.Combine(project, "unity-behavioral.log");
        var result = await UnityRunner.TestsAsync(context, project, editor, Assembly, report, log, timeout).ConfigureAwait(false);
        FileOps.RequireFile(report);
        var cases = XDocument.Load(report).Descendants("test-case").ToArray();
        var expected = FileOps.Strings(FileOps.ReadObject(Path.Combine(project, "behavioral-inputs.json"))["expectedFactNames"]);
        var actual = cases.Where(element => (string?)element.Attribute("name") is string name && name.StartsWith("Spire.BehavioralTests.Tests.", StringComparison.Ordinal))
            .Select(element => (string)element.Attribute("name")!).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        UnityRunner.PrintResults(report, cases);
        return result.ExitCode == 0 && expected.SequenceEqual(actual) && cases.All(element => (string?)element.Attribute("result") == "Passed") ? 0 : 1;
    }
}

internal static class UnityRunner
{
    internal static Task<ProcessResult> TestsAsync(ToolContext context, string project, string editor, string assemblies, string report, string log,
        TimeSpan timeout, IReadOnlyDictionary<string, string?>? environment = null)
        => ProcessRunner.RunAsync(FileOps.FullPath(editor), new[]
        {
            "-batchmode", "-nographics", "-projectPath", project, "-runTests", "-testPlatform", "EditMode",
            "-assemblyNames", assemblies, "-testResults", report, "-logFile", log,
        }, context.Root, timeout, environment, ownProcessGroup: true);

    internal static Task<ProcessResult> MethodAsync(ToolContext context, string project, string editor, string method, string log, TimeSpan timeout,
        bool graphics = false, IReadOnlyDictionary<string, string?>? environment = null)
        => ProcessRunner.RunAsync(FileOps.FullPath(editor), new[] { "-batchmode", "-projectPath", project, "-executeMethod", method, "-logFile", log }
            .Concat(graphics ? System.Array.Empty<string>() : new[] { "-nographics" }), context.Root, timeout, environment, ownProcessGroup: true);

    internal static void PrintResults(string report, XElement[] cases)
    {
        var root = XDocument.Load(report).Root!;
        Console.WriteLine("Unity results: " + string.Join(", ", root.Attributes().Select(attribute => attribute.Name + "=" + attribute.Value)));
        Console.WriteLine("Report: " + report);
        foreach (var element in cases.Where(element => (string?)element.Attribute("result") != "Passed").Take(20))
            Console.Error.WriteLine((string?)element.Attribute("fullname") + ": " + (element.Element("failure")?.Element("message")?.Value ?? (string?)element.Attribute("result")));
    }
}
