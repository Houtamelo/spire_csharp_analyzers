using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Xunit;

namespace DevTools.Tests;

public sealed class UnityBehavioralRunnerTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);

    private static readonly string[] RequiredTestDependencies =
    [
        "Newtonsoft.Json.dll",
        "System.IO.Pipelines.dll",
        "System.Runtime.CompilerServices.Unsafe.dll",
        "System.Text.Encodings.Web.dll",
        "System.Text.Json.dll",
        "xunit.abstractions.dll",
        "xunit.assert.dll",
        "xunit.core.dll",
    ];

    [Fact]
    public async Task PrepareProject_CopiesBehavioralSourcesAndEditorOnlyDependencies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        AssertBuildArtifactsPresent(repositoryRoot, configuration);

        using var fixture = PreparationFixture.Create(repositoryRoot);
        var editorPath = fixture.CreateIsolatedEditor();
        var result = await RunPrepareProjectAsync(
            repositoryRoot,
            fixture.ProjectPath,
            configuration,
            rootOverride: null,
            editorPath);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.Equal(0, result.ExitCode);

        AssertCopiedBehavioralSources(repositoryRoot, fixture.ProjectPath);
        AssertPreparedManifest(fixture.ProjectPath, editorPath);
        AssertEditorOnlyDependencies(fixture.ProjectPath);

        var staleDependency = Path.Combine(
            fixture.ProjectPath,
            "Assets",
            "Tests",
            "Dependencies",
            "stale.dll");
        File.WriteAllBytes(staleDependency, [0x01, 0x02, 0x03]);
        File.WriteAllText(staleDependency + ".meta", "stale metadata");

        var repeated = await RunPrepareProjectAsync(
            repositoryRoot,
            fixture.ProjectPath,
            configuration,
            rootOverride: null,
            editorPath);

        Assert.False(repeated.TimedOut, repeated.CombinedOutput);
        Assert.Equal(0, repeated.ExitCode);
        Assert.False(File.Exists(staleDependency));
        Assert.False(File.Exists(staleDependency + ".meta"));
        AssertPreparedManifest(fixture.ProjectPath, editorPath);
        Assert.Single(
            Directory.EnumerateFiles(
                Path.Combine(fixture.ProjectPath, "TestResults"),
                "behavioral-inputs.json",
                SearchOption.AllDirectories));
    }

    [Fact]
    public async Task PrepareProject_RefusesUnrelatedTargetWithoutTouchingIt()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = PreparationFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.ProjectPath);
        var sentinel = Path.Combine(fixture.ProjectPath, "unrelated-sentinel.txt");
        File.WriteAllText(sentinel, "preserve this file", Encoding.UTF8);

        var result = await RunPrepareProjectAsync(
            repositoryRoot,
            fixture.ProjectPath,
            configuration,
            rootOverride: null);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode != 0, $"Exit code {result.ExitCode}\n{result.CombinedOutput}");
        Assert.Contains("unrelated files", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("preserve this file", File.ReadAllText(sentinel));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, ".spire-unity-behavioral")));
    }

    [Fact]
    public async Task PrepareProject_RefusesTrackedFixture()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        var trackedFixture = Path.Combine(repositoryRoot, "tests", "UnityBehavioral");

        var result = await RunPrepareProjectAsync(
            repositoryRoot,
            trackedFixture,
            configuration,
            rootOverride: null);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode != 0, $"Exit code {result.ExitCode}\n{result.CombinedOutput}");
        Assert.Contains("tracked fixture", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareProject_ReportsMissingRequiredDependencyFromIsolatedRealFixture()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        AssertBuildArtifactsPresent(repositoryRoot, configuration);

        using var fixture = PreparationFixture.Create(repositoryRoot);
        CreateIsolatedRepositoryFixture(repositoryRoot, fixture.IsolatedRoot, configuration);
        File.Delete(Path.Combine(
            fixture.IsolatedRoot,
            "tools",
            "UnityTestDependencies",
            "bin",
            configuration,
            "netstandard2.1",
            "xunit.core.dll"));

        var result = await RunPrepareProjectAsync(
            repositoryRoot,
            fixture.IsolatedProjectPath,
            configuration,
            fixture.IsolatedRoot);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode != 0, $"Exit code {result.ExitCode}\n{result.CombinedOutput}");
        Assert.Contains("Missing test dependency: xunit.core.dll", result.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrepareProject_RejectsUnsupportedBehavioralSourceShape()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        AssertBuildArtifactsPresent(repositoryRoot, configuration);

        using var fixture = PreparationFixture.Create(repositoryRoot);
        CreateIsolatedRepositoryFixture(repositoryRoot, fixture.IsolatedRoot, configuration);
        File.WriteAllText(
            Path.Combine(
                fixture.IsolatedRoot,
                "tests",
                "Houtamelo.Spire.BehavioralTests",
                "Tests",
                "UnsupportedShape.cs"),
            """
            using Xunit;

            namespace Spire.BehavioralTests.Tests;

            public class FirstShape
            {
                [Fact]
                public void ShapeFact() { }
            }

            public class SecondShape
            {
                [Fact]
                public void OtherFact() { }
            }
            """,
            Encoding.UTF8);

        var result = await RunPrepareProjectAsync(
            repositoryRoot,
            fixture.IsolatedProjectPath,
            configuration,
            fixture.IsolatedRoot);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode != 0, $"Exit code {result.ExitCode}\n{result.CombinedOutput}");
        Assert.Contains("Unsupported behavioral source shape", result.CombinedOutput, StringComparison.Ordinal);
    }

    private static void AssertCopiedBehavioralSources(string repositoryRoot, string projectPath)
    {
        var sourceRoot = Path.Combine(repositoryRoot, "tests", "Houtamelo.Spire.BehavioralTests");
        var copiedRoot = Path.Combine(projectPath, "Assets", "Tests", "Existing");
        var expected = Directory.EnumerateDirectories(sourceRoot)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs"))
            .Select(path => RelativePath(sourceRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var actual = Directory.EnumerateFiles(copiedRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => RelativePath(copiedRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
        Assert.Equal(129, CountFacts(Path.Combine(sourceRoot, "Tests")));

        using var manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(projectPath, "behavioral-inputs.json")));
        var root = manifest.RootElement;
        Assert.Equal(129, root.GetProperty("expectedFacts").GetInt32());

        var inputPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in root.GetProperty("inputs").EnumerateArray())
        {
            var relativeSource = input.GetProperty("source").GetString()!;
            var suffix = "tests/Houtamelo.Spire.BehavioralTests/";
            Assert.StartsWith(suffix, relativeSource, StringComparison.Ordinal);
            var relative = relativeSource[suffix.Length..];
            Assert.True(inputPaths.Add(relative), $"Duplicate behavioral input: {relativeSource}");

            var sourcePath = Path.Combine(repositoryRoot, relativeSource.Replace('/', Path.DirectorySeparatorChar));
            var copiedPath = Path.Combine(copiedRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(sourcePath), sourcePath);
            Assert.True(File.Exists(copiedPath), copiedPath);
            Assert.Equal(File.ReadAllBytes(sourcePath), File.ReadAllBytes(copiedPath));
            Assert.Equal(
                input.GetProperty("sha256").GetString(),
                DevTools.ProcessCompatibility.ToHexString(DevTools.ProcessCompatibility.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant());
        }

        Assert.Equal(expected, inputPaths.OrderBy(path => path, StringComparer.Ordinal));

        var expectedNames = ExpectedFactNames(Path.Combine(sourceRoot, "Tests"));
        var manifestNames = root.GetProperty("expectedFactNames")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        Assert.Equal(expectedNames, manifestNames);
        Assert.Equal(manifestNames.Length, manifestNames.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(129, manifestNames.Length);
    }

    private static void AssertPreparedManifest(string projectPath, string? editorPath)
    {
        var packageRuntime = Path.Combine(
            projectPath,
            "Packages",
            "com.houtamelo.spire",
            "Runtime",
            "Houtamelo.Spire.dll");
        Assert.True(File.Exists(packageRuntime), packageRuntime);

        var asmdefPath = Path.Combine(
            projectPath,
            "Assets",
            "Tests",
            "Spire.UnityBehavioralTests.asmdef");
        using var asmdef = JsonDocument.Parse(File.ReadAllText(asmdefPath));
        var definition = asmdef.RootElement;
        Assert.True(definition.GetProperty("overrideReferences").GetBoolean());
        Assert.Contains(
            "Editor",
            definition.GetProperty("includePlatforms").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains(
            "TestAssemblies",
            definition.GetProperty("optionalUnityReferences").EnumerateArray().Select(value => value.GetString()));

        var rsp = File.ReadAllText(Path.Combine(projectPath, "Assets", "Tests", "csc.rsp"));
        Assert.Equal("-langversion:12\n-nullable:enable\n", rsp);

        using var inputs = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(projectPath, "behavioral-inputs.json")));
        var dependencies = inputs.RootElement.GetProperty("testDependencies")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var dependencyRoot = Path.Combine(projectPath, "Assets", "Tests", "Dependencies");
        var actual = Directory.EnumerateFiles(dependencyRoot, "*.dll")
            .Select(path => Path.GetFileName(path))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(dependencies, actual);
        Assert.Contains("Houtamelo.Spire.dll", definition.GetProperty("precompiledReferences")
            .EnumerateArray().Select(value => value.GetString()));
        foreach (var dependency in RequiredTestDependencies)
            Assert.Contains(dependency, actual);
        Assert.DoesNotContain(actual, dependency => dependency == "UnityTestDependencies.dll");
        using var editorManifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(projectPath, "behavioral-inputs.json")));
        var editorDependencies = editorManifest.RootElement
            .GetProperty("editorProvidedDependencies")
            .EnumerateObject()
            .ToArray();
        if (editorPath is null)
        {
            Assert.Empty(editorDependencies);
        }
        else
        {
            var dependency = Assert.Single(editorDependencies);
            Assert.Equal("Newtonsoft.Json.dll", dependency.Name);
            var source = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(editorPath)!,
                "Data",
                "Managed",
                "Newtonsoft.Json.dll"));
            Assert.Equal(source, dependency.Value.GetProperty("source").GetString());
            Assert.Equal(
                DevTools.ProcessCompatibility.ToHexString(DevTools.ProcessCompatibility.HashData(File.ReadAllBytes(source))).ToLowerInvariant(),
                dependency.Value.GetProperty("sha256").GetString());
            Assert.Equal(
                File.ReadAllBytes(source),
                File.ReadAllBytes(Path.Combine(dependencyRoot, "Newtonsoft.Json.dll")));
        }
    }

    private static void AssertEditorOnlyDependencies(string projectPath)
    {
        var dependencyRoot = Path.Combine(projectPath, "Assets", "Tests", "Dependencies");
        foreach (var dependency in Directory.EnumerateFiles(dependencyRoot, "*.dll"))
        {
            var metadata = File.ReadAllText(dependency + ".meta");
            Assert.Contains("isExplicitlyReferenced: 1", metadata, StringComparison.Ordinal);
            Assert.Contains("      '': Any\n    second:\n      enabled: 0", metadata, StringComparison.Ordinal);
            Assert.Contains("      Editor: Editor\n    second:\n      enabled: 1", metadata, StringComparison.Ordinal);
            Assert.DoesNotContain("Runtime: Runtime", metadata, StringComparison.Ordinal);
        }
    }

    private static int CountFacts(string sourceRoot)
        => Directory.EnumerateFiles(sourceRoot, "*.cs")
            .Sum(path => System.Text.RegularExpressions.Regex.Matches(
                File.ReadAllText(path),
                @"\[Fact(?:\([^\]]*\))?\]").Count);

    private static string[] ExpectedFactNames(string testsRoot)
    {
        var names = new List<string>();
        foreach (var source in Directory.EnumerateFiles(testsRoot, "*.cs"))
        {
            var text = File.ReadAllText(source);
            var namespaceMatch = Regex.Match(text, @"^namespace\s+([\w.]+)\s*;", RegexOptions.Multiline);
            var classMatch = Regex.Match(text, @"^public\s+class\s+(\w+)\b", RegexOptions.Multiline);
            Assert.True(namespaceMatch.Success, $"Missing file-scoped namespace: {source}");
            Assert.True(classMatch.Success, $"Missing public test class: {source}");
            foreach (Match method in Regex.Matches(
                         text,
                         @"\[Fact(?:\([^\]]*\))?\]\s+public\s+(?:async\s+)?(?:void|Task|ValueTask)\s+(\w+)\s*\("))
            {
                names.Add($"{namespaceMatch.Groups[1].Value}.{classMatch.Groups[1].Value}.{method.Groups[1].Value}");
            }
        }

        return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private static void AssertBuildArtifactsPresent(string repositoryRoot, string configuration)
    {
        foreach (var name in new[]
                 {
                     "Houtamelo.Spire",
                     "Houtamelo.Spire.Analyzers",
                     "Houtamelo.Spire.PatternAnalysis",
                     "Houtamelo.Spire.CodeFixes",
                 })
        {
            var path = Path.Combine(
                repositoryRoot,
                "src",
                name,
                "bin",
                configuration,
                "netstandard2.0",
                name + ".dll");
            Assert.True(File.Exists(path), $"Missing {configuration} artifact: {path}");
        }

        var dependencyRoot = Path.Combine(
            repositoryRoot,
            "tools",
            "UnityTestDependencies",
            "bin",
            configuration,
            "netstandard2.1");
        foreach (var dependency in RequiredTestDependencies)
            Assert.True(File.Exists(Path.Combine(dependencyRoot, dependency)), dependencyRoot);
    }

    private static void CreateIsolatedRepositoryFixture(
        string repositoryRoot,
        string isolatedRoot,
        string configuration)
    {
        CopyDirectory(
            Path.Combine(repositoryRoot, "tests", "UnityBehavioral", "Assets"),
            Path.Combine(isolatedRoot, "tests", "UnityBehavioral", "Assets"));
        CopyDirectory(
            Path.Combine(repositoryRoot, "tests", "UnityBehavioral", "Packages"),
            Path.Combine(isolatedRoot, "tests", "UnityBehavioral", "Packages"));
        CopyDirectory(
            Path.Combine(repositoryRoot, "tests", "UnityIntegration", "ProjectSettings"),
            Path.Combine(isolatedRoot, "tests", "UnityIntegration", "ProjectSettings"));
        CopyDirectory(
            Path.Combine(repositoryRoot, "tests", "Houtamelo.Spire.BehavioralTests", "Types"),
            Path.Combine(isolatedRoot, "tests", "Houtamelo.Spire.BehavioralTests", "Types"));
        CopyDirectory(
            Path.Combine(repositoryRoot, "tests", "Houtamelo.Spire.BehavioralTests", "Tests"),
            Path.Combine(isolatedRoot, "tests", "Houtamelo.Spire.BehavioralTests", "Tests"));
        CopyFile(repositoryRoot, isolatedRoot, "LICENSE");
        CopyFile(repositoryRoot, isolatedRoot, "tools/build-unity-package.py");

        foreach (var name in new[]
                 {
                     "Houtamelo.Spire",
                     "Houtamelo.Spire.Analyzers",
                     "Houtamelo.Spire.CodeFixes",
                 })
        {
            CopyFile(
                repositoryRoot,
                isolatedRoot,
                $"src/{name}/{name}.csproj");
            CopyFile(
                repositoryRoot,
                isolatedRoot,
                $"src/{name}/bin/{configuration}/netstandard2.0/{name}.dll");
        }

        CopyFile(
            repositoryRoot,
            isolatedRoot,
            $"src/Houtamelo.Spire.PatternAnalysis/bin/{configuration}/netstandard2.0/Houtamelo.Spire.PatternAnalysis.dll");
        CopyDirectory(
            Path.Combine(
                repositoryRoot,
                "tools",
                "UnityTestDependencies",
                "bin",
                configuration,
                "netstandard2.1"),
            Path.Combine(
                isolatedRoot,
                "tools",
                "UnityTestDependencies",
                "bin",
                configuration,
                "netstandard2.1"));
    }

    private static async Task<ProcessResult> RunPrepareProjectAsync(
        string repositoryRoot,
        string projectPath,
        string configuration,
        string? rootOverride,
        string? editorPath = null)
    {
        var scriptDirectory = Path.Combine(
            repositoryRoot,
            "tmp",
            "devtools-unity-behavioral-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scriptDirectory);
        var scriptPath = Path.Combine(scriptDirectory, "prepare.py");
        var script = $"""
            import runpy
            from pathlib import Path

            module = runpy.run_path({PythonString(Path.Combine(repositoryRoot, "tools", "test-unity-behavioral.py"))})
            {(rootOverride is null ? "" : $"module[\"prepare_project\"].__globals__[\"ROOT\"] = Path({PythonString(rootOverride)})")}
            module["prepare_project"](
                Path({PythonString(projectPath)}),
                {PythonString(configuration)},
                False{(editorPath is null ? "" : $", Path({PythonString(editorPath)})")})
            """;
        File.WriteAllText(scriptPath, script, Encoding.UTF8);

        try
        {
            return await RunPythonAsync(repositoryRoot, scriptPath);
        }
        finally
        {
            if (Directory.Exists(scriptDirectory))
                Directory.Delete(scriptDirectory, recursive: true);
        }
    }

    private static async Task<ProcessResult> RunPythonAsync(string workingDirectory, string scriptPath)
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
        => AppContext.BaseDirectory.Contains(
            Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar,
            StringComparison.Ordinal)
            ? "Release"
            : "Debug";
#endif

    private static void CopyFile(string sourceRoot, string targetRoot, string relativePath)
    {
        var source = Path.Combine(sourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var target = Path.Combine(targetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: true);
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "test-unity-behavioral.py")) &&
                    File.Exists(Path.Combine(directory.FullName, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root for Unity behavioral runner tests.");
    }

    private static string RelativePath(string root, string path)
        => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string PythonString(string value)
        => JsonSerializer.Serialize(value);

    private sealed class PreparationFixture : IDisposable
    {
        private PreparationFixture(string root)
        {
            Root = root;
            ProjectPath = Path.Combine(root, "project");
            IsolatedRoot = Path.Combine(root, "isolated-root");
            IsolatedProjectPath = Path.Combine(IsolatedRoot, "project");
            Directory.CreateDirectory(root);
        }

        public string Root { get; }

        public string ProjectPath { get; }

        public string IsolatedRoot { get; }

        public string IsolatedProjectPath { get; }

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

            var editorPath = Path.Combine(Root, "isolated-editor", "Editor");
            var editorManagedDirectory = Path.Combine(
                Path.GetDirectoryName(editorPath)!,
                "Data",
                "Managed");
            Directory.CreateDirectory(editorManagedDirectory);
            File.WriteAllBytes(editorPath, Array.Empty<byte>());
            File.Copy(
                installedNewtonsoft,
                Path.Combine(editorManagedDirectory, "Newtonsoft.Json.dll"),
                overwrite: true);
            return editorPath;
        }

        public static PreparationFixture Create(string repositoryRoot)
        {
            var root = Path.Combine(
                repositoryRoot,
                "tmp",
                "devtools-unity-behavioral-tests",
                Guid.NewGuid().ToString("N"));
            return new PreparationFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        bool TimedOut)
    {
        public string CombinedOutput => $"{StandardOutput}\n{StandardError}";
    }
}
