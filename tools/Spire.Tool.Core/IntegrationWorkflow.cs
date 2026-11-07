using System.Text.Json.Nodes;

namespace Spire.Tool;

public static class IntegrationWorkflow
{
    public static async Task<int> RunAsync(ToolContext context, string project, string editor, string configuration, bool build, bool graphics, string method, TimeSpan timeout, string fixtureRelativePath = "tests/UnityIntegration")
    {
        project = context.RequireOutsideExamples(project);
        context.RequireOutsideExamples(context.PathOf(fixtureRelativePath));
        FileOps.RequireFile(editor);
        var fixture = context.PathOf(fixtureRelativePath);
        project = FileOps.Disposable(project, fixture, ".spire-unity-integration", "Spire generated-code integration fixture\n");
        foreach (var folder in new[] { "Assets", "Packages", "ProjectSettings" })
            FileOps.CopyDirectory(Path.Combine(fixture, folder), Path.Combine(project, folder));
        var sdkConfiguration = Path.Combine(fixture, "global.json");
        if (File.Exists(sdkConfiguration))
            File.Copy(sdkConfiguration, Path.Combine(project, "global.json"), true);
        await PackageWorkflow.BuildAsync(context, Path.Combine(project, "Packages/individual.antonio-pedro-goncalves-ferreira.spire"), configuration, build).ConfigureAwait(false);
        FileOps.Archive(project, Directory.EnumerateFiles(project).Where(path => Path.GetFileName(path).Contains("results") || Path.GetFileName(path).Contains("build-report") || Path.GetFileName(path).StartsWith("unity-integration-", StringComparison.Ordinal))
            .Append(Path.Combine(project, "Library/SpireUnityIntegration-stage.json")));
        ProcessResult? result = null;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var log = Path.Combine(project, "unity-integration-" + attempt + ".log");
            result = await UnityRunner.MethodAsync(context, project, editor, method, log, timeout, graphics).ConfigureAwait(false);
            var text = File.Exists(log) ? FileOps.ReadSharedText(log) : "";
            var startupAssertion = text.Contains("ToFileDescriptor") && text.Contains("Assertion") && !text.Contains("SPIRE_UNITY_INTEGRATION_BEGIN");
            if (result.ExitCode == 0 || !startupAssertion) break;
            Console.Error.WriteLine("Unity startup assertion; retrying once. Log: " + log);
        }
        Console.WriteLine("Unity exit code: " + result!.ExitCode);
        Console.WriteLine("Project and logs: " + project);
        foreach (var report in Directory.EnumerateFiles(project, "*.json").Where(path => Path.GetFileName(path).Contains("results") || Path.GetFileName(path).Contains("build-report")))
        {
            var data = FileOps.ReadObject(report);
            Console.WriteLine("Report: " + report);
            foreach (var key in new[] { "unityVersion", "hardFailureCount", "failureCount", "passed", "failed" })
                if (data[key] != null) Console.WriteLine(key + ": " + data[key]);
        }
        return result.ExitCode;
    }
}
