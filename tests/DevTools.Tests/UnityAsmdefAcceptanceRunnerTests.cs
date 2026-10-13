using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Linq;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class UnityAsmdefAcceptanceRunnerTests
{
    [Fact]
    public async Task PrepareProject_CopiesTheFixtureAndPackageProvenance()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        using var fixture = AcceptanceFixture.Create(repositoryRoot);

        var prepared = await PrepareProjectAsync(repositoryRoot, fixture.ProjectPath, configuration);

        Assert.Equal(Path.GetFullPath(fixture.ProjectPath), Path.GetFullPath(prepared));
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

        await PrepareProjectAsync(repositoryRoot, fixture.ProjectPath, configuration);

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

        await PrepareProjectAsync(repositoryRoot, fixture.ProjectPath, configuration);

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

        var staleState = Path.Combine(fixture.ProjectPath, "Library", "Spire.UnityAsmdefIntegration-state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(staleState)!);
        File.WriteAllText(staleState, "stale state", Encoding.UTF8);

        await PrepareProjectAsync(repositoryRoot, fixture.ProjectPath, configuration);

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

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            PrepareProjectAsync(repositoryRoot, fixture.ProjectPath, configuration));

        Assert.Contains("ownership marker is invalid", error.Message, StringComparison.OrdinalIgnoreCase);
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

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            PrepareProjectAsync(repositoryRoot, fixture.ProjectPath, configuration));

        Assert.Contains("unrelated files", error.Message, StringComparison.OrdinalIgnoreCase);
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

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            PrepareProjectAsync(repositoryRoot, trackedFixture, configuration));

        Assert.Contains("fixture", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(trackedFixture, ".spire-unity-asmdefs")));
    }

    [Fact]
    public void AnalyzerReferences_ParsesARealBeeResponseAndDropsAResponseWithoutAnalyzers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceProject = Path.Combine(repositoryRoot, "tmp", "unity-asmdefs");
        var sourceBee = Path.Combine(sourceProject, "Library", "Bee");
        Assert.True(Directory.Exists(sourceBee), sourceBee);

        var sourceResponse = Directory
            .EnumerateFiles(sourceBee, "*.rsp", SearchOption.AllDirectories)
            .Select(path => new { Path = path, Lines = File.ReadAllLines(path) })
            .FirstOrDefault(value => value.Lines.Any(IsOutputArgument) && value.Lines.Any(IsAnalyzerArgument));
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
        var copiedResponse = Path.Combine(fixture.ProjectPath, "Library", "Bee", "copied-real-response.rsp");
        Directory.CreateDirectory(Path.GetDirectoryName(copiedResponse)!);
        File.WriteAllLines(copiedResponse, sourceResponse.Lines, Encoding.UTF8);

        var parsed = AsmdefWorkflow.AnalyzerReferences(fixture.ProjectPath);
        var assembly = GetObject(parsed, assemblyName);
        Assert.Equal("Library/Bee/copied-real-response.rsp", NormalizePath(GetString(assembly, "responseFile")));
        Assert.Equal(expectedAnalyzers, GetArray(assembly, "analyzers"));

        File.WriteAllLines(copiedResponse, sourceResponse.Lines.Where(line => !IsAnalyzerArgument(line)), Encoding.UTF8);
        var withoutAnalyzers = AsmdefWorkflow.AnalyzerReferences(fixture.ProjectPath);
        Assert.False(withoutAnalyzers.ContainsKey(assemblyName));
    }

    [Fact]
    public void GeneratedProjectReferences_ReadsActualGeneratedUnityProjects()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceProject = Path.Combine(repositoryRoot, "tmp", "unity-asmdefs");
        var sourceProjects = Directory.EnumerateFiles(sourceProject, "*.csproj", SearchOption.TopDirectoryOnly).ToArray();
        Assert.NotEmpty(sourceProjects);

        using var fixture = AcceptanceFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.ProjectPath);
        foreach (var source in sourceProjects)
            File.Copy(source, Path.Combine(fixture.ProjectPath, Path.GetFileName(source)));

        var expected = sourceProjects.ToDictionary(
            path => Path.GetFileNameWithoutExtension(path),
            path => XDocument.Load(path).Descendants()
                .Where(node => node.Name.LocalName == "Analyzer")
                .Select(node => node.Attribute("Include")?.Value)
                .Where(value => value is not null)
                .Cast<string>()
                .ToArray(),
            StringComparer.Ordinal);
        Assert.Contains(expected.Values, values => values.Length > 0);

        var actual = AsmdefWorkflow.GeneratedProjectReferences(fixture.ProjectPath);
        Assert.Equal(
            expected.Keys.OrderBy(value => value, StringComparer.Ordinal),
            actual.Select(pair => pair.Key).OrderBy(value => value, StringComparer.Ordinal));
        foreach (var pair in expected)
        {
            var project = GetObject(actual, pair.Key);
            Assert.Equal(Path.Combine(fixture.ProjectPath, pair.Key + ".csproj"), GetString(project, "project"));
            Assert.Equal(pair.Value, GetArray(project, "analyzers"));
        }
    }

    private static async Task<string> PrepareProjectAsync(string repositoryRoot, string projectPath, string configuration)
        => await AsmdefWorkflow.PrepareAsync(new ToolContext(repositoryRoot), projectPath, configuration, build: false);

    private static JsonObject GetObject(JsonObject parent, string property)
        => parent[property]?.AsObject() ?? throw new Xunit.Sdk.XunitException($"JSON object property '{property}' was missing.");

    private static string GetString(JsonObject parent, string property)
        => parent[property]?.GetValue<string>() ?? throw new Xunit.Sdk.XunitException($"JSON string property '{property}' was missing.");

    private static string[] GetArray(JsonObject parent, string property)
        => (parent[property]?.AsArray() ?? throw new Xunit.Sdk.XunitException($"JSON array property '{property}' was missing.")).Select(value => value!.GetValue<string>()).ToArray();

    private static void AssertFixtureSourcesCopied(string repositoryRoot, string projectPath)
    {
        foreach (var directoryName in new[] { "Assets", "Packages", "ProjectSettings" })
        {
            var sourceRoot = Path.Combine(repositoryRoot, "tests", "UnityAsmdefIntegration", directoryName);
            var targetRoot = Path.Combine(projectPath, directoryName);
            Assert.True(Directory.Exists(targetRoot), targetRoot);

            var expected = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                .Select(path => RelativePath(sourceRoot, path)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var actual = Directory.EnumerateFiles(targetRoot, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(Path.GetFileName(path), "csc.rsp", StringComparison.Ordinal))
                .Where(path => directoryName != "Packages" || !RelativePath(targetRoot, path).StartsWith("com.houtamelo.spire/", StringComparison.Ordinal))
                .Select(path => RelativePath(targetRoot, path)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
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
        var compiledAssetsRoot = Path.Combine(projectPath, "Assets");
        var rspDirectories = new[] { compiledAssetsRoot }
            .Concat(Directory.EnumerateFiles(compiledAssetsRoot, "*.asmdef", SearchOption.AllDirectories)
                .Select(Path.GetDirectoryName).Where(path => path is not null).Cast<string>())
            .Distinct(StringComparer.Ordinal);
        foreach (var directory in rspDirectories)
        {
            var rsp = Path.Combine(directory, "csc.rsp");
            Assert.True(File.Exists(rsp), rsp);
            Assert.Contains("-langversion:9", File.ReadAllText(rsp));
            Assert.Contains("-ruleset:\"" + ruleset + "\"", File.ReadAllText(rsp));
        }

        var sampleRoot = Path.Combine(projectPath, "Packages", "com.houtamelo.spire", "Samples~", "Showcase");
        Assert.True(Directory.Exists(sampleRoot), sampleRoot);
        var sampleAsmdefs = Directory.EnumerateFiles(sampleRoot, "*.asmdef", SearchOption.AllDirectories).ToArray();
        Assert.NotEmpty(sampleAsmdefs);
        Assert.All(sampleAsmdefs, asmdef =>
        {
            var rsp = Path.Combine(Path.GetDirectoryName(asmdef)!, "csc.rsp");
            Assert.False(File.Exists(rsp), "Hidden sample asmdef was treated as a compiled project input: " + asmdef);
        });
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
            Assert.Equal(Sha256(source), row.GetProperty("sha256").GetString());
            var copied = Path.Combine(projectPath, FixtureTargetRelativePath(relativeSource).Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(copied), copied);
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(copied));
        }

        Assert.Equal(expected.OrderBy(value => value, StringComparer.Ordinal), actual.OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string FixtureTargetRelativePath(string relativeSource)
    {
        var parts = NormalizePath(relativeSource).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var index = Array.FindIndex(parts, part => string.Equals(part, "Assets", StringComparison.Ordinal) ||
            string.Equals(part, "Packages", StringComparison.Ordinal) || string.Equals(part, "ProjectSettings", StringComparison.Ordinal));
        Assert.True(index >= 0, "Fixture provenance path has no project directory: " + relativeSource);
        return string.Join("/", parts.Skip(index));
    }

    private static void AssertPackageProvenance(string repositoryRoot, string projectPath, string configuration)
    {
        var packageRoot = Path.Combine(projectPath, "Packages", "com.houtamelo.spire");
        Assert.True(Directory.Exists(packageRoot), packageRoot);
        var packageJson = Path.Combine(packageRoot, "package.json");
        using var packageDocument = JsonDocument.Parse(File.ReadAllText(packageJson));
        var expectedVersion = XDocument.Load(Path.Combine(repositoryRoot, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj"))
            .Descendants("Version").Select(node => node.Value).Single();
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
            var source = Path.Combine(repositoryRoot, "src", assembly.Project, "bin", configuration, "netstandard2.0", assembly.Project + ".dll");
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
        => line.TrimStart().StartsWith("/out:", StringComparison.Ordinal) || line.TrimStart().StartsWith("-out:", StringComparison.Ordinal);

    private static bool IsAnalyzerArgument(string line)
        => line.TrimStart().StartsWith("/analyzer:", StringComparison.Ordinal) || line.TrimStart().StartsWith("-analyzer:", StringComparison.Ordinal);

    private static string Unquote(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static string Sha256(string path)
        => DevTools.ProcessCompatibility.ToHexString(DevTools.ProcessCompatibility.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string RelativePath(string root, string path) => NormalizePath(Path.GetRelativePath(root, path));

    private static string BuildConfiguration()
#if SPIRE_UNITY_TEST_HOST
        => Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_CONFIGURATION") ?? "Release";
#else
        => Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_CONFIGURATION") ??
            (AppContext.BaseDirectory.Contains(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? "Release" : "Debug");
#endif

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "Spire.Tool.Core", "Spire.Tool.Core.csproj")) &&
                    File.Exists(Path.Combine(directory.FullName, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root containing the Spire.Tool implementation.");
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
            => new(Path.Combine(repositoryRoot, "tmp", "devtools-unity-asmdef-acceptance-tests", Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
