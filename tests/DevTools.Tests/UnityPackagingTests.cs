using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class UnityPackagingTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);

    internal static readonly string[] ExpectedDlls =
    [
        "Runtime/Houtamelo.Spire.dll",
        "Runtime/Houtamelo.Spire.Functional.dll",
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

        Assert.Equal(0, result.ExitCode);
        AssertDllWhitelist(fixture.OutputPath);
        AssertManifest(fixture.OutputPath, ReadCoreVersion(repositoryRoot));
        AssertPackageReadme(fixture.OutputPath);
        UnityCustomerDeliveryTestSupport.AssertOnlyAllowlistedCustomerFiles(
            repositoryRoot,
            fixture.OutputPath);
        UnityCustomerDeliveryTestSupport.AssertGeneratedMetaGuids(
            repositoryRoot,
            fixture.OutputPath);
        UnityCustomerDeliveryTestSupport.AssertAuthoredShowcaseMetaGuids(
            repositoryRoot,
            fixture.OutputPath);
        AssertPluginMetadata(fixture.OutputPath, "Runtime/Houtamelo.Spire.dll", analyzer: false);
        AssertPluginMetadata(fixture.OutputPath, "Runtime/Houtamelo.Spire.Functional.dll", analyzer: false);
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
        AssertDeterministicMetaGuids(repositoryRoot, fixture.OutputPath);
    }

    [Fact]
    public async Task BuildPackage_RepeatedExportToSameDirectoryIsSafeAndDeterministic()
    {
        var repositoryRoot = FindRepositoryRoot();
        AssertDebugArtifactsPresent(repositoryRoot);

        using var fixture = PackageFixture.Create(repositoryRoot);
        var first = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath);
        Assert.True(first.ExitCode == 0, CombinedOutput(first));
        var before = CaptureFileDigests(fixture.OutputPath);

        var second = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath);
        Assert.True(second.ExitCode == 0, CombinedOutput(second));

        Assert.Equal(before, CaptureFileDigests(fixture.OutputPath));
        AssertDllWhitelist(fixture.OutputPath);
        UnityCustomerDeliveryTestSupport.AssertOnlyAllowlistedCustomerFiles(
            repositoryRoot,
            fixture.OutputPath);
        UnityCustomerDeliveryTestSupport.AssertGeneratedMetaGuids(
            repositoryRoot,
            fixture.OutputPath);
        UnityCustomerDeliveryTestSupport.AssertAuthoredShowcaseMetaGuids(
            repositoryRoot,
            fixture.OutputPath);
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
        var nestedSentinelPath = Path.Combine(fixture.OutputPath, "nested", "unrelated-sentinel.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(nestedSentinelPath)!);
        File.WriteAllText(nestedSentinelPath, "preserve nested sentinel");

        var result = await RunPackageBuilderAsync(repositoryRoot, fixture.OutputPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("unrelated", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("preserve this sentinel", File.ReadAllText(sentinelPath));
        Assert.Equal("preserve nested sentinel", File.ReadAllText(nestedSentinelPath));
        Assert.Equal(
            new[] { "nested/unrelated-sentinel.txt", "unrelated-sentinel.txt" },
            Directory.EnumerateFiles(fixture.OutputPath, "*", SearchOption.AllDirectories)
                .Select(path => RelativePath(fixture.OutputPath, path))
                .OrderBy(path => path, StringComparer.Ordinal));
    }

    [Fact]
    public async Task BuildPackage_VersionMismatchGuardUsesRealCliAndCreatesNoOutput()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = PackageFixture.Create(repositoryRoot);
        CreateVersionFixture(fixture.Root);

        var result = await RunPackageBuilderAsync(
            repositoryRoot,
            fixture.OutputPath,
            fixture.Root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("versions must match", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(fixture.OutputPath));
    }

    private static async Task<ProcessResult> RunPackageBuilderAsync(
        string repositoryRoot,
        string outputPath,
        string? rootOverride = null)
    {
        var configuration = "Debug";
        var launcherConfiguration = BuildConfiguration();
        var cliPath = Path.Combine(
            repositoryRoot,
            "tools",
            "Spire.Tool",
            "bin",
            launcherConfiguration,
            "net10.0",
            "Spire.Tool.dll");
        Assert.True(File.Exists(cliPath), $"Missing Spire.Tool CLI: {cliPath}");

        var arguments = new List<string>();
        if (rootOverride is not null)
        {
            arguments.Add("--root");
            arguments.Add(rootOverride);
        }

        arguments.Add("package");
        arguments.Add("--output");
        arguments.Add(outputPath);
        arguments.Add("--configuration");
        arguments.Add(configuration);
        arguments.Add("--no-build");

        return await ProcessRunner.RunAsync(
            new ToolContext(repositoryRoot).Dotnet,
            new[] { cliPath }.Concat(arguments),
            repositoryRoot,
            ProcessTimeout,
            new Dictionary<string, string?>
            {
                ["CI"] = "true",
                ["GIT_TERMINAL_PROMPT"] = "0",
            });
    }

    private static string CombinedOutput(ProcessResult result)
        => $"{result.StandardOutput}\n{result.StandardError}";

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

    private static void AssertDebugArtifactsPresent(string repositoryRoot)
    {
        var configuration = BuildConfiguration();
        foreach (var relativePath in ExpectedDlls)
        {
            var sourceName = Path.GetFileName(relativePath);
            var sourceDirectory = relativePath.StartsWith("Runtime/", StringComparison.Ordinal)
                ? sourceName.StartsWith("Houtamelo.Spire.Functional", StringComparison.Ordinal)
                    ? "Houtamelo.Spire.Functional"
                    : "Houtamelo.Spire"
                : sourceName.StartsWith("Houtamelo.Spire.Analyzers", StringComparison.Ordinal)
                    ? "Houtamelo.Spire.Analyzers"
                    : sourceName.StartsWith("Houtamelo.Spire.PatternAnalysis", StringComparison.Ordinal)
                        ? "Houtamelo.Spire.PatternAnalysis"
                        : "Houtamelo.Spire.CodeFixes";
            var sourcePath = Path.Combine(
                repositoryRoot,
                "src",
                sourceDirectory,
                "bin",
                configuration,
                "netstandard2.0",
                sourceName);
            Assert.True(
                File.Exists(sourcePath),
                $"{configuration} artifact is missing; build the core projects before running this --no-build test: {sourcePath}");
        }

        var xmlPath = Path.Combine(
            repositoryRoot,
            "src",
            "Houtamelo.Spire",
            "bin",
            configuration,
            "netstandard2.0",
            "Houtamelo.Spire.xml");
        Assert.True(
            File.Exists(xmlPath),
            $"Runtime XML artifact is missing; build the core project before running this --no-build test: {xmlPath}");

        var functionalXmlPath = Path.Combine(
            repositoryRoot,
            "src",
            "Houtamelo.Spire.Functional",
            "bin",
            configuration,
            "netstandard2.0",
            "Houtamelo.Spire.Functional.xml");
        Assert.True(
            File.Exists(functionalXmlPath),
            $"Functional XML artifact is missing; build the Functional project before running this --no-build test: {functionalXmlPath}");
    }

    internal static void AssertDllWhitelist(string outputPath)
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
        Assert.Equal("individual.antonio-pedro-goncalves-ferreira.spire", manifest.GetProperty("name").GetString());
        Assert.Equal(expectedVersion, manifest.GetProperty("version").GetString());
        Assert.Equal("2022.3", manifest.GetProperty("unity").GetString());
        Assert.Equal("62f3", manifest.GetProperty("unityRelease").GetString());

        var dependencies = manifest.GetProperty("dependencies");
        Assert.Equal(
            new[] { "com.unity.modules.imgui", "com.unity.modules.uielements" },
            dependencies.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal("1.0.0", dependencies.GetProperty("com.unity.modules.imgui").GetString());
        Assert.Equal("1.0.0", dependencies.GetProperty("com.unity.modules.uielements").GetString());
        Assert.False(dependencies.TryGetProperty("com.unity.render-pipelines.universal", out _));
        Assert.False(dependencies.TryGetProperty("com.unity.test-framework", out _));

        Assert.Equal(2, manifest.GetProperty("samples").GetArrayLength());
        var quickStart = manifest.GetProperty("samples").EnumerateArray().Single(item => item.GetProperty("path").GetString() == "Samples~/QuickStart");
        Assert.Equal("Quick Start", quickStart.GetProperty("displayName").GetString());
        Assert.Contains("Windows", manifest.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("2022.3.62f3", manifest.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.NotEmpty(manifest.GetProperty("keywords").EnumerateArray());
        Assert.Equal("https://github.com/Houtamelo/spire_csharp_analyzers/blob/main/docs/unity-package/Documentation~/GettingStarted.md", manifest.GetProperty("documentationUrl").GetString());
        Assert.Equal("https://github.com/Houtamelo/spire_csharp_analyzers/blob/main/LICENSE", manifest.GetProperty("licensesUrl").GetString());
        Assert.Equal("https://github.com/Houtamelo/spire_csharp_analyzers/issues", manifest.GetProperty("author").GetProperty("url").GetString());
        var sample = manifest.GetProperty("samples").EnumerateArray().Single(item => item.GetProperty("path").GetString() == "Samples~/Showcase");
        Assert.Equal("Unity Showcase (URP)", sample.GetProperty("displayName").GetString());
        Assert.Equal("Samples~/Showcase", sample.GetProperty("path").GetString());
        var sampleDescription = sample.GetProperty("description").GetString()!;
        Assert.Contains("6000.5.6f1", sampleDescription, StringComparison.Ordinal);
        Assert.Contains("17.5.0", sampleDescription, StringComparison.Ordinal);
        Assert.Contains("1.7.0", sampleDescription, StringComparison.Ordinal);
        Assert.Contains("URP", sampleDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("test framework", sampleDescription, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file:", document.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertPackageReadme(string outputPath)
    {
        var readme = string.Join(" ", File.ReadAllText(Path.Combine(outputPath, "README.md"))
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        Assert.Contains("Rider and Visual Studio Community", readme, StringComparison.Ordinal);
        Assert.Contains("Linux and Windows x64", readme, StringComparison.Ordinal);
        Assert.Contains("representative IDE Quick Action", readme, StringComparison.Ordinal);
        Assert.Contains("Unity 6000.6.5f1", readme, StringComparison.Ordinal);
        Assert.Contains("generic unions with reference-type arguments", readme, StringComparison.Ordinal);
        Assert.Contains("Unity 2020 will not work", readme, StringComparison.Ordinal);
        Assert.Contains("Minimum Unity version: 2022.3.62f3", readme, StringComparison.Ordinal);
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
            $"      Any:\n    second:\n      enabled: {enabled}\n",
            metadata,
            StringComparison.Ordinal);
        Assert.Contains(
            $"      Editor: Editor\n    second:\n      enabled: {enabled}\n",
            metadata,
            StringComparison.Ordinal);
    }

    private static void AssertDeterministicMetaGuids(string repositoryRoot, string outputPath)
    {
        foreach (var metaPath in Directory.EnumerateFiles(outputPath, "*.meta", SearchOption.AllDirectories))
        {
            var relativeMeta = RelativePath(outputPath, metaPath);
            if (UnityCustomerDeliveryTestSupport.IsAuthoredShowcaseMeta(
                    relativeMeta,
                    repositoryRoot))
            {
                continue;
            }

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
        CreateVersionProject(root, "Houtamelo.Spire.Functional", "1.0.0");
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
                if (File.Exists(Path.Combine(directory.FullName, "tools", "Spire.Tool", "Spire.Tool.csproj")) &&
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

}
