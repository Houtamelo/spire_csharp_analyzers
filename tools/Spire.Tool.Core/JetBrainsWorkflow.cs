using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Spire.Tool;

public static class JetBrainsWorkflow
{
    private const string FixTitle = "Replace with SpireEnum<T>.From(value)";

    public static JsonArray IssueRows(string report)
    {
        var data = FileOps.ReadObject(report);
        if (data["version"]?.GetValue<string>() != "2.1.0" || data["runs"] is not JsonArray runs || runs.Count == 0)
            throw new InvalidDataException("Expected a nonempty SARIF 2.1.0 report.");
        var rows = new JsonArray();
        foreach (var run in runs)
        {
            foreach (var item in run?["results"]?.AsArray() ?? new JsonArray())
            {
                if (item?["locations"] is not JsonArray locations || locations.Count == 0)
                    throw new InvalidDataException("An inspection result has no physical location.");
                foreach (var location in locations)
                {
                    var physical = location?["physicalLocation"];
                    var uri = physical?["artifactLocation"]?["uri"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(uri) || physical?["region"]?["startLine"]?.GetValue<int>() is not > 0)
                        throw new InvalidDataException("An inspection result has no source URI or line.");
                    rows.Add(new JsonObject
                    {
                        ["id"] = item?["ruleId"]?.GetValue<string>() ?? "", ["uri"] = uri,
                        ["region"] = physical!["region"]!.DeepClone(), ["message"] = item?["message"]?.DeepClone() ?? new JsonObject(),
                    });
                }
            }
        }
        return rows;
    }

    public static JsonObject ValidateProject(string project)
    {
        project = FileOps.FullPath(project);
        var marker = Path.Combine(project, ".spire-unity-asmdefs");
        if (!File.Exists(marker) || File.ReadAllText(marker) != AsmdefWorkflow.Marker)
            throw new InvalidOperationException("Use the disposable project created by unity-asmdefs.");
        var state = FileOps.ReadObject(Path.Combine(project, "asmdef-results.json"));
        if (state["status"]?.GetValue<string>() != "pass") throw new InvalidOperationException("The Unity asmdef acceptance stage must pass first.");
        var solution = FileOps.FullPath(state["solutionPath"]!.GetValue<string>());
        if (Path.GetDirectoryName(solution) != project || !File.Exists(solution))
            throw new InvalidOperationException("The generated solution must belong to the disposable project.");
        return state;
    }

