using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Spire.Tool;

public static class HarnessWorkflow
{
    private const string ProbeAssembly = "Spire.UnityCompilerProbeTests";
    private const string HarnessAssembly = "Spire.UnityTestHarness";
    private static readonly string[] Suites =
    {
        "Houtamelo.Spire.Analyzers.Tests", "Houtamelo.Spire.SourceGenerators.Tests", "Houtamelo.Spire.PatternAnalysis.Tests",
        "DevTools.Tests", "Spire.GlobalConfig.EndToEnd",
    };
    private static string SuiteFolder(string assembly) => assembly == "Spire.GlobalConfig.EndToEnd" ? "Houtamelo.Spire.GlobalConfig.EndToEnd" : assembly;

    public static string[] RuntimeDependencies(string output, string assemblyName)
    {
        var manifest = FileOps.ReadObject(Path.Combine(output, assemblyName + ".deps.json"));
        var names = manifest["targets"]!.AsObject().SelectMany(target => target.Value!.AsObject())
            .SelectMany(library => library.Value?["runtime"]?.AsObject() ?? new JsonObject())
            .Select(item => Path.GetFileName(item.Key)).Where(name => name.EndsWith(".dll", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).Select(name => Path.Combine(output, name)).ToArray();
        foreach (var path in names)
            if (!File.Exists(path)) throw new InvalidOperationException("Missing manifest dependency: " + path);
        return names;
    }

    public static void ImportDependency(string source, string dependencies)
    {
        var target = Path.Combine(dependencies, Path.GetFileName(source));
        if (File.Exists(target))
        {
            var sourceHash = FileOps.Hash(source);
            var targetHash = FileOps.Hash(target);
            if (sourceHash != targetHash) throw new InvalidOperationException("Conflicting test dependency: " + source + " (" + sourceHash + ") and " + target + " (" + targetHash + ")");
            return;
        }
        Directory.CreateDirectory(dependencies);
        File.Copy(source, target);
        FileOps.Write(target + ".meta", PackageWorkflow.TestPluginMetadata(FileOps.HashText("unity-compiler-tests/" + Path.GetFileName(source)).Substring(0, 32)));
    }

    public static async Task PrepareAsync(ToolContext context, string project, string editor, string configuration, bool build, bool probe = false)
    {
        project = FileOps.FullPath(project);
        var marker = Path.Combine(project, ".spire-unity-behavioral");
        if (File.Exists(marker) && File.ReadAllText(marker) == BehavioralWorkflow.Marker)
            FileOps.Archive(project, (probe ? new[] { "compiler-probe-results.xml", "unity-compiler-probe.log" } : new[]
            { "harness-results.xml", "unity-harness.log", "harness-inputs.json", "harness-discovery.json", "expected-harness-discovery.json", "behavioral-inputs.json" })
                .Select(name => Path.Combine(project, name)));
        await BehavioralWorkflow.PrepareAsync(context, project, configuration, build, editor).ConfigureAwait(false);
        if (build) await context.BuildAsync("tools/UnityCompilerTestDependencies/UnityCompilerTestDependencies.csproj", configuration).ConfigureAwait(false);
        var probeTarget = Path.Combine(project, "Assets/CompilerProbe");
        FileOps.ResetDirectory(probeTarget);
        FileOps.CopyDirectory(context.PathOf("tests/UnityBehavioral/Assets/CompilerProbe"), probeTarget);
        FileOps.Write(Path.Combine(probeTarget, "csc.rsp"), "-langversion:12\n-nullable:enable\n");
        var dependencies = Path.Combine(project, "Assets/Tests/Dependencies");
        FileOps.ResetDirectory(dependencies);
        var probeOutput = context.PathOf("tools/UnityCompilerTestDependencies/bin/" + configuration + "/netstandard2.1");
        var probeNames = new List<string>();
        foreach (var dll in RuntimeDependencies(probeOutput, "UnityCompilerTestDependencies"))
        {
            var name = Path.GetFileName(dll);
            if (new[] { "UnityCompilerTestDependencies.dll", "UnityTestDependencies.dll", "Houtamelo.Spire.dll" }.Contains(name)) continue;
            ImportDependency(EditorDependency(dll, editor), dependencies);
            probeNames.Add(name);
        }
        foreach (var required in new[] { "Microsoft.CodeAnalysis.dll", "Microsoft.CodeAnalysis.CSharp.dll", "Microsoft.CodeAnalysis.Workspaces.dll" })
            if (!probeNames.Contains(required)) throw new InvalidOperationException("Missing compiler test dependency: " + required);
        BehavioralWorkflow.AddReferences(Path.Combine(probeTarget, ProbeAssembly + ".asmdef"), probeNames);
        if (probe) return;

        if (build) await context.BuildAsync("tools/UnityHarnessDependencies/UnityHarnessDependencies.csproj", configuration, "-p:SpireUnityTestHost=true").ConfigureAwait(false);
        var bundle = context.PathOf("tools/UnityHarnessDependencies/bin/" + configuration + "/netstandard2.1");
        if (!File.Exists(Path.Combine(bundle, "UnityHarnessDependencies.dll")))
            throw new InvalidOperationException("Missing full harness dependency bundle; run without --no-build.");
        FileOps.ResetDirectory(dependencies);
        var dependencyInputs = new JsonArray();
        foreach (var dll in RuntimeDependencies(bundle, "UnityHarnessDependencies"))
        {
            var name = Path.GetFileName(dll);
            if (name == "UnityHarnessDependencies.dll" || name == "Houtamelo.Spire.dll") continue;
            var source = EditorDependency(dll, editor);
            ImportDependency(source, dependencies);
            dependencyInputs.Add(new JsonObject { ["name"] = name, ["source"] = source, ["sha256"] = FileOps.Hash(source) });
        }
        var dataRoot = Path.Combine(project, "TestData");
        FileOps.ResetDirectory(dataRoot);
        var sources = new JsonArray();
        foreach (var assembly in Suites)
        {
            var sourceRoot = context.PathOf("tests/" + SuiteFolder(assembly));
            var output = Path.Combine(sourceRoot, "bin/" + configuration + "/netstandard2.1");
            var dll = Path.Combine(output, assembly + ".dll");
            FileOps.RequireFile(dll);
            if (FileOps.Hash(dll) != FileOps.Hash(Path.Combine(dependencies, Path.GetFileName(dll))))
                throw new InvalidOperationException("Stale test assembly in dependency bundle: " + dll);
            var target = Path.Combine(dataRoot, assembly);
            Directory.CreateDirectory(target);
            foreach (var directory in Directory.EnumerateDirectories(output).Where(path => !Path.GetFileName(path).StartsWith("runtimes", StringComparison.Ordinal)))
                FileOps.CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
            var unityFixtures = Path.Combine(sourceRoot, "Unity/fixtures");
            if (Directory.Exists(unityFixtures)) FileOps.CopyDirectory(unityFixtures, Path.Combine(target, "Unity/fixtures"));
            foreach (var source in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
                if (!FileOps.Relative(sourceRoot, source).Split('/').Any(part => part == "bin" || part == "obj")) sources.Add(FileOps.Input(context.Root, source));
        }
        var packs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages/microsoft.netcore.app.ref");
        var referenceSource = Directory.Exists(packs) ? Directory.EnumerateDirectories(packs, "8*")
            .Select(path => new { Path = Path.Combine(path, "ref/net8.0"), Version = Version.TryParse(Path.GetFileName(path), out var version) ? version : new Version() })
            .Where(item => Directory.Exists(item.Path)).OrderBy(item => item.Version).LastOrDefault()?.Path : null;
        if (referenceSource == null) throw new InvalidOperationException("The .NET 8 reference pack is missing; restore the normal test suite first.");
        var references = Path.Combine(project, "ReferenceAssemblies/net8.0");
        FileOps.ResetDirectory(references);
        foreach (var path in Directory.EnumerateFiles(referenceSource, "*.dll")) File.Copy(path, Path.Combine(references, Path.GetFileName(path)), true);
        var harness = Path.Combine(project, "Assets/Harness");
        FileOps.ResetDirectory(harness);
        FileOps.CopyDirectory(context.PathOf("tests/UnityTestHarness/Assets/Tests"), harness);
        BehavioralWorkflow.AddReferences(Path.Combine(harness, HarnessAssembly + ".asmdef"), Directory.EnumerateFiles(dependencies, "*.dll").Select(Path.GetFileName)!);
        var discovery = Path.Combine(project, "expected-harness-discovery.json");
        var result = await ProcessRunner.RunAsync(context.Dotnet, new[] { "run", "--project", context.PathOf("tools/UnityHarnessManifest/UnityHarnessManifest.csproj"),
            "-c", configuration, "--", context.Root, configuration, discovery }, context.Root, TimeSpan.FromMinutes(10)).ConfigureAwait(false);
        result.EnsureSuccess("Discover original xUnit cases");
        Console.Write(result.StandardOutput);
        var baseline = FileOps.ReadObject(discovery)["cases"]!.AsArray();
        var counts = new JsonObject();
        foreach (var assembly in Suites) counts[assembly] = baseline.Count(item => item!["assembly"]!.GetValue<string>() == assembly);
        var referenceInputs = new JsonArray();
        foreach (var path in Directory.EnumerateFiles(references, "*.dll").OrderBy(path => path, StringComparer.Ordinal))
            referenceInputs.Add(new JsonObject { ["name"] = Path.GetFileName(path), ["sha256"] = FileOps.Hash(path) });
        FileOps.WriteJson(Path.Combine(project, "harness-inputs.json"), new JsonObject
        {
            ["expectedCounts"] = counts, ["inputs"] = sources, ["referenceSource"] = referenceSource,
            ["dependencies"] = dependencyInputs, ["references"] = referenceInputs,
        });
    }

    private static string EditorDependency(string dll, string editor) => Path.GetFileName(dll) == "Newtonsoft.Json.dll"
        ? Path.Combine(Path.GetDirectoryName(FileOps.FullPath(editor))!, "Data/Managed/Newtonsoft.Json.dll") : dll;

    public static async Task<int> RunAsync(ToolContext context, string project, string editor, string configuration, bool build, bool prepareOnly, bool probe, TimeSpan timeout)
    {
        FileOps.RequireFile(editor);
        project = FileOps.FullPath(project);
        await PrepareAsync(context, project, editor, configuration, build, probe).ConfigureAwait(false);
        if (prepareOnly) { Console.WriteLine("Prepared: " + project); return 0; }
        var report = Path.Combine(project, probe ? "compiler-probe-results.xml" : "harness-results.xml");
        var log = Path.Combine(project, probe ? "unity-compiler-probe.log" : "unity-harness.log");
        var environment = new Dictionary<string, string?>
        {
            ["SPIRE_UNITY_TEST_DATA_ROOT"] = Path.Combine(project, "TestData"),
            ["SPIRE_UNITY_TEST_REFERENCE_ROOT"] = Path.Combine(project, "ReferenceAssemblies/net8.0"),
            ["SPIRE_UNITY_TEST_ASSEMBLIES"] = string.Join(";", Suites), ["SPIRE_UNITY_TEST_DISCOVERY_PATH"] = Path.Combine(project, "harness-discovery.json"),
            ["SPIRE_UNITY_TEST_CONFIGURATION"] = configuration, ["SPIRE_UNITY_TEST_REPOSITORY_ROOT"] = context.Root, ["SPIRE_UNITY_EDITOR"] = FileOps.FullPath(editor),
        };
        var result = await UnityRunner.TestsAsync(context, project, editor, probe ? ProbeAssembly : HarnessAssembly + ";" + BehavioralWorkflow.Assembly,
            report, log, timeout, environment).ConfigureAwait(false);
        FileOps.RequireFile(report);
        var cases = XDocument.Load(report).Descendants("test-case").ToArray();
        UnityRunner.PrintResults(report, cases);
        var complete = cases.Length != 0;
        if (!probe)
        {
            var actual = FileOps.ReadObject(Path.Combine(project, "harness-discovery.json"))["cases"]!.AsArray();
            var expected = FileOps.ReadObject(Path.Combine(project, "expected-harness-discovery.json"))["cases"]!.AsArray();
            string[] Values(JsonArray values, string field) => values.Select(item => item![field]!.GetValue<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var identitiesMatch = Values(expected, "identity").SequenceEqual(Values(actual, "identity"));
            var executed = Values(actual, "displayName").SequenceEqual(cases.Where(element => ((string?)element.Attribute("classname") ?? "").EndsWith("XunitReflectionNUnitRunner", StringComparison.Ordinal))
                .Select(element => (string?)element.Attribute("name") ?? "").OrderBy(value => value, StringComparer.Ordinal));
            var factsMatch = FileOps.Strings(FileOps.ReadObject(Path.Combine(project, "behavioral-inputs.json"))["expectedFactNames"]).SequenceEqual(cases
                .Select(element => (string?)element.Attribute("name") ?? "").Where(name => name.StartsWith("Spire.BehavioralTests.Tests.", StringComparison.Ordinal)).OrderBy(value => value, StringComparer.Ordinal));
            var counts = FileOps.ReadObject(Path.Combine(project, "harness-inputs.json"))["expectedCounts"]!.AsObject();
            var countsMatch = counts.All(item => actual.Count(value => value!["assembly"]!.GetValue<string>() == item.Key) == item.Value!.GetValue<int>());
            complete &= identitiesMatch && executed && factsMatch && countsMatch;
            Console.WriteLine("Exact .NET/Unity case identity match: " + identitiesMatch);
            Console.WriteLine("All discovered cases executed: " + executed);
            Console.WriteLine("All original behavioral cases executed: " + factsMatch);
        }
        return result.ExitCode == 0 && complete && cases.All(element => (string?)element.Attribute("result") == "Passed") ? 0 : 1;
    }
}
