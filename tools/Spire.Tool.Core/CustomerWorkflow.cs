using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Spire.Tool;

public static class CustomerWorkflow
{
    private const string Marker = "Spire customer package acceptance\n";
    private const string Fixture = "tests/UnityCustomerAcceptance";
    private const string MethodPrefix = "Spire.UnityCustomerAcceptance.CustomerAcceptance.";

    public static string Prepare(ToolContext context, string project, string package, string? baseline)
    {
        project = context.RequireOutsideExamples(project);
        package = ValidatePackage(context.RequireOutsideExamples(package));
        if (baseline != null)
        {
            baseline = ValidatePackage(context.RequireOutsideExamples(baseline));
            if (!Directory.Exists(Path.Combine(baseline, "Samples~/Showcase")))
                throw new DirectoryNotFoundException("Baseline package Showcase sample is missing: " + baseline);
        }
        project = FileOps.Disposable(project, context.PathOf(Fixture), ".spire-unity-customer", Marker);
        foreach (var folder in new[] { "Assets", "Packages", "ProjectSettings" })
        {
            var target = Path.Combine(project, folder);
            FileOps.ResetDirectory(target);
            FileOps.CopyDirectory(context.PathOf(folder == "ProjectSettings"
                ? "tests/UnityPackageSample/ProjectSettings"
                : Fixture + "/" + folder), target);
        }

        if (baseline != null)
        {
            var manifestPath = Path.Combine(project, "Packages/manifest.json");
            var manifest = FileOps.ReadObject(manifestPath);
            manifest["dependencies"]!["com.houtamelo.spire"] = "file:" + baseline;
            FileOps.WriteJson(manifestPath, manifest);
            FileOps.CopyDirectory(Path.Combine(baseline, "Samples~/Showcase"), Path.Combine(project, "Assets/Samples/SpireAcceptance"));
        }
        FileOps.WriteJson(Path.Combine(project, "global.json"), new JsonObject
        { ["sdk"] = new JsonObject { ["version"] = "8.0.318", ["rollForward"] = "latestPatch" } });
        FileOps.WriteJson(Path.Combine(project, "customer-inputs.json"), new JsonObject
        {
            ["package"] = package,
            ["baseline"] = baseline,
            ["candidateManifestSha256"] = FileOps.Hash(Path.Combine(package, "package.json")),
            ["baselineManifestSha256"] = baseline == null ? null : FileOps.Hash(Path.Combine(baseline, "package.json")),
        });
        return project;
    }

    public static async Task<int> RunAsync(ToolContext context, string project, string editor, string package,
        string? baseline, bool prepareOnly, TimeSpan timeout)
    {
        project = Prepare(context, project, package, baseline);
        if (prepareOnly)
        {
            Console.WriteLine("Customer acceptance project: " + project);
            return 0;
        }
        FileOps.RequireFile(editor);
        var inputs = FileOps.ReadObject(Path.Combine(project, "customer-inputs.json"));
        var candidate = inputs["package"]!.GetValue<string>();
        var prior = inputs["baseline"]?.GetValue<string>();
        var reports = new JsonArray();

        async Task Stage(string method, string source, string phase)
        {
            var report = Path.Combine(project, "customer-acceptance.json");
            FileOps.DeleteFile(report);
            var environment = new Dictionary<string, string?>
            {
                ["SPIRE_CUSTOMER_PACKAGE"] = source,
                ["SPIRE_CUSTOMER_PHASE"] = phase,
            };
            var log = Path.Combine(project, phase + "-" + method + ".log");
            var result = await UnityRunner.MethodAsync(context, project, editor, MethodPrefix + method,
                log, timeout, graphics: true, environment).ConfigureAwait(false);
            result.EnsureSuccess("Customer acceptance " + phase + "/" + method + "; log: " + log);
            FileOps.RequireFile(report);
            var data = FileOps.ReadObject(report);
            if (data["success"]?.GetValue<bool>() != true)
                throw new InvalidOperationException("Customer acceptance failed: " + data.ToJsonString());
            var saved = Path.Combine(project, phase + "-" + method + ".json");
            File.Copy(report, saved, true);
            reports.Add(JsonNode.Parse(data.ToJsonString()));
        }

        if (prior != null)
        {
            await Stage("Install", prior, "baseline").ConfigureAwait(false);
            await Stage("CaptureBaseline", prior, "baseline").ConfigureAwait(false);
        }
        await Stage("Install", candidate, "candidate").ConfigureAwait(false);
        if (prior != null)
            await Stage("VerifyUpgrade", candidate, "candidate").ConfigureAwait(false);
        else
        {
            await Stage("ImportSample", candidate, "candidate").ConfigureAwait(false);
            await Stage("VerifyImportedSample", candidate, "candidate").ConfigureAwait(false);
        }

        var testReport = Path.Combine(project, "showcase-results.xml");
        FileOps.DeleteFile(testReport);
        if (prior == null)
            await Stage("ConfigureSample", candidate, "candidate").ConfigureAwait(false);
        ProcessResult? run = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var log = Path.Combine(project, "showcase-tests-" + attempt + ".log");
            run = await UnityRunner.TestsWithGraphicsAsync(context, project, editor,
                "Houtamelo.Spire.Showcase.Tests", testReport, log, timeout).ConfigureAwait(false);
            var text = File.Exists(log) ? FileOps.ReadSharedText(log) : "";
            var startupAssertion = !File.Exists(testReport) && text.Contains("ToFileDescriptor") && text.Contains("Assertion");
            if (run.ExitCode == 0 || !startupAssertion) break;
            Console.Error.WriteLine("Unity startup descriptor assertion; retrying once. Log: " + log);
        }
        FileOps.RequireFile(testReport);
        var cases = XDocument.Load(testReport).Descendants("test-case").ToArray();
        if (run!.ExitCode != 0 || cases.Length < 15 || cases.Any(item => (string?)item.Attribute("result") != "Passed"))
            throw new InvalidOperationException("Customer showcase tests failed; report: " + testReport);
        if (prior != null)
            await Stage("VerifyUpgrade", candidate, "candidate").ConfigureAwait(false);
        FileOps.WriteJson(Path.Combine(project, "customer-results.json"), new JsonObject
        {
            ["success"] = true,
            ["mode"] = prior == null ? "clean-import" : "upgrade",
            ["showcasePassed"] = cases.Length,
            ["stages"] = reports,
        });
        Console.WriteLine("Customer acceptance passed: " + project);
        return 0;
    }

    private static string ValidatePackage(string package)
    {
        package = FileOps.FullPath(package);
        var manifest = Path.Combine(package, "package.json");
        FileOps.RequireFile(manifest);
        if (FileOps.ReadObject(manifest)["name"]?.GetValue<string>() != "com.houtamelo.spire")
            throw new InvalidOperationException("Expected a com.houtamelo.spire package: " + manifest);
        foreach (var assembly in PackageWorkflow.Assemblies)
        {
            var dll = Path.Combine(package, assembly.Folder, assembly.Name + ".dll");
            FileOps.RequireFile(dll);
            FileOps.RequireFile(dll + ".meta");
        }
        return package;
    }
}
