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

public sealed class UnityHarnessRunnerTests
#if !SPIRE_UNITY_TEST_HOST
    : IClassFixture<UnityHarnessRunnerTests.HarnessBuildFixture>
#endif
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(3);
    private const string BehavioralOwnershipMarker = "Spire runtime behavioral tests\n";

    [Fact]
    public async Task PrepareHarness_NoBuild_CopiesPortableSuitesAndIndependentManifest()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = HarnessFixture.Create(repositoryRoot);
        var editor = fixture.CreateIsolatedEditor();

        var result = await RunHarnessAsync(repositoryRoot, configuration, fixture.ProjectPath, editor);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        AssertPreparedHarness(fixture.ProjectPath);
    }

    [Fact]
    public async Task PrepareHarness_RepeatedPreparationRemovesOwnedStaleFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = HarnessFixture.Create(repositoryRoot);
        var editor = fixture.CreateIsolatedEditor();

        var first = await RunHarnessAsync(repositoryRoot, configuration, fixture.ProjectPath, editor);
        Assert.True(first.ExitCode == 0, first.CombinedOutput);

        var staleFiles = new[]
        {
            Path.Combine(fixture.ProjectPath, "Assets", "Tests", "stale-owned.cs"),
            Path.Combine(fixture.ProjectPath, "Assets", "Tests", "stale-owned.cs.meta"),
            Path.Combine(fixture.ProjectPath, "ReferenceAssemblies", "net8.0", "stale.dll"),
            Path.Combine(fixture.ProjectPath, "ReferenceAssemblies", "net8.0", "stale.dll.meta"),
            Path.Combine(fixture.ProjectPath, "Assets", "Harness", "stale-owned.cs"),
            Path.Combine(fixture.ProjectPath, "TestData", "DevTools.Tests", "stale-input.txt"),
            Path.Combine(fixture.ProjectPath, "Assets", "rogue-owned.asset"),
            Path.Combine(fixture.ProjectPath, "Packages", "rogue-owned.package"),
            Path.Combine(fixture.ProjectPath, "ProjectSettings", "rogue-owned.asset"),
        };
        foreach (var path in staleFiles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "stale");
        }

        var second = await RunHarnessAsync(repositoryRoot, configuration, fixture.ProjectPath, editor);

        Assert.False(second.TimedOut, second.CombinedOutput);
        Assert.True(second.ExitCode == 0, second.CombinedOutput);
        foreach (var path in staleFiles)
            Assert.False(File.Exists(path), $"Owned stale file survived preparation: {path}");
        AssertPreparedHarness(fixture.ProjectPath);
    }

    [Fact]
    public async Task PrepareHarness_RejectsInvalidOwnershipMarkerWithoutTouchingTheProject()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = HarnessFixture.Create(repositoryRoot);
        var editor = fixture.CreateIsolatedEditor();
        Directory.CreateDirectory(fixture.ProjectPath);
        var marker = Path.Combine(fixture.ProjectPath, ".spire-unity-behavioral");
        var sentinel = Path.Combine(fixture.ProjectPath, "sentinel.txt");
        File.WriteAllText(marker, "owned by another preparation\n");
        File.WriteAllText(sentinel, "preserve me");

        var result = await RunHarnessAsync(repositoryRoot, configuration, fixture.ProjectPath, editor);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ownership marker is invalid", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("owned by another preparation\n", File.ReadAllText(marker));
        Assert.Equal("preserve me", File.ReadAllText(sentinel));
        Assert.False(Directory.Exists(Path.Combine(fixture.ProjectPath, "Assets")));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, "harness-inputs.json")));
    }

    [Fact]
    public async Task ImportDependency_RejectsDifferentRealBytesForTheSameDestinationName()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = HarnessFixture.Create(repositoryRoot);
        var pair = FindRealCollisionPair(repositoryRoot, configuration);
        var sourceA = Path.Combine(fixture.Root, "source-a", "SharedDependency.dll");
        var sourceB = Path.Combine(fixture.Root, "source-b", "SharedDependency.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceA)!);
        Directory.CreateDirectory(Path.GetDirectoryName(sourceB)!);
        File.Copy(pair.First.Path, sourceA);
        File.Copy(pair.Second.Path, sourceB);
        var dependencies = Path.Combine(fixture.Root, "dependencies");
        Directory.CreateDirectory(dependencies);
        var script = Path.Combine(fixture.Root, "check-conflict.py");
        File.WriteAllText(
            script,
            "import runpy, sys\n"
            + "from pathlib import Path\n"
            + "module = runpy.run_path(sys.argv[1])\n"
            + "source_a, source_b, dependencies = map(Path, sys.argv[2:5])\n"
            + "module['import_dependency'](source_a, dependencies)\n"
            + "module['import_dependency'](source_a, dependencies)\n"
            + "try:\n"
            + "    module['import_dependency'](source_b, dependencies)\n"
            + "except ValueError as error:\n"
            + "    print('EXPECTED_CONFLICT:' + str(error))\n"
            + "else:\n"
            + "    raise SystemExit('A different dependency was accepted for an occupied name.')\n",
            Encoding.UTF8);

        var result = await RunPythonAsync(
            repositoryRoot,
            script,
            Path.Combine(repositoryRoot, "tools", "test-unity-harness.py"),
            sourceA,
            sourceB,
            dependencies);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("EXPECTED_CONFLICT:", result.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains(pair.First.Hash, result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(pair.Second.Hash, result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(File.ReadAllBytes(sourceA), File.ReadAllBytes(Path.Combine(dependencies, "SharedDependency.dll")));
        Assert.True(File.Exists(Path.Combine(dependencies, "SharedDependency.dll.meta")));
    }

    [Fact]
    public async Task RuntimeDependencies_FiltersOrphanOutputDllsUsingTheRealDepsManifest()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = HarnessFixture.Create(repositoryRoot);
        var sourceOutput = FindDevToolsOutput(repositoryRoot, configuration);
        CopyFiles(sourceOutput, fixture.Root);
        File.Copy(
            Path.Combine(sourceOutput, "DevTools.Tests.dll"),
            Path.Combine(fixture.Root, "orphan.dll"));
        var script = Path.Combine(fixture.Root, "runtime-dependencies.py");
        File.WriteAllText(
            script,
            "import json, runpy, sys\n"
            + "from pathlib import Path\n"
            + "module = runpy.run_path(sys.argv[1])\n"
            + "values = module['runtime_dependencies'](Path(sys.argv[2]), sys.argv[3])\n"
            + "print(json.dumps(sorted(Path(value).name for value in values)))\n",
            Encoding.UTF8);

        var result = await RunPythonAsync(
            repositoryRoot,
            script,
            Path.Combine(repositoryRoot, "tools", "test-unity-harness.py"),
            fixture.Root,
            "DevTools.Tests");

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        using var names = JsonDocument.Parse(result.StandardOutput.Trim());
        var actual = names.RootElement.EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Contains("DevTools.Tests.dll", actual);
        Assert.DoesNotContain("orphan.dll", actual);
        Assert.Equal(actual.Length, actual.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task RuntimeDependencies_RejectsAReferencedDllMissingFromTheRealOutput()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = HarnessFixture.Create(repositoryRoot);
        var sourceOutput = FindDevToolsOutput(repositoryRoot, configuration);
        CopyFiles(sourceOutput, fixture.Root);
        var missing = Path.Combine(fixture.Root, "DevTools.Tests.dll");
        File.Delete(missing);
        var script = Path.Combine(fixture.Root, "missing-runtime-dependency.py");
        File.WriteAllText(
            script,
            "import runpy, sys\n"
            + "from pathlib import Path\n"
            + "module = runpy.run_path(sys.argv[1])\n"
            + "try:\n"
            + "    module['runtime_dependencies'](Path(sys.argv[2]), sys.argv[3])\n"
            + "except (FileNotFoundError, ValueError) as error:\n"
            + "    print('EXPECTED_MISSING:' + str(error))\n"
            + "else:\n"
            + "    raise SystemExit('A missing runtime asset was accepted.')\n",
            Encoding.UTF8);

        var result = await RunPythonAsync(
            repositoryRoot,
            script,
            Path.Combine(repositoryRoot, "tools", "test-unity-harness.py"),
            fixture.Root,
            "DevTools.Tests");

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("EXPECTED_MISSING:", result.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("DevTools.Tests.dll", result.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunUnity_TimeoutKillsTheRealChildProcessBeforeRethrowing()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = HarnessFixture.Create(repositoryRoot);
        var childScript = Path.Combine(fixture.Root, "run-child.py");
        var pidFile = Path.Combine(fixture.Root, "child.pid");
        File.WriteAllText(
            childScript,
            "from pathlib import Path\n"
            + "import subprocess, sys\n"
            + "child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'])\n"
            + "Path(sys.argv[1]).write_text(str(child.pid))\n"
            + "child.wait()\n",
            Encoding.UTF8);
        var script = Path.Combine(fixture.Root, "run-unity-timeout.py");
        File.WriteAllText(
            script,
            "import os, runpy, subprocess, sys, time\n"
            + "from pathlib import Path\n"
            + "module = runpy.run_path(sys.argv[1])\n"
            + "try:\n"
            + "    module['run_unity']([sys.executable, sys.argv[2], sys.argv[3]], 0.2, dict(os.environ))\n"
            + "except subprocess.TimeoutExpired:\n"
            + "    pid = int(Path(sys.argv[3]).read_text())\n"
            + "    deadline = time.time() + 3\n"
            + "    while time.time() < deadline:\n"
            + "        try:\n"
            + "            os.kill(pid, 0)\n"
            + "        except ProcessLookupError:\n"
            + "            print('EXPECTED_TIMEOUT:CHILD_CLEAN')\n"
            + "            break\n"
            + "        time.sleep(0.05)\n"
            + "    else:\n"
            + "        raise SystemExit('Timed out child survived process-group cleanup.')\n"
            + "else:\n"
            + "    raise SystemExit('run_unity returned instead of raising TimeoutExpired.')\n",
            Encoding.UTF8);

        var result = await RunPythonAsync(
            repositoryRoot,
            script,
            Path.Combine(repositoryRoot, "tools", "test-unity-harness.py"),
            childScript,
            pidFile);

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("EXPECTED_TIMEOUT:CHILD_CLEAN", result.CombinedOutput, StringComparison.Ordinal);
    }

    private static void AssertPreparedHarness(string projectPath)
    {
        Assert.Equal(
            BehavioralOwnershipMarker,
            File.ReadAllText(Path.Combine(projectPath, ".spire-unity-behavioral")));

        using var inputs = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(projectPath, "harness-inputs.json")));
        var expectedCounts = inputs.RootElement.GetProperty("expectedCounts")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetInt32(), StringComparer.Ordinal);
        Assert.NotEmpty(expectedCounts);
        Assert.All(expectedCounts.Values, count => Assert.True(count > 0));

        var inputSources = inputs.RootElement.GetProperty("inputs")
            .EnumerateArray()
            .Select(value => value.GetProperty("source").GetString()!)
            .ToArray();
        Assert.NotEmpty(inputSources);
        Assert.Equal(inputSources.Length, inputSources.Distinct(StringComparer.Ordinal).Count());

        var manifestPath = Path.Combine(projectPath, "expected-harness-discovery.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var cases = manifest.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(expectedCounts.Values.Sum(), cases.Length);
        Assert.Equal(
            cases.Length,
            cases.Select(value => value.GetProperty("identity").GetString()!).Distinct(StringComparer.Ordinal).Count());
        var actualCounts = cases
            .GroupBy(value => value.GetProperty("assembly").GetString()!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Assert.Equal(expectedCounts, actualCounts);

        foreach (var assembly in expectedCounts.Keys)
        {
            var testAssembly = Path.Combine(
                projectPath,
                "Assets",
                "Tests",
                "Dependencies",
                assembly + ".dll");
            Assert.True(File.Exists(testAssembly), testAssembly);
        }

        var dependencies = Path.Combine(projectPath, "Assets", "Tests", "Dependencies");
        Assert.True(Directory.Exists(dependencies), dependencies);
        var expectedDependencies = inputs.RootElement.GetProperty("dependencies")
            .EnumerateArray()
            .Select(value => new
            {
                Name = value.GetProperty("name").GetString()!,
                Source = value.GetProperty("source").GetString()!,
                Sha256 = value.GetProperty("sha256").GetString()!,
            })
            .ToArray();
        var actualDependencyNames = Directory.EnumerateFiles(dependencies, "*.dll")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedDependencies.Select(value => value.Name).OrderBy(name => name, StringComparer.Ordinal),
            actualDependencyNames);
        foreach (var dependency in expectedDependencies)
        {
            var source = Path.GetFullPath(dependency.Source);
            var target = Path.Combine(dependencies, dependency.Name);
            Assert.True(File.Exists(source), source);
            Assert.Equal(dependency.Sha256, Sha256(source));
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(target));

            var metadata = File.ReadAllText(target + ".meta");
            Assert.Contains("isExplicitlyReferenced: 1", metadata, StringComparison.Ordinal);
            Assert.Contains("      '': Any\n    second:\n      enabled: 0", metadata, StringComparison.Ordinal);
            Assert.Contains("      Editor: Editor\n    second:\n      enabled: 1", metadata, StringComparison.Ordinal);
            Assert.DoesNotContain("Runtime: Runtime", metadata, StringComparison.Ordinal);
        }

        var references = Path.Combine(projectPath, "ReferenceAssemblies", "net8.0");
        Assert.True(Directory.Exists(references), references);
        var expectedReferences = inputs.RootElement.GetProperty("references")
            .EnumerateArray()
            .Select(value => new
            {
                Name = value.GetProperty("name").GetString()!,
                Sha256 = value.GetProperty("sha256").GetString()!,
            })
            .ToArray();
        var actualReferenceNames = Directory.EnumerateFiles(references, "*.dll")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedReferences.Select(value => value.Name).OrderBy(name => name, StringComparer.Ordinal),
            actualReferenceNames);
        foreach (var reference in expectedReferences)
        {
            var path = Path.Combine(references, reference.Name);
            Assert.Equal(reference.Sha256, Sha256(path));
        }
        Assert.True(File.Exists(Path.Combine(projectPath, "Assets", "Harness", "Spire.UnityTestHarness.asmdef")));
        Assert.True(File.Exists(Path.Combine(projectPath, "Assets", "CompilerProbe", "Spire.UnityCompilerProbeTests.asmdef")));
    }

    private static (Artifact First, Artifact Second) FindRealCollisionPair(
        string repositoryRoot,
        string configuration)
    {
        var firstPath = Path.Combine(
            repositoryRoot,
            "src",
            "Houtamelo.Spire.Analyzers",
            "bin",
            configuration,
            "netstandard2.0",
            "Houtamelo.Spire.Analyzers.dll");
        var secondPath = Path.Combine(
            repositoryRoot,
            "src",
            "Houtamelo.Spire.PatternAnalysis",
            "bin",
            configuration,
            "netstandard2.0",
            "Houtamelo.Spire.PatternAnalysis.dll");
        Assert.True(File.Exists(firstPath), firstPath);
        Assert.True(File.Exists(secondPath), secondPath);
        var first = new Artifact(firstPath, Sha256(firstPath));
        var second = new Artifact(secondPath, Sha256(secondPath));
        Assert.NotEqual(first.Hash, second.Hash);
        return (first, second);
    }

    private static string Sha256(string path)
        => DevTools.ProcessCompatibility.ToHexString(
            DevTools.ProcessCompatibility.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string FindDevToolsOutput(string repositoryRoot, string configuration)
    {
        var output = Path.Combine(
            repositoryRoot,
            "tests",
            "DevTools.Tests",
            "bin",
            configuration,
            DevToolsOutputTarget());
        Assert.True(
            File.Exists(Path.Combine(output, "DevTools.Tests.deps.json")),
            $"Missing real DevTools output manifest: {output}");
        return output;
    }

    private static string DevToolsOutputTarget()
#if SPIRE_UNITY_TEST_HOST
        => "netstandard2.1";
#else
        => "net10.0";
#endif

    private static void CopyFiles(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
    }

    private static async Task<ProcessResult> RunHarnessAsync(
        string repositoryRoot,
        string configuration,
        string projectPath,
        string editorPath)
        => await RunPythonAsync(
            repositoryRoot,
            Path.Combine(repositoryRoot, "tools", "test-unity-harness.py"),
            "--editor",
            editorPath,
            "--project",
            projectPath,
            "--configuration",
            configuration,
            "--no-build",
            "--prepare-only");

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
                    // Reusable build workers retain the redirected pipes after Python exits.
                    ["MSBUILDDISABLENODEREUSE"] = "1",
                },
            },
        };
        DevTools.ProcessCompatibility.AddArgument(process.StartInfo, scriptPath);
        foreach (var argument in arguments)
            DevTools.ProcessCompatibility.AddArgument(process.StartInfo, argument);

        Assert.True(process.Start(), $"Could not start Python process '{python}'.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(ProcessTimeout);
        var timedOut = false;
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
        => AppContext.BaseDirectory.Contains(
            Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar,
            StringComparison.Ordinal)
            ? "Release"
            : "Debug";
#endif

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "test-unity-harness.py")) &&
                    File.Exists(Path.Combine(directory.FullName, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root for Unity harness tests.");
    }

#if !SPIRE_UNITY_TEST_HOST
    public sealed class HarnessBuildFixture : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            // The no-build tests need portable inputs for the active configuration on a clean checkout.
            var repositoryRoot = FindRepositoryRoot();
            using var fixture = HarnessFixture.Create(repositoryRoot);
            var result = await RunPythonAsync(
                repositoryRoot,
                Path.Combine(repositoryRoot, "tools", "test-unity-harness.py"),
                "--editor",
                fixture.CreateIsolatedEditor(),
                "--project",
                fixture.ProjectPath,
                "--configuration",
                BuildConfiguration(),
                "--prepare-only");

            Assert.False(result.TimedOut, result.CombinedOutput);
            Assert.True(result.ExitCode == 0, result.CombinedOutput);
            AssertPreparedHarness(fixture.ProjectPath);
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }
#endif

    private sealed class HarnessFixture : IDisposable
    {
        private HarnessFixture(string root)
        {
            Root = root;
            ProjectPath = Path.Combine(root, "project");
            Directory.CreateDirectory(root);
        }

        public string Root { get; }

        public string ProjectPath { get; }

        public static HarnessFixture Create(string repositoryRoot)
            => new(Path.Combine(repositoryRoot, "tmp", "devtools-unity-harness-tests", Guid.NewGuid().ToString("N")));

        public string CreateIsolatedEditor()
        {
            var installedEditor = Environment.GetEnvironmentVariable("SPIRE_UNITY_EDITOR") ??
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Unity",
                    "Hub",
                    "Editor",
                    "6000.5.6f1",
                    "Editor",
                    "Unity");
            var installedNewtonsoft = Path.Combine(
                Path.GetDirectoryName(installedEditor)!,
                "Data",
                "Managed",
                "Newtonsoft.Json.dll");
            Assert.True(File.Exists(installedEditor), installedEditor);
            Assert.True(File.Exists(installedNewtonsoft), installedNewtonsoft);

            var editor = Path.Combine(Root, "isolated-editor", "Editor", "Unity");
            var managed = Path.Combine(Path.GetDirectoryName(editor)!, "Data", "Managed");
            Directory.CreateDirectory(managed);
            File.WriteAllBytes(editor, Array.Empty<byte>());
            File.Copy(installedNewtonsoft, Path.Combine(managed, "Newtonsoft.Json.dll"), overwrite: true);
            return editor;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed record Artifact(string Path, string Hash);

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        bool TimedOut)
    {
        public string CombinedOutput => $"{StandardOutput}\n{StandardError}";
    }
}