    private static async Task<JsonObject> InspectAsync(ToolContext context, string project, string cli, string rider, TimeSpan timeout)
    {
        var state = ValidateProject(project);
        var solution = state["solutionPath"]!.GetValue<string>();
        var report = Path.Combine(project, "jetbrains-inspections.sarif");
        File.Delete(report);
        var versionResult = await ProcessRunner.RunAsync(cli, new[] { "inspectcode", "--version" }, project, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        var match = Regex.Match(versionResult.StandardOutput, @"^Version: ([0-9.]+)$", RegexOptions.Multiline);
        if (versionResult.ExitCode != 0 || !match.Success) throw new InvalidOperationException("Could not determine the InspectCode version.");
        var version = match.Groups[1].Value;
        var productPath = Path.Combine(rider, "product-info.json");
        if (File.Exists(productPath))
        {
            var expected = FileOps.ReadObject(productPath)["version"]!.GetValue<string>();
            if (version != expected) throw new InvalidOperationException("InspectCode version " + version + " does not match Rider " + expected + ".");
        }
        var log = Path.Combine(project, "jetbrains-inspection.log");
        var result = await ProcessRunner.RunAsync(cli, new[] { "inspectcode", solution, "--no-build", "--no-updates", "--no-swea", "--properties=RunAnalyzers=true",
            "--output=" + report, "--caches-home=" + Path.Combine(project, "JetBrainsInspectionCache") }, project, timeout, logFile: log, ownProcessGroup: true).ConfigureAwait(false);
        if (result.ExitCode != 0 || !File.Exists(report)) throw new InvalidOperationException("InspectCode did not produce a successful report: " + log);
        var rows = IssueRows(report);
        var expectedScenarios = FileOps.ReadObject(context.PathOf("tests/UnityAsmdefIntegration/matrix.json"))["scenarios"]!.AsArray();
        foreach (var scenario in expectedScenarios)
        {
            var source = scenario!["spire002Location"]!.GetValue<string>();
            if (!rows.Any(item => item!["id"]!.GetValue<string>().EndsWith("SPIRE002", StringComparison.Ordinal)
                && item["uri"]!.GetValue<string>().Replace('\\', '/').EndsWith(source, StringComparison.Ordinal)))
                throw new InvalidOperationException("Missing JetBrains SPIRE002 diagnostic: " + source);
        }
        var cast = rows.Where(item => item!["id"]!.GetValue<string>().EndsWith("SPIRE016", StringComparison.Ordinal)
            && item["uri"]!.GetValue<string>().EndsWith("_rider.cs", StringComparison.Ordinal)).ToArray();
        if (cast.Length != 1) throw new InvalidOperationException("Expected exactly one JetBrains SPIRE016 diagnostic.");
        return new JsonObject { ["status"] = "pass", ["version"] = version, ["solution"] = solution, ["expectedSPIRE002"] = expectedScenarios.Count,
            ["SPIRE016"] = new JsonArray(cast.Select(item => item!.DeepClone()).ToArray()), ["report"] = report };
    }

    private static async Task BuildDriverAsync(ToolContext context, string rider, string work)
    {
        var source = context.PathOf("tools/JetBrainsIntegrationTestPlugin");
        var classes = Path.Combine(work, "classes");
        FileOps.ResetDirectory(classes);
        var args = Path.Combine(work, "javac.args");
        string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        FileOps.Write(args, "--release\n21\n-classpath\n" + Quote(string.Join(Path.PathSeparator.ToString(), Directory.EnumerateFiles(Path.Combine(rider, "lib"), "*.jar"))) +
            "\n-d\n" + Quote(classes) + "\n" + Quote(Path.Combine(source, "src/spire/integration/SpireIntegrationDriver.java")) + "\n");
        var result = await ProcessRunner.RunAsync(Path.Combine(rider, "jbr/bin/javac"), new[] { "@" + args }, work, TimeSpan.FromSeconds(90), ownProcessGroup: true).ConfigureAwait(false);
        result.EnsureSuccess("Compile Rider integration driver");
        var target = Path.Combine(work, "plugins/SpireIntegrationDriver/lib");
        Directory.CreateDirectory(target);
        using var archive = ZipFile.Open(Path.Combine(target, "spire-integration-driver.jar"), ZipArchiveMode.Create);
        archive.CreateEntryFromFile(Path.Combine(source, "META-INF/plugin.xml"), "META-INF/plugin.xml");
        foreach (var path in Directory.EnumerateFiles(classes, "*.class", SearchOption.AllDirectories)) archive.CreateEntryFromFile(path, FileOps.Relative(classes, path));
    }

    private static async Task<JsonObject> QuickFixAsync(ToolContext context, string project, string rider, TimeSpan timeout)
    {
        var state = ValidateProject(project);
        var source = Path.Combine(project, "Assets/RuntimeAuto/_rider.cs");
        var before = File.ReadAllText(source);
        const string token = "(ProbeKind)value";
        if (Regex.Matches(before, Regex.Escape(token)).Count != 1) throw new InvalidOperationException("The Rider fixture must contain exactly one unfixed enum cast.");
        var expected = before.Replace(token, "SpireEnum<ProbeKind>.From(value)");
        var work = Path.Combine(project, "RiderDriver");
        Directory.CreateDirectory(work);
        FileOps.Archive(work, new[] { "rider-quick-fix.json", "rider-console.log", "logs" }.Select(name => Path.Combine(work, name)));
        FileOps.Write(Path.Combine(work, "before.cs.txt"), before);
        FileOps.Write(Path.Combine(work, "expected-after.cs.txt"), expected);
        FileOps.DeleteFile(Path.Combine(work, "plugins/SpireIntegrationDriver/lib/spire-integration-driver.jar"));
        await BuildDriverAsync(context, rider, work).ConfigureAwait(false);
        var report = Path.Combine(work, "rider-quick-fix.json");
        var properties = Path.Combine(work, "idea.properties");
        FileOps.Write(properties, string.Join("\n", new[] { "idea.config.path=" + Path.Combine(work, "config"), "idea.system.path=" + Path.Combine(work, "system"),
            "idea.plugins.path=" + Path.Combine(work, "plugins"), "idea.log.path=" + Path.Combine(work, "logs"), "idea.trust.all.projects=true",
            "ide.show.tips.on.startup.default.value=false", "idea.is.integration.test=true", "ide.show.whats.new=false" }) + "\n");
        var vmOptions = Path.Combine(work, "rider-test.vmoptions");
        FileOps.Write(vmOptions, File.ReadAllText(Path.Combine(rider, "bin/rider64.vmoptions")) + "\n-Dawt.toolkit.name=XToolkit\n" +
            "-Dspire.integration.project=" + project + "\n-Dspire.integration.report=" + report + "\n");
        using var display = ProcessRunner.Start("Xvfb", new[] { "-displayfd", "1", "-screen", "0", "1440x1000x24", "-nolisten", "tcp" }, project, ownProcessGroup: true);
        var displayErrors = display.StandardError.ReadToEndAsync();
        try
        {
            var displayRead = display.StandardOutput.ReadLineAsync();
            if (await Task.WhenAny(displayRead, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false) != displayRead)
                throw new TimeoutException("Xvfb did not allocate a display.");
            var number = await displayRead.ConfigureAwait(false);
            if (!int.TryParse(number, out _)) throw new InvalidOperationException("Xvfb did not allocate a display.");
            var environment = new Dictionary<string, string?>
            {
                ["DISPLAY"] = ":" + number, ["RIDER_PROPERTIES"] = properties, ["RIDER_VM_OPTIONS"] = vmOptions,
                ["XDG_SESSION_TYPE"] = "x11", ["WAYLAND_DISPLAY"] = null,
            };
            await ProcessRunner.RunAsync(Path.Combine(rider, "bin/rider.sh"), new[] { state["solutionPath"]!.GetValue<string>() }, project, timeout,
                environment, Path.Combine(work, "rider-console.log"), true).ConfigureAwait(false);
            FileOps.RequireFile(report);
            var data = FileOps.ReadObject(report);
            if (data["status"]?.GetValue<string>() != "pass" || data["discovered"]?.GetValue<bool>() != true || data["applied"]?.GetValue<bool>() != true
                || !(data["detail"]?.GetValue<string>() ?? "").Contains(FixTitle)) throw new InvalidOperationException("Rider quick-fix failed: " + data.ToJsonString());
            var after = File.ReadAllText(source);
            FileOps.Write(Path.Combine(work, "after.cs.txt"), after);
            if (after != expected) throw new InvalidOperationException("Rider source transformation differs from the expected fixture.");
            data["sourceBeforeSha256"] = FileOps.HashText(before);
            data["sourceAfterSha256"] = FileOps.HashText(after);
            var product = FileOps.ReadObject(Path.Combine(rider, "product-info.json"));
            data["riderVersion"] = product["version"]!.GetValue<string>();
            data["riderBuild"] = product["buildNumber"]!.GetValue<string>();
            FileOps.WriteJson(report, data);
            return data;
        }
        finally
        {
            ProcessRunner.Kill(display, true);
            await DevTools.ProcessCompatibility.WaitForExitAsync(display).ConfigureAwait(false);
            FileOps.Write(Path.Combine(work, "xvfb.log"), await displayErrors.ConfigureAwait(false));
        }
    }

    private static async Task<JsonObject> RecompileAsync(ToolContext context, string project, string editor, TimeSpan timeout)
    {
        var report = Path.Combine(project, "rider-recompile-results.json");
        File.Delete(report);
        var result = await UnityRunner.MethodAsync(context, project, editor, "Spire.UnityAsmdefIntegration.AsmdefIntegration.VerifyRiderFix",
            Path.Combine(project, "rider-recompile.log"), timeout).ConfigureAwait(false);
        FileOps.RequireFile(report);
        var data = FileOps.ReadObject(report);
        if (result.ExitCode != 0 || data["status"]?.GetValue<string>() != "pass") throw new InvalidOperationException("Unity recompilation failed: " + data.ToJsonString());
        return data;
    }

    public static async Task<int> RunAsync(ToolContext context, string project, string rider, string cli, string editor, string stage, TimeSpan timeout)
    {
        project = FileOps.FullPath(project);
        ValidateProject(project);
        var results = new JsonObject();
        if (stage == "all" || stage == "inspect") results["inspection"] = await InspectAsync(context, project, cli, rider, timeout).ConfigureAwait(false);
        if (stage == "all" || stage == "quick-fix") results["quickFix"] = await QuickFixAsync(context, project, rider, timeout).ConfigureAwait(false);
        if (stage == "all" || stage == "quick-fix" || stage == "recompile") results["unityRecompile"] = await RecompileAsync(context, project, editor, timeout).ConfigureAwait(false);
        var report = Path.Combine(project, "jetbrains-integration-results.json");
        FileOps.WriteJson(report, results);
        Console.WriteLine("Report: " + report);
        foreach (var item in results) Console.WriteLine(item.Key + ": " + item.Value!["status"]);
        return 0;
    }
}
