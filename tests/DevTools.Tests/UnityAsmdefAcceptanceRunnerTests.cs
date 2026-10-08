using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace DevTools.Tests;

public sealed class UnityAsmdefAcceptanceRunnerTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task PrepareProject_CopiesTheFixtureAndPackageProvenance()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);

        var result = await RunModuleAsync(
            fixture.Root,
            Path.Combine(repositoryRoot, "tools", "test-unity-asmdefs.py"),
            """
            import runpy, sys
            from pathlib import Path

            module = runpy.run_path(sys.argv[1])
            module["prepare_project"](Path(sys.argv[2]), sys.argv[3], False)
            print("PREPARED")
            """,
            fixture.ProjectPath,
            configuration);

        AssertSuccess(result);
        Assert.Contains("PREPARED", result.StandardOutput, StringComparison.Ordinal);
        AssertFixtureSourcesCopied(repositoryRoot, fixture.ProjectPath);
        AssertInputProvenance(repositoryRoot, fixture.ProjectPath);
        AssertPackageProvenance(repositoryRoot, fixture.ProjectPath, configuration);
    }

    [Fact]
    public async Task PrepareProject_WritesDisposableGlobalJsonPinnedToUnitySdk()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);

        var result = await RunModuleAsync(
            fixture.Root,
            Path.Combine(repositoryRoot, "tools", "test-unity-asmdefs.py"),
            """
            import runpy, sys
            from pathlib import Path

            module = runpy.run_path(sys.argv[1])
            module["prepare_project"](Path(sys.argv[2]), sys.argv[3], False)
            """,
            fixture.ProjectPath,
            configuration);

        AssertSuccess(result);
        var globalJson = Path.Combine(fixture.ProjectPath, "global.json");
        Assert.True(File.Exists(globalJson), globalJson);
        using var document = JsonDocument.Parse(File.ReadAllText(globalJson));
        var sdk = document.RootElement.GetProperty("sdk");
        Assert.Equal("8.0.318", sdk.GetProperty("version").GetString());
        Assert.Equal("latestPatch", sdk.GetProperty("rollForward").GetString());
    }

    [Fact]
    public async Task PrepareProject_RepeatedPreparationRemovesOwnedStaleInputs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        var modulePath = Path.Combine(repositoryRoot, "tools", "test-unity-asmdefs.py");
        const string prepareScript = """
            import runpy, sys
            from pathlib import Path

            module = runpy.run_path(sys.argv[1])
            module["prepare_project"](Path(sys.argv[2]), sys.argv[3], False)
            """;

        var first = await RunModuleAsync(
            fixture.Root,
            modulePath,
            prepareScript,
            fixture.ProjectPath,
            configuration);
        AssertSuccess(first);

        var staleFiles = new[]
        {
            Path.Combine(fixture.ProjectPath, "Assets", "stale-owned.cs"),
            Path.Combine(fixture.ProjectPath, "Packages", "stale-owned.txt"),
            Path.Combine(fixture.ProjectPath, "ProjectSettings", "stale-owned.asset"),
        };
        foreach (var path in staleFiles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "stale", Encoding.UTF8);
        }

        var staleState = Path.Combine(
            fixture.ProjectPath,
            "Library",
            "Spire.UnityAsmdefIntegration-state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(staleState)!);
        File.WriteAllText(staleState, "stale state", Encoding.UTF8);

        var second = await RunModuleAsync(
            fixture.Root,
            modulePath,
            prepareScript,
            fixture.ProjectPath,
            configuration);

        AssertSuccess(second);
        Assert.All(staleFiles, path => Assert.False(File.Exists(path), path));
        Assert.False(File.Exists(staleState));
        Assert.True(
            File.Exists(Path.Combine(fixture.ProjectPath, "Assets", "RuntimeAuto", "RuntimeAuto.cs")),
            "The repeated preparation removed the fixture source instead of only owned stale input.");
    }

    [Fact]
    public async Task PrepareProject_RejectsAnInvalidOwnershipMarkerWithoutTouchingTheProject()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.ProjectPath);
        var marker = Path.Combine(fixture.ProjectPath, ".spire-unity-asmdefs");
        var sentinel = Path.Combine(fixture.ProjectPath, "sentinel.txt");
        File.WriteAllText(marker, "owned by another preparation\n", Encoding.UTF8);
        File.WriteAllText(sentinel, "preserve this file", Encoding.UTF8);

        var result = await RunExpectedPrepareFailureAsync(
            repositoryRoot,
            fixture,
            fixture.ProjectPath,
            configuration);

        AssertExpectedPrepareFailure(result, "ownership marker is invalid");
        Assert.Equal("owned by another preparation\n", File.ReadAllText(marker));
        Assert.Equal("preserve this file", File.ReadAllText(sentinel));
        Assert.False(Directory.Exists(Path.Combine(fixture.ProjectPath, "Assets")));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, "asmdef-inputs.json")));
    }

    [Fact]
    public async Task PrepareProject_RejectsAnUnrelatedProjectWithoutTouchingIt()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.ProjectPath);
        var sentinel = Path.Combine(fixture.ProjectPath, "unrelated-sentinel.txt");
        File.WriteAllText(sentinel, "preserve this file", Encoding.UTF8);

        var result = await RunExpectedPrepareFailureAsync(
            repositoryRoot,
            fixture,
            fixture.ProjectPath,
            configuration);

        AssertExpectedPrepareFailure(result, "unrelated files");
        Assert.Equal("preserve this file", File.ReadAllText(sentinel));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, ".spire-unity-asmdefs")));
        Assert.False(Directory.Exists(Path.Combine(fixture.ProjectPath, "Assets")));
    }

    [Fact]
    public async Task PrepareProject_RejectsTheTrackedFixture()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        var trackedFixture = Path.Combine(repositoryRoot, "tests", "UnityAsmdefIntegration");

        var result = await RunExpectedPrepareFailureAsync(
            repositoryRoot,
            fixture,
            trackedFixture,
            configuration);

        AssertExpectedPrepareFailure(result, "fixture");
        Assert.False(File.Exists(Path.Combine(trackedFixture, ".spire-unity-asmdefs")));
    }

    [Fact]
    public async Task AnalyzerReferences_ParsesARealBeeResponseAndDropsAResponseWithoutAnalyzers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceProject = Path.Combine(repositoryRoot, "tmp", "unity-asmdefs");
        var sourceBee = Path.Combine(sourceProject, "Library", "Bee");
        Assert.True(Directory.Exists(sourceBee), sourceBee);

        var sourceResponse = Directory
            .EnumerateFiles(sourceBee, "*.rsp", SearchOption.AllDirectories)
            .Select(path => new { Path = path, Lines = File.ReadAllLines(path) })
            .FirstOrDefault(value =>
                value.Lines.Any(IsOutputArgument) &&
                value.Lines.Any(IsAnalyzerArgument));
        Assert.NotNull(sourceResponse);

        var outputArgument = sourceResponse!.Lines.First(IsOutputArgument);
        var assemblyName = Path.GetFileNameWithoutExtension(
            Unquote(outputArgument[(outputArgument.IndexOf(':') + 1)..].Trim()));
        Assert.False(string.IsNullOrWhiteSpace(assemblyName));
        var expectedAnalyzers = sourceResponse.Lines
            .Where(IsAnalyzerArgument)
            .Select(line => Unquote(line[(line.IndexOf(':') + 1)..].Trim()))
            .ToArray();
        Assert.NotEmpty(expectedAnalyzers);

        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        var copiedResponse = Path.Combine(
            fixture.ProjectPath,
            "Library",
            "Bee",
            "copied-real-response.rsp");
        Directory.CreateDirectory(Path.GetDirectoryName(copiedResponse)!);
        File.WriteAllLines(copiedResponse, sourceResponse.Lines, Encoding.UTF8);

        var modulePath = Path.Combine(repositoryRoot, "tools", "test-unity-asmdefs.py");
        var inspectScript = """
            import json, runpy, sys
            from pathlib import Path

            module = runpy.run_path(sys.argv[1])
            print(json.dumps(module["analyzer_references"](Path(sys.argv[2])), sort_keys=True))
            """;
        var parsed = await RunModuleAsync(
            fixture.Root,
            modulePath,
            inspectScript,
            fixture.ProjectPath);
        AssertSuccess(parsed);

        using var document = JsonDocument.Parse(parsed.StandardOutput.Trim());
        var root = document.RootElement;
        Assert.True(root.TryGetProperty(assemblyName, out var assembly));
        Assert.Equal(
            "Library/Bee/copied-real-response.rsp",
            NormalizePath(assembly.GetProperty("responseFile").GetString()!));
        Assert.Equal(
            expectedAnalyzers,
            assembly.GetProperty("analyzers").EnumerateArray()
                .Select(value => value.GetString()!)
                .ToArray());

        File.WriteAllLines(
            copiedResponse,
            sourceResponse.Lines.Where(line => !IsAnalyzerArgument(line)),
            Encoding.UTF8);
        var withoutAnalyzers = await RunModuleAsync(
            fixture.Root,
            modulePath,
            inspectScript,
            fixture.ProjectPath);
        AssertSuccess(withoutAnalyzers);
        using var withoutDocument = JsonDocument.Parse(withoutAnalyzers.StandardOutput.Trim());
        Assert.False(withoutDocument.RootElement.TryGetProperty(assemblyName, out _));
    }

    [Fact]
    public async Task GeneratedProjectReferences_ReadsActualGeneratedUnityProjects()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceProject = Path.Combine(repositoryRoot, "tmp", "unity-asmdefs");
        var sourceProjects = Directory
            .EnumerateFiles(sourceProject, "*.csproj", SearchOption.TopDirectoryOnly)
            .ToArray();
        Assert.NotEmpty(sourceProjects);

        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.ProjectPath);
        foreach (var source in sourceProjects)
            File.Copy(source, Path.Combine(fixture.ProjectPath, Path.GetFileName(source)));

        var expected = sourceProjects.ToDictionary(
            path => Path.GetFileNameWithoutExtension(path),
            path => XDocument.Load(path)
                .Descendants()
                .Where(node => node.Name.LocalName == "Analyzer")
                .Select(node => node.Attribute("Include")?.Value)
                .Where(value => value is not null)
                .Cast<string>()
                .ToArray(),
            StringComparer.Ordinal);
        Assert.Contains(expected.Values, values => values.Length > 0);

        var inspectScript = """
            import json, runpy, sys
            from pathlib import Path

            module = runpy.run_path(sys.argv[1])
            print(json.dumps(module["generated_project_references"](Path(sys.argv[2])), sort_keys=True))
            """;
        var result = await RunModuleAsync(
            fixture.Root,
            Path.Combine(repositoryRoot, "tools", "test-unity-asmdefs.py"),
            inspectScript,
            fixture.ProjectPath);
        AssertSuccess(result);

        using var document = JsonDocument.Parse(result.StandardOutput.Trim());
        var actual = document.RootElement;
        Assert.Equal(expected.Keys.OrderBy(value => value, StringComparer.Ordinal),
            actual.EnumerateObject().Select(property => property.Name).OrderBy(value => value, StringComparer.Ordinal));
        foreach (var pair in expected)
        {
            var project = actual.GetProperty(pair.Key);
            Assert.Equal(
                Path.Combine(fixture.ProjectPath, pair.Key + ".csproj"),
                project.GetProperty("project").GetString());
            Assert.Equal(
                pair.Value,
                project.GetProperty("analyzers").EnumerateArray()
                    .Select(value => value.GetString()!)
                    .ToArray());
        }
    }

    private static async Task<ProcessResult> RunExpectedPrepareFailureAsync(
        string repositoryRoot,
        AcceptanceFixture fixture,
        string projectPath,
        string configuration)
    {
        return await RunModuleAsync(
            fixture.Root,
            Path.Combine(repositoryRoot, "tools", "test-unity-asmdefs.py"),
            """
            import runpy, sys
            from pathlib import Path

            module = runpy.run_path(sys.argv[1])
            try:
                module["prepare_project"](Path(sys.argv[2]), sys.argv[3], False)
            except Exception as error:
                print("EXPECTED_ERROR:" + type(error).__name__ + ":" + str(error))
                raise SystemExit(0)
            raise SystemExit("prepare_project unexpectedly accepted the unsafe target")
            """,
            projectPath,
            configuration);
    }

    private static void AssertExpectedPrepareFailure(ProcessResult result, string expectedMessage)
    {
        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("EXPECTED_ERROR:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(expectedMessage, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertSuccess(ProcessResult result)
    {
        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 0, result.CombinedOutput);
    }

    private static void AssertFixtureSourcesCopied(string repositoryRoot, string projectPath)
    {
        foreach (var directoryName in new[] { "Assets", "Packages", "ProjectSettings" })
        {
            var sourceRoot = Path.Combine(repositoryRoot, "tests", "UnityAsmdefIntegration", directoryName);
            var targetRoot = Path.Combine(projectPath, directoryName);
            Assert.True(Directory.Exists(targetRoot), targetRoot);

            var expected = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                .Select(path => RelativePath(sourceRoot, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var actual = Directory.EnumerateFiles(targetRoot, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(Path.GetFileName(path), "csc.rsp", StringComparison.Ordinal))
                .Where(path => directoryName != "Packages" ||
                    !RelativePath(targetRoot, path).StartsWith("com.houtamelo.spire/", StringComparison.Ordinal))
                .Select(path => RelativePath(targetRoot, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(expected, actual);
            foreach (var relativePath in expected)
            {
                var source = Path.Combine(sourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                var target = Path.Combine(targetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(target));
            }
        }

        var ruleset = Path.Combine(projectPath, "Assets", "Default.ruleset");
        Assert.True(File.Exists(ruleset), ruleset);
        var rspDirectories = new[] { Path.Combine(projectPath, "Assets") }
            .Concat(Directory.EnumerateFiles(projectPath, "*.asmdef", SearchOption.AllDirectories)
                .Select(Path.GetDirectoryName)
                .Where(path => path is not null)
                .Cast<string>())
            .Distinct(StringComparer.Ordinal);
        foreach (var directory in rspDirectories)
        {
            var rsp = Path.Combine(directory, "csc.rsp");
            Assert.True(File.Exists(rsp), rsp);
            Assert.Contains("-langversion:9", File.ReadAllText(rsp));
            Assert.Contains("-ruleset:\"" + ruleset + "\"", File.ReadAllText(rsp));
        }
    }

    private static void AssertInputProvenance(string repositoryRoot, string projectPath)
    {
        var manifestPath = Path.Combine(projectPath, "asmdef-inputs.json");
        Assert.True(File.Exists(manifestPath), manifestPath);
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var rows = document.RootElement.GetProperty("inputs").EnumerateArray().ToArray();
        Assert.NotEmpty(rows);

        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directoryName in new[] { "Assets", "Packages", "ProjectSettings" })
        {
            var sourceRoot = Path.Combine(repositoryRoot, "tests", "UnityAsmdefIntegration", directoryName);
            foreach (var path in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
                expected.Add(RelativePath(repositoryRoot, path));
        }

        var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var relativeSource = row.GetProperty("source").GetString()!;
            Assert.True(actual.Add(relativeSource), "Duplicate provenance entry: " + relativeSource);
            var source = Path.Combine(repositoryRoot, relativeSource.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(source), source);
            var actualHash = Sha256(source);
            Assert.Equal(actualHash, row.GetProperty("sha256").GetString());
            var copied = Path.Combine(
                projectPath,
                FixtureTargetRelativePath(relativeSource).Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(copied), copied);
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(copied));
        }

        Assert.Equal(expected.OrderBy(value => value, StringComparer.Ordinal),
            actual.OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string FixtureTargetRelativePath(string relativeSource)
    {
        var parts = NormalizePath(relativeSource)
            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var index = Array.FindIndex(
            parts,
            part => string.Equals(part, "Assets", StringComparison.Ordinal) ||
                string.Equals(part, "Packages", StringComparison.Ordinal) ||
                string.Equals(part, "ProjectSettings", StringComparison.Ordinal));
        Assert.True(index >= 0, "Fixture provenance path has no project directory: " + relativeSource);
        return string.Join("/", parts.Skip(index));
    }

    private static void AssertPackageProvenance(
        string repositoryRoot,
        string projectPath,
        string configuration)
    {
        var packageRoot = Path.Combine(projectPath, "Packages", "com.houtamelo.spire");
        Assert.True(Directory.Exists(packageRoot), packageRoot);

        var packageJson = Path.Combine(packageRoot, "package.json");
        using var packageDocument = JsonDocument.Parse(File.ReadAllText(packageJson));
        var expectedVersion = XDocument.Load(
                Path.Combine(repositoryRoot, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj"))
            .Descendants("Version")
            .Select(node => node.Value)
            .Single();
        Assert.Equal("com.houtamelo.spire", packageDocument.RootElement.GetProperty("name").GetString());
        Assert.Equal(expectedVersion, packageDocument.RootElement.GetProperty("version").GetString());

        var expectedAssemblies = new[]
        {
            (Project: "Houtamelo.Spire", Folder: "Runtime", Analyzer: false),
            (Project: "Houtamelo.Spire.Analyzers", Folder: "Analyzers", Analyzer: true),
            (Project: "Houtamelo.Spire.PatternAnalysis", Folder: "Analyzers", Analyzer: true),
            (Project: "Houtamelo.Spire.CodeFixes", Folder: "Analyzers", Analyzer: true),
        };
        foreach (var assembly in expectedAssemblies)
        {
            var source = Path.Combine(
                repositoryRoot,
                "src",
                assembly.Project,
                "bin",
                configuration,
                "netstandard2.0",
                assembly.Project + ".dll");
            Assert.True(File.Exists(source), source);
            var target = Path.Combine(packageRoot, assembly.Folder, Path.GetFileName(source));
            Assert.True(File.Exists(target), target);
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(target));
            var metadata = File.ReadAllText(target + ".meta");
            if (assembly.Analyzer)
                Assert.Contains("- RoslynAnalyzer", metadata, StringComparison.Ordinal);
            else
                Assert.DoesNotContain("RoslynAnalyzer", metadata, StringComparison.Ordinal);
        }
    }

    private static bool IsOutputArgument(string line)
        => line.TrimStart().StartsWith("/out:", StringComparison.Ordinal) ||
            line.TrimStart().StartsWith("-out:", StringComparison.Ordinal);

    private static bool IsAnalyzerArgument(string line)
        => line.TrimStart().StartsWith("/analyzer:", StringComparison.Ordinal) ||
            line.TrimStart().StartsWith("-analyzer:", StringComparison.Ordinal);

    private static string Unquote(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;

    private static string NormalizePath(string path)
        => path.Replace('\\', '/');

    private static string Sha256(string path)
        => DevTools.ProcessCompatibility.ToHexString(
            DevTools.ProcessCompatibility.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string RelativePath(string root, string path)
        => NormalizePath(Path.GetRelativePath(root, path));

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

    private static string BuildConfiguration()
#if SPIRE_UNITY_TEST_HOST
        => Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_CONFIGURATION") ?? "Release";
#else
        => Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_CONFIGURATION") ??
            (AppContext.BaseDirectory.Contains(
                Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal)
                ? "Release"
                : "Debug");
#endif

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "test-unity-asmdefs.py")) &&
                    File.Exists(Path.Combine(directory.FullName, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root for Unity asmdef acceptance tests.");
    }

    private sealed class AcceptanceFixture : IDisposable
    {
        private AcceptanceFixture(string root)
        {
            Root = root;
            ProjectPath = Path.Combine(root, "project");
            Directory.CreateDirectory(root);
        }

        public string Root { get; }

        public string ProjectPath { get; }

        public static AcceptanceFixture Create(string repositoryRoot)
            => new(Path.Combine(
                repositoryRoot,
                "tmp",
                "devtools-unity-asmdef-acceptance-tests",
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
