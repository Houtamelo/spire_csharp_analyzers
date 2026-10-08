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
using System.Xml.Linq;
using Xunit;

namespace DevTools.Tests;

public sealed class UnityPackagingTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);

    private static readonly string[] ExpectedDlls =
    [
        "Runtime/Houtamelo.Spire.dll",
        "Analyzers/Houtamelo.Spire.Analyzers.dll",
        "Analyzers/Houtamelo.Spire.PatternAnalysis.dll",
        "Analyzers/Houtamelo.Spire.CodeFixes.dll",
    ];

    [Fact]
    public async Task BuildPackage_DebugNoBuild_ExportsExpectedUnitySurfaceAndMetadata()
    {
        var repositoryRoot = FindRepositoryRoot();
        AssertDebugArtifactsPresent(repositoryRoot);

        using var fixture = PackageFixture.Create(repositoryRoot);
        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.Equal(0, result.ExitCode);
        AssertDllWhitelist(fixture.OutputPath);
        AssertManifest(fixture.OutputPath, ReadCoreVersion(repositoryRoot));
        AssertPluginMetadata(fixture.OutputPath, "Runtime/Houtamelo.Spire.dll", analyzer: false);
        AssertPluginMetadata(
            fixture.OutputPath,
            "Analyzers/Houtamelo.Spire.Analyzers.dll",
            analyzer: true);
        AssertPluginMetadata(
            fixture.OutputPath,
            "Analyzers/Houtamelo.Spire.PatternAnalysis.dll",
            analyzer: true);
        AssertPluginMetadata(
            fixture.OutputPath,
            "Analyzers/Houtamelo.Spire.CodeFixes.dll",
            analyzer: true);
        AssertDeterministicMetaGuids(fixture.OutputPath);
    }

    [Fact]
    public async Task BuildPackage_RepeatedExportToSameDirectoryIsSafeAndDeterministic()
    {
        var repositoryRoot = FindRepositoryRoot();
        AssertDebugArtifactsPresent(repositoryRoot);

        using var fixture = PackageFixture.Create(repositoryRoot);
        var first = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath);
        Assert.False(first.TimedOut, first.CombinedOutput);
        Assert.True(first.ExitCode == 0, first.CombinedOutput);
        var before = CaptureFileDigests(fixture.OutputPath);

        var second = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath);
        Assert.False(second.TimedOut, second.CombinedOutput);
        Assert.True(second.ExitCode == 0, second.CombinedOutput);

        Assert.Equal(before, CaptureFileDigests(fixture.OutputPath));
        AssertDllWhitelist(fixture.OutputPath);
    }

    [Fact]
    public async Task BuildPackage_RefusesUnrelatedExistingOutputBeforeWritingDlls()
    {
        var repositoryRoot = FindRepositoryRoot();
        AssertDebugArtifactsPresent(repositoryRoot);

        using var fixture = PackageFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.OutputPath);
        var sentinelPath = Path.Combine(fixture.OutputPath, "unrelated-sentinel.txt");
        File.WriteAllText(sentinelPath, "preserve this sentinel");

        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("unrelated", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("preserve this sentinel", File.ReadAllText(sentinelPath));
        Assert.Empty(Directory.EnumerateFiles(fixture.OutputPath, "*.dll", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task BuildPackage_VersionMismatchGuardUsesRealRunpyAndCreatesNoOutput()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = PackageFixture.Create(repositoryRoot);
        CreateVersionFixture(fixture.Root);

        var scriptPath = Path.Combine(fixture.Root, "version-mismatch-guard.py");
        File.WriteAllText(
            scriptPath,
            $"""
            import runpy
            from pathlib import Path

            module = runpy.run_path({PythonString(Path.Combine(repositoryRoot, "tools", "build-unity-package.py"))})
            build_package = module["build_package"]
            build_package.__globals__["ROOT"] = Path({PythonString(fixture.Root)})
            output = Path({PythonString(fixture.OutputPath)})

            try:
                build_package(output, "Debug", False)
            except ValueError as error:
                print("EXPECTED_VERSION_MISMATCH:" + str(error))
                raise SystemExit(0)

            raise SystemExit("The mismatched project fixture was unexpectedly accepted.")
            """);

        var result = await RunPythonAsync(
            fixture.Root,
            scriptPath);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("EXPECTED_VERSION_MISMATCH", result.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("versions must match", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(fixture.OutputPath));
    }

    private static async Task<ProcessResult> RunPackageBuilderAsync(
        string repositoryRoot,
        string outputPath)
    {
        var scriptPath = Path.Combine(repositoryRoot, "tools", "build-unity-package.py");
        return await RunPythonAsync(
            repositoryRoot,
            scriptPath,
            "--output",
            outputPath,
            "--configuration",
            "Debug",
            "--no-build");
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

    private static void AssertDebugArtifactsPresent(string repositoryRoot)
    {
        foreach (var relativePath in ExpectedDlls)
        {
            var sourceName = Path.GetFileName(relativePath);
            var sourceDirectory = relativePath.StartsWith("Runtime/", StringComparison.Ordinal)
                ? "Houtamelo.Spire"
                : sourceName.StartsWith("Houtamelo.Spire.Analyzers", StringComparison.Ordinal)
                    ? "Houtamelo.Spire.Analyzers"
                    : sourceName.StartsWith("Houtamelo.Spire.PatternAnalysis", StringComparison.Ordinal)
                        ? "Houtamelo.Spire.Analyzers"
                        : "Houtamelo.Spire.CodeFixes";
            var sourcePath = Path.Combine(
                repositoryRoot,
                "src",
                sourceDirectory,
                "bin",
                "Debug",
                "netstandard2.0",
                sourceName);
            Assert.True(
                File.Exists(sourcePath),
                $"Debug artifact is missing; build the core projects before running this --no-build test: {sourcePath}");
        }
    }

    private static void AssertDllWhitelist(string outputPath)
    {
        var actual = Directory.EnumerateFiles(outputPath, "*.dll", SearchOption.AllDirectories)
            .Select(path => RelativePath(outputPath, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var expected = ExpectedDlls.OrderBy(path => path, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, actual);
        Assert.DoesNotContain(actual, path =>
            path.Contains("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Workspaces", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("dotnet", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertManifest(string outputPath, string expectedVersion)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputPath, "package.json")));
        var manifest = document.RootElement;
        Assert.Equal("com.houtamelo.spire", manifest.GetProperty("name").GetString());
        Assert.Equal(expectedVersion, manifest.GetProperty("version").GetString());
        Assert.Equal("6000.5", manifest.GetProperty("unity").GetString());

        var dependencies = manifest.GetProperty("dependencies");
        Assert.Equal(
            new[] { "com.unity.modules.imgui", "com.unity.modules.uielements" },
            dependencies.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal("1.0.0", dependencies.GetProperty("com.unity.modules.imgui").GetString());
        Assert.Equal("1.0.0", dependencies.GetProperty("com.unity.modules.uielements").GetString());
    }

    private static void AssertPluginMetadata(string outputPath, string relativeDll, bool analyzer)
    {
        var metaPath = Path.Combine(outputPath, relativeDll + ".meta");
        var metadata = File.ReadAllText(metaPath);
        var enabled = analyzer ? 0 : 1;

        if (analyzer)
            Assert.Contains("labels:\n- RoslynAnalyzer\n", metadata, StringComparison.Ordinal);
        else
            Assert.DoesNotContain("RoslynAnalyzer", metadata, StringComparison.Ordinal);

        Assert.Contains(
            $"      : Any\n    second:\n      enabled: {enabled}\n",
            metadata,
            StringComparison.Ordinal);
        Assert.Contains(
            $"      Editor: Editor\n    second:\n      enabled: {enabled}\n",
            metadata,
            StringComparison.Ordinal);
    }

    private static void AssertDeterministicMetaGuids(string outputPath)
    {
        foreach (var metaPath in Directory.EnumerateFiles(outputPath, "*.meta", SearchOption.AllDirectories))
        {
            var relativeMeta = RelativePath(outputPath, metaPath);
            var logicalPath = relativeMeta[..^".meta".Length];
            var expected = DevTools.ProcessCompatibility.ToHexString(
                    DevTools.ProcessCompatibility.HashData(Encoding.UTF8.GetBytes("Houtamelo.Spire.Unity/" + logicalPath)))
                .ToLowerInvariant()[..32];
            var guidLine = File.ReadLines(metaPath)
                .Single(line => line.StartsWith("guid: ", StringComparison.Ordinal));
            Assert.Equal("guid: " + expected, guidLine);
        }
    }

    private static Dictionary<string, string> CaptureFileDigests(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => RelativePath(root, path),
                path => DevTools.ProcessCompatibility.ToHexString(DevTools.ProcessCompatibility.HashData(File.ReadAllBytes(path))),
                StringComparer.Ordinal);

    private static void CreateVersionFixture(string root)
    {
        CreateVersionProject(root, "Houtamelo.Spire", "1.0.0");
        CreateVersionProject(root, "Houtamelo.Spire.Analyzers", "1.0.1");
        CreateVersionProject(root, "Houtamelo.Spire.CodeFixes", "1.0.0");
    }

    private static void CreateVersionProject(string root, string name, string version)
    {
        var directory = Path.Combine(root, "src", name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, name + ".csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>netstandard2.0</TargetFramework>
                <Version>{version}</Version>
              </PropertyGroup>
            </Project>
            """);
    }

    private static string ReadCoreVersion(string repositoryRoot)
        => XDocument.Load(
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "Houtamelo.Spire",
                    "Houtamelo.Spire.csproj"))
            .Descendants("Version")
            .Select(element => element.Value)
            .Single();

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "build-unity-package.py")) &&
                    File.Exists(Path.Combine(directory.FullName, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root for Unity packaging tests.");
    }

    private static string RelativePath(string root, string path)
        => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string PythonString(string value)
        => JsonSerializer.Serialize(value);

    private sealed class PackageFixture : IDisposable
    {
        private PackageFixture(string root)
        {
            Root = root;
            OutputPath = Path.Combine(root, "package");
            Directory.CreateDirectory(root);
        }

        public string Root { get; }

        public string OutputPath { get; }

        public static PackageFixture Create(string repositoryRoot)
        {
            var root = Path.Combine(
                repositoryRoot,
                "tmp",
                "devtools-unity-package-tests",
                Guid.NewGuid().ToString("N"));
            return new PackageFixture(root);
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
