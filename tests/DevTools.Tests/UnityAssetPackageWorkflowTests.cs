using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class UnityAssetPackageWorkflowTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);

    private static readonly string[] ExpectedAssemblies =
    [
        "Runtime/Houtamelo.Spire.dll",
        "Runtime/Houtamelo.Spire.Functional.dll",
        "Analyzers/Houtamelo.Spire.Analyzers.dll",
        "Analyzers/Houtamelo.Spire.PatternAnalysis.dll",
        "Analyzers/Houtamelo.Spire.CodeFixes.dll",
    ];

    [Fact]
    public async Task PrepareOnly_StagesLegacyAssetPackageSurfaceWithExactRuntimeAndAnalyzerMetadata()
    {
        var repositoryRoot = FindRepositoryRoot();
        AssertBuildArtifactsPresent(repositoryRoot);
        using var fixture = AssetPackageFixture.Create(repositoryRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.OutputPath)!);
        File.WriteAllText(fixture.OutputPath, "preserve this archive\n");

        var result = await RunUnityPackageAsync(
            repositoryRoot,
            fixture.ProjectPath,
            fixture.OutputPath,
            prepareOnly: true);

        AssertSuccess(result);
        Assert.Equal("preserve this archive\n", File.ReadAllText(fixture.OutputPath));
        AssertStageMarker(fixture.ProjectPath);
        AssertStageSurface(fixture.ProjectPath);
        AssertPackageManifest(fixture.ProjectPath);
        AssertProjectVersion(fixture.ProjectPath, repositoryRoot);
        AssertUnitySdkSelection(fixture.ProjectPath);
        AssertRuntimeAndAnalyzerMetadata(fixture.ProjectPath);
        AssertDllBytesMatchUpmInput(fixture.ProjectPath);
        AssertReadme(fixture.ProjectPath);
    }

    [Fact]
    public async Task PrepareOnly_RerunningOwnedProjectResetsStageAndKeepsDeterministicMetadata()
    {
        var repositoryRoot = FindRepositoryRoot();
        AssertBuildArtifactsPresent(repositoryRoot);
        using var fixture = AssetPackageFixture.Create(repositoryRoot);

        var first = await RunUnityPackageAsync(
            repositoryRoot,
            fixture.ProjectPath,
            fixture.OutputPath,
            prepareOnly: true);
        AssertSuccess(first);
        var before = CaptureFileDigests(Path.Combine(fixture.ProjectPath, "Assets", "Spire"));
        var beforeRootMetadata = Hash(Path.Combine(fixture.ProjectPath, "Assets", "Spire.meta"));

        File.WriteAllText(Path.Combine(fixture.ProjectPath, "Assets", "stale.asset"), "stale\n");
        File.WriteAllText(Path.Combine(fixture.ProjectPath, "Packages", "stale.json"), "stale\n");
        File.WriteAllText(Path.Combine(fixture.ProjectPath, "ProjectSettings", "stale.asset"), "stale\n");

        var second = await RunUnityPackageAsync(
            repositoryRoot,
            fixture.ProjectPath,
            fixture.OutputPath,
            prepareOnly: true);

        AssertSuccess(second);
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, "Assets", "stale.asset")));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, "Packages", "stale.json")));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, "ProjectSettings", "stale.asset")));
        Assert.Equal(before, CaptureFileDigests(Path.Combine(fixture.ProjectPath, "Assets", "Spire")));
        Assert.Equal(beforeRootMetadata, Hash(Path.Combine(fixture.ProjectPath, "Assets", "Spire.meta")));
        AssertStageSurface(fixture.ProjectPath);
    }

    [Fact]
    public async Task PrepareOnly_RejectsProjectInsideExamplesBeforeMutation()
    {
        using var fixture = AssetPackageFixture.CreateIsolatedRoot();
        var project = Path.Combine(fixture.Root, "examples", "Unity Project");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "sentinel.txt"), "preserve project\n");
        var before = Snapshot(project);

        var result = await RunUnityPackageAsync(
            fixture.RepositoryRoot,
            project,
            Path.Combine(fixture.Root, "export.unitypackage"),
            prepareOnly: true,
            rootOverride: fixture.Root);

        AssertFailure(result);
        Assert.Contains("repository examples directory", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(project));
        Assert.False(File.Exists(Path.Combine(project, ".spire-unitypackage-export")));
    }

    [Fact]
    public async Task PrepareOnly_RejectsArchiveInsideExamplesBeforeMutation()
    {
        using var fixture = AssetPackageFixture.CreateIsolatedRoot();
        var project = Path.Combine(fixture.Root, "project");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "sentinel.txt"), "preserve project\n");
        var before = Snapshot(project);

        var result = await RunUnityPackageAsync(
            fixture.RepositoryRoot,
            project,
            Path.Combine(fixture.Root, "examples", "export.unitypackage"),
            prepareOnly: true,
            rootOverride: fixture.Root);

        AssertFailure(result);
        Assert.Contains("repository examples directory", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(project));
    }

    [Fact]
    public async Task PrepareOnly_RejectsWrongOutputExtensionBeforeMutation()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AssetPackageFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.ProjectPath);
        File.WriteAllText(Path.Combine(fixture.ProjectPath, "sentinel.txt"), "preserve project\n");
        var before = Snapshot(fixture.ProjectPath);

        var result = await RunUnityPackageAsync(
            repositoryRoot,
            fixture.ProjectPath,
            Path.Combine(fixture.Root, "export.zip"),
            prepareOnly: true);

        AssertFailure(result);
        Assert.Contains(".unitypackage", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(fixture.ProjectPath));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, ".spire-unitypackage-export")));
    }

    [Fact]
    public async Task PrepareOnly_RejectsOutputInsideProjectBeforeMutation()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AssetPackageFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.ProjectPath);
        File.WriteAllText(Path.Combine(fixture.ProjectPath, "sentinel.txt"), "preserve project\n");
        var before = Snapshot(fixture.ProjectPath);

        var result = await RunUnityPackageAsync(
            repositoryRoot,
            fixture.ProjectPath,
            Path.Combine(fixture.ProjectPath, "export.unitypackage"),
            prepareOnly: true);

        AssertFailure(result);
        Assert.Contains("outside", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(fixture.ProjectPath));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, ".spire-unitypackage-export")));
    }

    [Fact]
    public async Task PrepareOnly_RejectsUnrelatedExistingProjectBeforeMutation()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AssetPackageFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.ProjectPath);
        File.WriteAllText(Path.Combine(fixture.ProjectPath, "unrelated-sentinel.txt"), "preserve project\n");
        var before = Snapshot(fixture.ProjectPath);

        var result = await RunUnityPackageAsync(
            repositoryRoot,
            fixture.ProjectPath,
            fixture.OutputPath,
            prepareOnly: true);

        AssertFailure(result);
        Assert.Contains("unrelated", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(fixture.ProjectPath));
        Assert.False(File.Exists(fixture.OutputPath));
    }

    [Fact]
    public async Task PrepareOnly_RejectsInvalidOwnershipMarkerBeforeMutation()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AssetPackageFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.ProjectPath);
        var marker = Path.Combine(fixture.ProjectPath, ".spire-unitypackage-export");
        File.WriteAllText(marker, "not an owned Spire export project\n");
        var before = Snapshot(fixture.ProjectPath);

        var result = await RunUnityPackageAsync(
            repositoryRoot,
            fixture.ProjectPath,
            fixture.OutputPath,
            prepareOnly: true);

        AssertFailure(result);
        Assert.Contains("ownership marker", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(fixture.ProjectPath));
        Assert.False(File.Exists(fixture.OutputPath));
    }

    [Fact]
    public async Task NonPrepare_MissingEditorDoesNotModifyExistingArchiveOrProject()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = AssetPackageFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.ProjectPath);
        File.WriteAllText(Path.Combine(fixture.ProjectPath, "sentinel.txt"), "preserve project\n");
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.OutputPath)!);
        File.WriteAllText(fixture.OutputPath, "preserve archive\n");
        var projectBefore = Snapshot(fixture.ProjectPath);

        var result = await RunUnityPackageAsync(
            repositoryRoot,
            fixture.ProjectPath,
            fixture.OutputPath,
            prepareOnly: false,
            editor: Path.Combine(fixture.Root, "missing", "Unity"));

        AssertFailure(result);
        Assert.Contains("Required file is missing", CombinedOutput(result), StringComparison.Ordinal);
        Assert.Equal("preserve archive\n", File.ReadAllText(fixture.OutputPath));
        Assert.Equal(projectBefore, Snapshot(fixture.ProjectPath));
    }

    [Fact]
    public async Task PrepareOnly_VersionMismatchFailsBeforeMutatingProjectOrCreatingArtifact()
    {
        using var fixture = AssetPackageFixture.CreateIsolatedRoot();
        CreateVersionFixture(fixture.Root);
        var project = Path.Combine(fixture.Root, "project");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "sentinel.txt"), "preserve project\n");
        var before = Snapshot(project);
        var output = Path.Combine(fixture.Root, "export.unitypackage");

        var result = await RunUnityPackageAsync(
            fixture.RepositoryRoot,
            project,
            output,
            prepareOnly: true,
            rootOverride: fixture.Root);

        AssertFailure(result);
        Assert.Contains("versions must match", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(project));
        Assert.False(File.Exists(output));
    }

    private static async Task<ProcessResult> RunUnityPackageAsync(
        string repositoryRoot,
        string projectPath,
        string outputPath,
        bool prepareOnly,
        string? editor = null,
        string? rootOverride = null)
    {
        var arguments = new List<string>();
        if (rootOverride is not null)
        {
            arguments.Add("--root");
            arguments.Add(rootOverride);
        }

        arguments.Add("unitypackage");
        arguments.Add("--project");
        arguments.Add(projectPath);
        arguments.Add("--output");
        arguments.Add(outputPath);
        arguments.Add("--configuration");
        arguments.Add(CurrentConfiguration);
        arguments.Add("--no-build");
        if (prepareOnly)
            arguments.Add("--prepare-only");
        if (editor is not null)
        {
            arguments.Add("--editor");
            arguments.Add(editor);
        }

        var launcherConfiguration = CurrentConfiguration;
        var cliPath = Path.Combine(
            repositoryRoot,
            "tools",
            "Spire.Tool",
            "bin",
            launcherConfiguration,
            "net10.0",
            "Spire.Tool.dll");
        Assert.True(File.Exists(cliPath), $"Missing {launcherConfiguration} Spire.Tool CLI: {cliPath}");

        return await ProcessRunner.RunAsync(
            new ToolContext(repositoryRoot).Dotnet,
            new[] { cliPath }.Concat(arguments),
            repositoryRoot,
            ProcessTimeout,
            new Dictionary<string, string?>
            {
                ["CI"] = "true",
                ["GIT_TERMINAL_PROMPT"] = "0",
                ["MSBUILDDISABLENODEREUSE"] = "1",
            });
    }

    private static void AssertStageMarker(string projectPath)
        => Assert.Equal(
            "Spire unitypackage export\n",
            File.ReadAllText(Path.Combine(projectPath, ".spire-unitypackage-export")));

    private static void AssertStageSurface(string projectPath)
    {
        var package = Path.Combine(projectPath, "upm");
        var assets = Path.Combine(projectPath, "Assets", "Spire");
        Assert.True(File.Exists(Path.Combine(projectPath, "Assets", "Spire.meta")));
        Assert.True(File.Exists(Path.Combine(package, "package.json")));
        Assert.True(File.Exists(Path.Combine(package, "Samples~", "QuickStart", "QuickStart.cs")));
        Assert.True(File.Exists(Path.Combine(package, "Samples~", "Showcase", "README.md")));

        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "README.txt",
            "README.txt.meta",
            "LICENSE.md",
            "LICENSE.md.meta",
            "Third Party Notices.md",
            "Third Party Notices.md.meta",
            "CHANGELOG.md",
            "CHANGELOG.md.meta",
            "Runtime.meta",
            "Analyzers.meta",
            "Documentation.meta",
        };

        foreach (var folder in new[] { "Runtime", "Analyzers" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(package, folder), "*", SearchOption.AllDirectories))
                expected.Add(folder + "/" + RelativePath(Path.Combine(package, folder), file));
            expected.Add(folder + ".meta");
        }

        var documentationSource = Path.Combine(package, "Documentation~");
        foreach (var directory in Directory.EnumerateDirectories(documentationSource, "*", SearchOption.AllDirectories))
        {
            var relative = RelativePath(documentationSource, directory);
            expected.Add("Documentation/" + relative + ".meta");
        }

        foreach (var file in Directory.EnumerateFiles(documentationSource, "*", SearchOption.AllDirectories))
        {
            var relative = RelativePath(documentationSource, file);
            expected.Add("Documentation/" + relative);
            if (!relative.EndsWith(".meta", StringComparison.Ordinal))
                expected.Add("Documentation/" + relative + ".meta");
        }

        var actual = Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories)
            .Select(path => RelativePath(assets, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(path => path, StringComparer.Ordinal), actual);
        foreach (var metadata in actual.Where(path => path.EndsWith(".meta", StringComparison.Ordinal)))
        {
            var source = Path.Combine(assets, metadata[..^".meta".Length].Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(source) || Directory.Exists(source), $"Orphaned metadata: {metadata}");
        }
        Assert.DoesNotContain(actual, path => path.Contains("Samples", StringComparison.Ordinal));
        Assert.DoesNotContain(actual, path => path.EndsWith("package.json", StringComparison.Ordinal));
    }

    private static void AssertPackageManifest(string projectPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectPath, "Packages", "manifest.json")));
        var dependencies = document.RootElement.GetProperty("dependencies");
        Assert.Equal(
            new[] { "com.unity.modules.imgui", "com.unity.modules.jsonserialize", "com.unity.modules.uielements" },
            dependencies.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal("1.0.0", dependencies.GetProperty("com.unity.modules.imgui").GetString());
        Assert.Equal("1.0.0", dependencies.GetProperty("com.unity.modules.jsonserialize").GetString());
        Assert.Equal("1.0.0", dependencies.GetProperty("com.unity.modules.uielements").GetString());
        Assert.DoesNotContain("individual.antonio-pedro-goncalves-ferreira.spire", document.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    private static void AssertProjectVersion(string projectPath, string repositoryRoot)
        => Assert.Equal(
            File.ReadAllText(Path.Combine(repositoryRoot, "tests", "UnityTarballAcceptance", "ProjectSettings", "ProjectVersion.txt")),
            File.ReadAllText(Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt")));

    private static void AssertUnitySdkSelection(string projectPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectPath, "global.json")));
        Assert.Equal("8.0.318", document.RootElement.GetProperty("sdk").GetProperty("version").GetString());
        Assert.Equal("latestPatch", document.RootElement.GetProperty("sdk").GetProperty("rollForward").GetString());
    }

    private static void AssertRuntimeAndAnalyzerMetadata(string projectPath)
    {
        var assets = Path.Combine(projectPath, "Assets", "Spire");
        AssertPluginMetadata(Path.Combine(assets, "Runtime", "Houtamelo.Spire.dll.meta"), analyzer: false);
        AssertPluginMetadata(Path.Combine(assets, "Runtime", "Houtamelo.Spire.Functional.dll.meta"), analyzer: false);
        foreach (var name in new[] { "Houtamelo.Spire.Analyzers", "Houtamelo.Spire.PatternAnalysis", "Houtamelo.Spire.CodeFixes" })
            AssertPluginMetadata(Path.Combine(assets, "Analyzers", name + ".dll.meta"), analyzer: true);
    }

    private static void AssertPluginMetadata(string path, bool analyzer)
    {
        var metadata = File.ReadAllText(path);
        if (analyzer)
            Assert.Contains("labels:\n- RoslynAnalyzer\n", metadata, StringComparison.Ordinal);
        else
            Assert.DoesNotContain("RoslynAnalyzer", metadata, StringComparison.Ordinal);

        var enabled = analyzer ? 0 : 1;
        Assert.Contains($"      Any:\n    second:\n      enabled: {enabled}\n", metadata, StringComparison.Ordinal);
        Assert.Contains($"      Editor: Editor\n    second:\n      enabled: {enabled}\n", metadata, StringComparison.Ordinal);
    }

    private static void AssertDllBytesMatchUpmInput(string projectPath)
    {
        foreach (var relative in ExpectedAssemblies)
        {
            var source = Path.Combine(projectPath, "upm", relative.Replace('/', Path.DirectorySeparatorChar));
            var staged = Path.Combine(projectPath, "Assets", "Spire", relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(Hash(source), Hash(staged));
        }

        foreach (var name in new[] { "Houtamelo.Spire", "Houtamelo.Spire.Functional" })
        {
            var source = Path.Combine(projectPath, "upm", "Runtime", name + ".xml");
            var staged = Path.Combine(projectPath, "Assets", "Spire", "Runtime", name + ".xml");
            Assert.True(File.Exists(source), $"Missing UPM XML input: {source}");
            Assert.True(File.Exists(staged), $"Missing staged XML: {staged}");
            Assert.Equal(Hash(source), Hash(staged));
        }
    }

    private static void AssertReadme(string projectPath)
    {
        var readme = File.ReadAllText(Path.Combine(projectPath, "Assets", "Spire", "README.txt"));
        Assert.Contains("Assets > Import Package > Custom Package", readme, StringComparison.Ordinal);
        Assert.Contains("archive can be deleted afterward", readme, StringComparison.Ordinal);
        Assert.Contains("Assets/Spire", readme, StringComparison.Ordinal);
        Assert.Contains("Documentation", readme, StringComparison.Ordinal);
        Assert.Contains("2022.3.62f3", readme, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> CaptureFileDigests(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => RelativePath(root, path),
                Hash,
                StringComparer.Ordinal);

    private static void AssertBuildArtifactsPresent(string repositoryRoot)
    {
        var configuration = CurrentConfiguration;
        foreach (var relative in ExpectedAssemblies)
        {
            var name = Path.GetFileName(relative);
            var project = name switch
            {
                "Houtamelo.Spire.dll" => "Houtamelo.Spire",
                "Houtamelo.Spire.Functional.dll" => "Houtamelo.Spire.Functional",
                "Houtamelo.Spire.Analyzers.dll" => "Houtamelo.Spire.Analyzers",
                "Houtamelo.Spire.PatternAnalysis.dll" => "Houtamelo.Spire.PatternAnalysis",
                _ => "Houtamelo.Spire.CodeFixes",
            };
            var path = Path.Combine(repositoryRoot, "src", project, "bin", configuration, "netstandard2.0", name);
            Assert.True(File.Exists(path), $"Missing {configuration} artifact: {path}");
        }

        Assert.True(
            File.Exists(Path.Combine(
                repositoryRoot,
                "src",
                "Houtamelo.Spire",
                "bin",
                configuration,
                "netstandard2.0",
                "Houtamelo.Spire.xml")),
            "Missing runtime XML artifact.");
    }

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
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>netstandard2.0</TargetFramework><Version>{version}</Version></PropertyGroup></Project>");
    }

    private static string CurrentConfiguration
        => Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_CONFIGURATION") ??
            (AppContext.BaseDirectory.Contains(
                Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal)
                ? "Release"
                : "Debug");

    private static string[] Snapshot(string root)
        => (Directory.Exists(root) ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories) : Array.Empty<string>())
            .Select(path => RelativePath(root, path) + "=" + Hash(path))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string Hash(string path)
        => DevTools.ProcessCompatibility.ToHexString(
                DevTools.ProcessCompatibility.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();

    private static void AssertSuccess(ProcessResult result)
        => Assert.True(result.ExitCode == 0, CombinedOutput(result));

    private static void AssertFailure(ProcessResult result)
        => Assert.NotEqual(0, result.ExitCode);

    private static string CombinedOutput(ProcessResult result)
        => result.StandardOutput + Environment.NewLine + result.StandardError;

    private static string RelativePath(string root, string path)
        => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "Spire.Tool", "Spire.Tool.csproj")) &&
                    File.Exists(Path.Combine(directory.FullName, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root for Unity asset package tests.");
    }

    private sealed class AssetPackageFixture : IDisposable
    {
        private AssetPackageFixture(string repositoryRoot, string root)
        {
            RepositoryRoot = repositoryRoot;
            Root = root;
            ProjectPath = Path.Combine(root, "Unity Project With Spaces");
            OutputPath = Path.Combine(root, "output with spaces", "Spire Asset Package.unitypackage");
            Directory.CreateDirectory(root);
        }

        public string RepositoryRoot { get; }
        public string Root { get; }
        public string ProjectPath { get; }
        public string OutputPath { get; }

        public static AssetPackageFixture Create(string repositoryRoot)
            => new(
                repositoryRoot,
                Path.Combine(repositoryRoot, "tmp", "devtools-unitypackage-tests", "space root " + Guid.NewGuid().ToString("N")));

        public static AssetPackageFixture CreateIsolatedRoot()
        {
            var repositoryRoot = FindRepositoryRoot();
            var root = Path.Combine(
                repositoryRoot,
                "tmp",
                "devtools-unitypackage-isolation-tests",
                "space root " + Guid.NewGuid().ToString("N"));
            return new AssetPackageFixture(repositoryRoot, root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
