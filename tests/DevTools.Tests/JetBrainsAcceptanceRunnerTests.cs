using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DevTools.Tests;

public sealed class JetBrainsAcceptanceRunnerTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task IssueRows_ReadsInspectCodeSarifAndPreservesDiagnosticLocations()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        var report = Path.Combine(fixture.Root, "jetbrains-inspections.sarif");
        var generatedReport = Path.Combine(repositoryRoot, "tmp", "unity-asmdefs", "jetbrains-inspections.sarif");
        var usesGeneratedReport = File.Exists(generatedReport);
        if (usesGeneratedReport)
            File.Copy(generatedReport, report);
        else
            File.WriteAllText(report, SarifFixture, Encoding.UTF8);

        var result = await RunModuleAsync(
            fixture.Root,
            Path.Combine(repositoryRoot, "tools", "test-jetbrains-integration.py"),
            """
            import json, runpy, sys
            from pathlib import Path

            module = runpy.run_path(sys.argv[1])
            print(json.dumps(module["issue_rows"](Path(sys.argv[2])), sort_keys=True))
            """,
            report);

        AssertSuccess(result);
        using var document = JsonDocument.Parse(result.StandardOutput.Trim());
        var rows = document.RootElement.EnumerateArray().ToArray();
        Assert.NotEmpty(rows);

        var spire002 = rows.Where(row =>
                row.GetProperty("id").GetString()!.EndsWith("SPIRE002", StringComparison.Ordinal) &&
                NormalizePath(row.GetProperty("uri").GetString()!).EndsWith(
                    "Assets/RuntimeAuto/RuntimeAuto.cs",
                    StringComparison.Ordinal))
            .ToArray();
        var spire016 = rows.Where(row =>
                row.GetProperty("id").GetString()!.EndsWith("SPIRE016", StringComparison.Ordinal) &&
                NormalizePath(row.GetProperty("uri").GetString()!).EndsWith(
                    "Assets/RuntimeAuto/_rider.cs",
                    StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(spire002);
        Assert.Single(spire016);

        if (!usesGeneratedReport)
        {
            Assert.Equal(11, spire002[0].GetProperty("region").GetProperty("startLine").GetInt32());
            Assert.Equal(7, spire016[0].GetProperty("region").GetProperty("startLine").GetInt32());
            Assert.Equal(18, spire016[0].GetProperty("region").GetProperty("startColumn").GetInt32());
        }
    }

    [Fact]
    public async Task IssueRows_RejectsAResultWithoutAPhysicalLocation()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        var report = Path.Combine(fixture.Root, "malformed.sarif");
        File.WriteAllText(
            report,
            "{\"version\":\"2.1.0\",\"runs\":[{\"tool\":{\"driver\":{\"name\":\"InspectCode\"}},\"results\":[{\"ruleId\":\"SPIRE002\",\"message\":{\"text\":\"missing location\"},\"locations\":[]}]}]}",
            Encoding.UTF8);

        var result = await RunModuleAsync(
            fixture.Root,
            Path.Combine(repositoryRoot, "tools", "test-jetbrains-integration.py"),
            """
            import runpy, sys
            from pathlib import Path

            module = runpy.run_path(sys.argv[1])
            try:
                module["issue_rows"](Path(sys.argv[2]))
            except Exception as error:
                print("EXPECTED_ERROR:" + type(error).__name__ + ":" + str(error))
                raise SystemExit(0)
            raise SystemExit("issue_rows unexpectedly accepted a result without a physical location")
            """,
            report);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("EXPECTED_ERROR:", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueRows_RejectsMalformedSarifJson()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        var report = Path.Combine(fixture.Root, "malformed-json.sarif");
        File.WriteAllText(report, "{ this is not SARIF", Encoding.UTF8);

        var result = await RunModuleAsync(
            fixture.Root,
            Path.Combine(repositoryRoot, "tools", "test-jetbrains-integration.py"),
            """
            import runpy, sys
            from pathlib import Path

            module = runpy.run_path(sys.argv[1])
            try:
                module["issue_rows"](Path(sys.argv[2]))
            except Exception as error:
                print("EXPECTED_ERROR:" + type(error).__name__ + ":" + str(error))
                raise SystemExit(0)
            raise SystemExit("issue_rows unexpectedly accepted malformed JSON")
            """,
            report);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("EXPECTED_ERROR:", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueRows_RejectsEmptyPhysicalLocationUriLineAndWrongSchema()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        var malformedReports = new[]
        {
            (Name: "empty-physical-location", Json: """
                {
                  "version": "2.1.0",
                  "runs": [{ "results": [{ "locations": [{ "physicalLocation": {} }] }] }]
                }
                """),
            (Name: "missing-uri", Json: """
                {
                  "version": "2.1.0",
                  "runs": [{ "results": [{ "locations": [{ "physicalLocation": {
                    "artifactLocation": {}, "region": { "startLine": 1 }
                  } }] }] }]
                }
                """),
            (Name: "missing-line", Json: """
                {
                  "version": "2.1.0",
                  "runs": [{ "results": [{ "locations": [{ "physicalLocation": {
                    "artifactLocation": { "uri": "file:///workspace/Assets/RuntimeAuto/RuntimeAuto.cs" },
                    "region": {}
                  } }] }] }]
                }
                """),
            (Name: "wrong-schema", Json: """
                {
                  "version": "2.0.0",
                  "runs": [{ "results": [{ "locations": [{ "physicalLocation": {
                    "artifactLocation": { "uri": "file:///workspace/Assets/RuntimeAuto/RuntimeAuto.cs" },
                    "region": { "startLine": 1 }
                  } }] }] }]
                }
                """),
        };

        foreach (var malformed in malformedReports)
        {
            var report = Path.Combine(fixture.Root, malformed.Name + ".sarif");
            File.WriteAllText(report, malformed.Json, Encoding.UTF8);
            var result = await RunIssueRowsExpectedFailureAsync(repositoryRoot, fixture, report);

            Assert.False(result.TimedOut, malformed.Name + ": " + result.CombinedOutput);
            Assert.True(result.ExitCode == 0, malformed.Name + ": " + result.CombinedOutput);
            Assert.Contains("EXPECTED_ERROR:", result.StandardOutput, StringComparison.Ordinal);
        }
    }

    private const string SarifFixture = """
        {
          "$schema": "https://json.schemastore.org/sarif-2.1.0.json",
          "version": "2.1.0",
          "runs": [
            {
              "tool": {
                "driver": {
                  "name": "InspectCode",
                  "version": "2026.2.3.1"
                }
              },
              "results": [
                {
                  "ruleId": "SPIRE002",
                  "level": "warning",
                  "message": { "text": "The type requires initialization." },
                  "locations": [
                    {
                      "physicalLocation": {
                        "artifactLocation": { "uri": "file:///workspace/Assets/RuntimeAuto/RuntimeAuto.cs" },
                        "region": { "startLine": 11, "startColumn": 9, "endLine": 11, "endColumn": 20 }
                      }
                    }
                  ]
                },
                {
                  "ruleId": "SPIRE016",
                  "level": "warning",
                  "message": { "text": "Use SpireEnum<T>.From(value)." },
                  "locations": [
                    {
                      "physicalLocation": {
                        "artifactLocation": { "uri": "file:///workspace/Assets/RuntimeAuto/_rider.cs" },
                        "region": { "startLine": 7, "startColumn": 18, "endLine": 7, "endColumn": 42 }
                      }
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string RejectIssueRowsScript = """
        import runpy, sys
        from pathlib import Path

        module = runpy.run_path(sys.argv[1])
        try:
            module["issue_rows"](Path(sys.argv[2]))
        except Exception as error:
            print("EXPECTED_ERROR:" + type(error).__name__ + ":" + str(error))
            raise SystemExit(0)
        raise SystemExit("issue_rows unexpectedly accepted malformed SARIF")
        """;

    private static void AssertSuccess(ProcessResult result)
    {
        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 0, result.CombinedOutput);
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/');

    private static Task<ProcessResult> RunIssueRowsExpectedFailureAsync(
        string repositoryRoot,
        AcceptanceFixture fixture,
        string report)
        => RunModuleAsync(
            fixture.Root,
            Path.Combine(repositoryRoot, "tools", "test-jetbrains-integration.py"),
            RejectIssueRowsScript,
            report);

    private static async Task<ProcessResult> RunModuleAsync(
        string workingDirectory,
        string modulePath,
        string launcher,
        params string[] arguments)
    {
        var scriptPath = Path.Combine(workingDirectory, "invoke-helper.py");
        File.WriteAllText(scriptPath, launcher, Encoding.UTF8);
        var processArguments = new string[arguments.Length + 1];
        processArguments[0] = modulePath;
        Array.Copy(arguments, 0, processArguments, 1, arguments.Length);
        return await RunPythonAsync(workingDirectory, scriptPath, processArguments);
    }

    private static async Task<ProcessResult> RunPythonAsync(
        string workingDirectory,
        string scriptPath,
        params string[] arguments)
    {
        var python = Environment.GetEnvironmentVariable("PYTHON") ?? "python3";
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = python,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment =
                {
                    ["CI"] = "true",
                    ["GIT_TERMINAL_PROMPT"] = "0",
                    ["PYTHONUNBUFFERED"] = "1",
                },
            },
        };
        DevTools.ProcessCompatibility.AddArgument(process.StartInfo, scriptPath);
        foreach (var argument in arguments)
            DevTools.ProcessCompatibility.AddArgument(process.StartInfo, argument);

        Assert.True(process.Start(), $"Could not start Python process '{python}'.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        var timedOut = false;
        using (var timeout = new CancellationTokenSource(ProcessTimeout))
        {
            try
            {
                await DevTools.ProcessCompatibility.WaitForExitAsync(process, timeout.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                timedOut = true;
                if (!process.HasExited)
                    DevTools.ProcessCompatibility.KillTree(process);
                await DevTools.ProcessCompatibility.WaitForExitAsync(process);
            }
        }

        return new ProcessResult(
            process.ExitCode,
            await standardOutput,
            await standardError,
            timedOut);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "test-jetbrains-integration.py")) &&
                    File.Exists(Path.Combine(directory.FullName, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root for JetBrains acceptance tests.");
    }

    private sealed class AcceptanceFixture : IDisposable
    {
        private AcceptanceFixture(string root)
        {
            Root = root;
            Directory.CreateDirectory(root);
        }

        public string Root { get; }

        public static AcceptanceFixture Create(string repositoryRoot)
            => new(Path.Combine(
                repositoryRoot,
                "tmp",
                "devtools-jetbrains-acceptance-tests",
                Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class ProcessResult
    {
        public ProcessResult(int exitCode, string standardOutput, string standardError, bool timedOut)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
            TimedOut = timedOut;
        }

        public int ExitCode { get; }

        public string StandardOutput { get; }

        public string StandardError { get; }

        public bool TimedOut { get; }

        public string CombinedOutput => $"{StandardOutput}\n{StandardError}";
    }
}
