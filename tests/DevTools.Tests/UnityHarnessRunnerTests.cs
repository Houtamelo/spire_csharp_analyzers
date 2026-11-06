using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Spire.Tool;
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

        Assert.True(result.ExitCode == 0, CombinedOutput(result));
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
        Assert.True(first.ExitCode == 0, CombinedOutput(first));

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

        Assert.True(second.ExitCode == 0, CombinedOutput(second));
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

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ownership marker is invalid", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("owned by another preparation\n", File.ReadAllText(marker));
        Assert.Equal("preserve me", File.ReadAllText(sentinel));
        Assert.False(Directory.Exists(Path.Combine(fixture.ProjectPath, "Assets")));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, "harness-inputs.json")));
    }

    [Fact]
    public void ImportDependency_RejectsDifferentRealBytesForTheSameDestinationName()
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
        HarnessWorkflow.ImportDependency(sourceA, dependencies);
        HarnessWorkflow.ImportDependency(sourceA, dependencies);
        var exception = Assert.ThrowsAny<Exception>(
            () => HarnessWorkflow.ImportDependency(sourceB, dependencies));

        Assert.Contains(pair.First.Hash, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(pair.Second.Hash, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(File.ReadAllBytes(sourceA), File.ReadAllBytes(Path.Combine(dependencies, "SharedDependency.dll")));
        Assert.True(File.Exists(Path.Combine(dependencies, "SharedDependency.dll.meta")));
    }

    [Fact]
    public void RuntimeDependencies_FiltersOrphanOutputDllsUsingTheRealDepsManifest()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = HarnessFixture.Create(repositoryRoot);
        var sourceOutput = FindDevToolsOutput(repositoryRoot, configuration);
        CopyFiles(sourceOutput, fixture.Root);
        File.Copy(
            Path.Combine(sourceOutput, "DevTools.Tests.dll"),
            Path.Combine(fixture.Root, "orphan.dll"));
        var actual = HarnessWorkflow.RuntimeDependencies(fixture.Root, "DevTools.Tests")
            .Select(Path.GetFileName)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Contains("DevTools.Tests.dll", actual);
        Assert.DoesNotContain("orphan.dll", actual);
        Assert.Equal(actual.Length, actual.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RuntimeDependencies_RejectsAReferencedDllMissingFromTheRealOutput()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = HarnessFixture.Create(repositoryRoot);
        var sourceOutput = FindDevToolsOutput(repositoryRoot, configuration);
        CopyFiles(sourceOutput, fixture.Root);
        var missing = Path.Combine(fixture.Root, "DevTools.Tests.dll");
        File.Delete(missing);
        var exception = Assert.ThrowsAny<Exception>(
            () => HarnessWorkflow.RuntimeDependencies(fixture.Root, "DevTools.Tests"));
        Assert.Contains("DevTools.Tests.dll", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunUnity_TimeoutKillsTheRealChildProcessBeforeRethrowing()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = HarnessFixture.Create(repositoryRoot);
        var childScript = Path.Combine(fixture.Root, "run-child.sh");
        var pidFile = Path.Combine(fixture.Root, "child.pid");
        File.WriteAllText(
            childScript,
            "#!/bin/sh\n"
            + "set -eu\n"
            + "pid_file=\"$1\"\n"
            + "sleep 30 &\n"
            + "child=$!\n"
            + "printf '%s\\n' \"$child\" > \"$pid_file\"\n"
            + "wait \"$child\"\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var timeout = Assert.ThrowsAsync<TimeoutException>(
            () => ProcessRunner.RunAsync(
                "/bin/sh",
                new[] { childScript, pidFile },
                fixture.Root,
                TimeSpan.FromMilliseconds(200),
                ownProcessGroup: true));
        await timeout;

        Assert.True(SpinWait.SpinUntil(
            () => File.Exists(pidFile) && !IsProcessRunning(int.Parse(File.ReadAllText(pidFile))),
            TimeSpan.FromSeconds(3)),
            "Timed out child survived process-group cleanup.");
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
        AssertNoSupportTheoryRows(cases);
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
            Assert.Contains("      Any:\n    second:\n      enabled: 0", metadata, StringComparison.Ordinal);
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
        var compilerProbe = Path.Combine(projectPath, "Assets", "CompilerProbe");
        Assert.True(File.Exists(Path.Combine(compilerProbe, "Spire.UnityCompilerProbeTests.asmdef")));
        Assert.True(File.Exists(Path.Combine(compilerProbe, "UnityCompilerProbeTests.cs")));
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
        string editorPath,
        bool build = false)
    {
        var cliPath = Path.Combine(
            repositoryRoot,
            "tools",
            "Spire.Tool",
            "bin",
            configuration,
            "net10.0",
            "Spire.Tool.dll");
        Assert.True(File.Exists(cliPath), $"Missing Spire.Tool CLI: {cliPath}");
        var arguments = new List<string>
        {
            cliPath,
            "unity-harness",
            "--editor",
            editorPath,
            "--project",
            projectPath,
            "--configuration",
            configuration,
        };
        if (!build) arguments.Add("--no-build");
        arguments.Add("--prepare-only");

        return await ProcessRunner.RunAsync(
            new ToolContext(repositoryRoot).Dotnet,
            arguments,
            repositoryRoot,
            ProcessTimeout,
            new Dictionary<string, string?>
            {
                ["CI"] = "true",
                ["GIT_TERMINAL_PROMPT"] = "0",
                ["MSBUILDDISABLENODEREUSE"] = "1",
            });
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            return !Process.GetProcessById(processId).HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void AssertNoSupportTheoryRows(JsonElement[] cases)
    {
        const string methodName =
            "Houtamelo.Spire.SourceGenerators.Tests.Unity.UnityDrawerSharedSupportTests." +
            "EditorDrawer_NoSupportForNoUnionOrRejectedInput";
        var rows = cases
            .Where(value => value.GetProperty("identity").GetString()!.Contains(
                methodName,
                StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(6, rows.Length);
        Assert.Equal(
            rows.Length,
            rows.Select(value => value.GetProperty("identity").GetString()!)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(rows, row =>
        {
            var arguments = row.GetProperty("arguments");
            Assert.Equal(JsonValueKind.Array, arguments.ValueKind);
            Assert.Equal(3, arguments.GetArrayLength());
        });

        foreach (var marker in new[]
                 {
                     "UnityNormalAdditive",
                     "NoUnionInput",
                     "UnityRecordRejectedInput",
                     "UnityMissingEngineInput",
                     "UnityZeroVariantInput",
                     "UnityReadonlyRejectedInput",
                 })
        {
            Assert.Contains(
                rows,
                row => row.GetProperty("arguments")[0].GetString()!.Contains(
                    marker,
                    StringComparison.Ordinal));
        }
    }

    private static string CombinedOutput(ProcessResult result)
        => $"{result.StandardOutput}\n{result.StandardError}";

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
                if (File.Exists(Path.Combine(directory.FullName, "tools", "Spire.Tool", "Spire.Tool.csproj")) &&
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
            var result = await RunHarnessAsync(
                repositoryRoot,
                BuildConfiguration(),
                fixture.ProjectPath,
                fixture.CreateIsolatedEditor(),
                build: true);

            Assert.True(result.ExitCode == 0, CombinedOutput(result));
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

}
