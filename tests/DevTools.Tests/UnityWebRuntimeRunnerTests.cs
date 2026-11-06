using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class UnityWebRuntimeRunnerTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task PrepareProject_CopiesOriginalFactsAndWebFixtureByteForByte()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        AssertBuildArtifactsPresent(repositoryRoot, configuration);

        using var fixture = PreparationFixture.Create(repositoryRoot);
        var editor = fixture.CreateIsolatedEditor();
        var result = await RunPrepareProjectAsync(repositoryRoot, fixture.ProjectPath, configuration, editor);

        Assert.True(result.ExitCode == 0, CombinedOutput(result));
        var sourceRoot = Path.Combine(repositoryRoot, "tests", "Houtamelo.Spire.BehavioralTests");
        var copiedRoot = Path.Combine(fixture.ProjectPath, "Assets", "WebBehavioral", "Existing");
        Assert.Equal(CaptureAuthoredBehavioralDigests(sourceRoot), CaptureFileDigests(copiedRoot));

        using var inputs = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.ProjectPath, "web-runtime-inputs.json")));
        var expectedNames = inputs.RootElement.GetProperty("expectedFactNames")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Equal(100, expectedNames.Length);
        Assert.Equal(expectedNames, ExpectedFactNames(Path.Combine(sourceRoot, "Tests")));
        Assert.Equal("WebGL", inputs.RootElement.GetProperty("platform").GetString());
        Assert.Equal("IL2CPP", inputs.RootElement.GetProperty("backend").GetString());
        Assert.Equal(
            "m_EditorVersion: 6000.5.6f1\n",
            File.ReadAllText(Path.Combine(fixture.ProjectPath, "ProjectSettings", "ProjectVersion.txt")));
        var originalInit = Path.Combine(repositoryRoot, "tests", "UnityBehavioral", "Assets", "Tests", "IsExternalInit.cs");
        var copiedInit = Path.Combine(fixture.ProjectPath, "Assets", "WebBehavioral", "IsExternalInit.cs");
        Assert.True(File.Exists(originalInit), originalInit);
        Assert.True(File.Exists(copiedInit), copiedInit);
        Assert.Equal(File.ReadAllBytes(originalInit), File.ReadAllBytes(copiedInit));

        var coreSource = Path.Combine(repositoryRoot, "tests", "UnityVariantPayloadCore", "Assets");
        var copiedCore = Path.Combine(fixture.ProjectPath, "Assets");
        foreach (var source in Directory.EnumerateFiles(coreSource, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(coreSource, source);
            var target = Path.Combine(copiedCore, relative);
            Assert.True(File.Exists(target), target);
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(target));
        }

        var webFixture = Path.Combine(repositoryRoot, "tests", "UnityWebRuntime", "Assets");
        foreach (var source in Directory.EnumerateFiles(webFixture, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(webFixture, source);
            var target = Path.Combine(copiedCore, relative);
            Assert.True(File.Exists(target), target);
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(target));
        }
    }

    [Fact]
    public async Task PrepareProject_UsesWebPlayerAssemblyAndScopedLinkerPreservation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        AssertBuildArtifactsPresent(repositoryRoot, configuration);

        using var fixture = PreparationFixture.Create(repositoryRoot);
        var editor = fixture.CreateIsolatedEditor();
        var result = await RunPrepareProjectAsync(repositoryRoot, fixture.ProjectPath, configuration, editor);
        Assert.True(result.ExitCode == 0, CombinedOutput(result));

        var runtimeRoot = Path.Combine(fixture.ProjectPath, "Assets", "WebBehavioral");
        var asmdefPath = Path.Combine(runtimeRoot, "Spire.UnityWebBehavioral.asmdef");
        Assert.True(File.Exists(asmdefPath), asmdefPath);
        using var asmdef = JsonDocument.Parse(File.ReadAllText(asmdefPath));
        var definition = asmdef.RootElement;
        Assert.Equal("Spire.UnityWebBehavioral", definition.GetProperty("name").GetString());
        Assert.True(definition.GetProperty("overrideReferences").GetBoolean());
        Assert.Empty(definition.GetProperty("includePlatforms").EnumerateArray());
        Assert.DoesNotContain("Editor", definition.GetProperty("includePlatforms").EnumerateArray()
            .Select(value => value.GetString()));
        Assert.DoesNotContain("optionalUnityReferences", definition.EnumerateObject().Select(property => property.Name));

        var references = definition.GetProperty("precompiledReferences")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Contains("Houtamelo.Spire.dll", references);
        Assert.Contains("xunit.assert.dll", references);
        Assert.Contains("xunit.core.dll", references);
        Assert.DoesNotContain(references, name => name.Contains("CodeAnalysis", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.Contains("Workspaces", StringComparison.OrdinalIgnoreCase));

        var linkPath = Path.Combine(runtimeRoot, "link.xml");
        Assert.True(File.Exists(linkPath), linkPath);
        var linked = System.Xml.Linq.XDocument.Load(linkPath).Descendants("assembly")
            .Select(element => (string?)element.Attribute("fullname"))
            .Where(name => name is not null).Cast<string>().ToArray();
        Assert.Contains("Spire.UnityWebBehavioral", linked);
        Assert.DoesNotContain("Houtamelo.Spire", linked);
        Assert.Contains("xunit.core", linked);
        Assert.DoesNotContain("Houtamelo.Spire.Analyzers", linked);
        Assert.DoesNotContain("Houtamelo.Spire.PatternAnalysis", linked);
        Assert.DoesNotContain("Houtamelo.Spire.CodeFixes", linked);

        var files = Directory.EnumerateFiles(Path.Combine(fixture.ProjectPath, "Assets"), "*", SearchOption.AllDirectories);
        Assert.DoesNotContain(files, path => Path.GetFileName(path).Contains("CodeAnalysis", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, path => Path.GetFileName(path).Contains("Workspaces", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, path => path.Contains(Path.Combine("examples", ""), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, path => Path.GetFileName(path).Equals("XunitFactBridge.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrepareProject_RepeatedRunRemovesStaleWebFixtureFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        AssertBuildArtifactsPresent(repositoryRoot, configuration);

        using var fixture = PreparationFixture.Create(repositoryRoot);
        var editor = fixture.CreateIsolatedEditor();
        var first = await RunPrepareProjectAsync(repositoryRoot, fixture.ProjectPath, configuration, editor);
        Assert.True(first.ExitCode == 0, CombinedOutput(first));
        var stale = Path.Combine(fixture.ProjectPath, "Assets", "WebBehavioral", "stale.cs");
        File.WriteAllText(stale, "stale");

        var second = await RunPrepareProjectAsync(repositoryRoot, fixture.ProjectPath, configuration, editor);
        Assert.True(second.ExitCode == 0, CombinedOutput(second));
        Assert.False(File.Exists(stale));
        Assert.True(File.Exists(Path.Combine(fixture.ProjectPath, "Assets", "WebBehavioral", "Spire.UnityWebBehavioral.asmdef")));
    }

    [LinuxFact]
    public async Task PrepareProject_RejectsExamplesPathAndSymlinkBeforeMutation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        AssertBuildArtifactsPresent(repositoryRoot, configuration);
        using var fixture = PreparationFixture.Create(repositoryRoot);
        var editor = fixture.CreateIsolatedEditor();

        var syntheticRoot = Path.Combine(fixture.Root, "synthetic-root");
        var examplesTarget = Path.Combine(syntheticRoot, "examples", "web-runtime-test-target");
        var alias = Path.Combine(fixture.Root, "examples-alias");
        Directory.CreateDirectory(examplesTarget);
        try
        {
            var direct = await RunPrepareProjectAsync(repositoryRoot, examplesTarget, configuration, editor, syntheticRoot);
            Assert.NotEqual(0, direct.ExitCode);
            Assert.Contains("examples", CombinedOutput(direct), StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFileSystemEntries(examplesTarget));

            var link = await ProcessRunner.RunAsync(
                "/bin/ln",
                new[] { "-s", examplesTarget, alias },
                fixture.Root,
                TimeSpan.FromSeconds(10));
            Assert.True(link.ExitCode == 0, CombinedOutput(link));
            var linked = await RunPrepareProjectAsync(repositoryRoot, alias, configuration, editor, syntheticRoot);
            Assert.NotEqual(0, linked.ExitCode);
            Assert.Contains("examples", CombinedOutput(linked), StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFileSystemEntries(examplesTarget));
        }
        finally
        {
            Directory.Delete(examplesTarget, recursive: true);
            File.Delete(alias);
        }
    }

    [Fact]
    public async Task PrepareProject_MissingEditorFailsBeforeCreatingProjectFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = BuildConfiguration();
        AssertBuildArtifactsPresent(repositoryRoot, configuration);
        using var fixture = PreparationFixture.Create(repositoryRoot);
        Directory.CreateDirectory(fixture.ProjectPath);
        var sentinel = Path.Combine(fixture.ProjectPath, "sentinel.txt");
        File.WriteAllText(sentinel, "preserve", Encoding.UTF8);

        var missingEditor = Path.Combine(fixture.Root, "missing", "Unity");
        var result = await RunPrepareProjectAsync(repositoryRoot, fixture.ProjectPath, configuration, missingEditor);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("preserve", File.ReadAllText(sentinel, Encoding.UTF8));
        Assert.Single(Directory.EnumerateFiles(fixture.ProjectPath));
        Assert.DoesNotContain("WebRuntime project:", CombinedOutput(result), StringComparison.Ordinal);
    }

    private static async Task<ProcessResult> RunPrepareProjectAsync(
        string repositoryRoot, string projectPath, string configuration, string editorPath, string? rootOverride = null)
    {
        var cliPath = Path.Combine(repositoryRoot, "tools", "Spire.Tool", "bin", configuration, "net10.0", "Spire.Tool.dll");
        Assert.True(File.Exists(cliPath), cliPath);
        var arguments = new[] { cliPath }
            .Concat(rootOverride is null ? Array.Empty<string>() : new[] { "--root", rootOverride })
            .Concat(new[]
            {
                "unity-web", "--project", projectPath, "--configuration", configuration,
                "--no-build", "--prepare-only", "--editor", editorPath,
            });
        return await ProcessRunner.RunAsync(
            new ToolContext(repositoryRoot).Dotnet,
            arguments,
            repositoryRoot,
            ProcessTimeout,
            new Dictionary<string, string?>
            {
                ["CI"] = "true",
                ["GIT_TERMINAL_PROMPT"] = "0",
            });
    }

    private static Dictionary<string, string> CaptureFileDigests(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'),
                HashFile,
                StringComparer.Ordinal);

    private static Dictionary<string, string> CaptureAuthoredBehavioralDigests(string root)
        => new[] { "Types", "Tests" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'),
                HashFile,
                StringComparer.Ordinal);

    private static string HashFile(string path)
        => DevTools.ProcessCompatibility.ToHexString(
            DevTools.ProcessCompatibility.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string[] ExpectedFactNames(string testsRoot)
    {
        var names = new List<string>();
        foreach (var source in Directory.EnumerateFiles(testsRoot, "*.cs"))
        {
            var text = File.ReadAllText(source);
            var ns = Regex.Match(text, @"^namespace\s+([\w.]+)\s*;", RegexOptions.Multiline);
            var type = Regex.Match(text, @"^public\s+class\s+(\w+)\b", RegexOptions.Multiline);
            Assert.True(ns.Success, source);
            Assert.True(type.Success, source);
            foreach (Match fact in Regex.Matches(text, @"\[Fact(?:\([^\]]*\))?\]\s+public\s+(?:async\s+)?(?:void|Task|ValueTask)\s+(\w+)\s*\("))
                names.Add($"{ns.Groups[1].Value}.{type.Groups[1].Value}.{fact.Groups[1].Value}");
        }
        return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private static void AssertBuildArtifactsPresent(string repositoryRoot, string configuration)
    {
        foreach (var name in new[] { "Houtamelo.Spire", "Houtamelo.Spire.Analyzers", "Houtamelo.Spire.PatternAnalysis", "Houtamelo.Spire.CodeFixes" })
        {
            var path = Path.Combine(repositoryRoot, "src", name, "bin", configuration, "netstandard2.0", name + ".dll");
            Assert.True(File.Exists(path), path);
        }

        var deps = Path.Combine(repositoryRoot, "tools", "UnityTestDependencies", "bin", configuration, "netstandard2.1");
        foreach (var name in new[] { "Newtonsoft.Json.dll", "System.IO.Pipelines.dll", "System.Runtime.CompilerServices.Unsafe.dll", "System.Text.Encodings.Web.dll", "System.Text.Json.dll", "xunit.abstractions.dll", "xunit.assert.dll", "xunit.core.dll" })
            Assert.True(File.Exists(Path.Combine(deps, name)), Path.Combine(deps, name));
    }

    private static string BuildConfiguration()
#if SPIRE_UNITY_TEST_HOST
        => Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_CONFIGURATION") ?? "Release";
#else
        => AppContext.BaseDirectory.Contains(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? "Release" : "Debug";
#endif

    private static string CombinedOutput(ProcessResult result) => result.StandardOutput + "\n" + result.StandardError;

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Spire.Analyzers.slnx"))) return directory.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class PreparationFixture : IDisposable
    {
        private PreparationFixture(string root)
        {
            Root = root;
            ProjectPath = Path.Combine(root, "project");
            Directory.CreateDirectory(root);
        }

        public string Root { get; }
        public string ProjectPath { get; }

        public static PreparationFixture Create(string repositoryRoot)
            => new(Path.Combine(repositoryRoot, "tmp", "devtools-unity-web-tests", Guid.NewGuid().ToString("N")));

        public string CreateIsolatedEditor()
        {
            var installed = ToolContext.DefaultEditor;
            var installedNewtonsoft = Path.Combine(Path.GetDirectoryName(installed)!, "Data", "Managed", "Newtonsoft.Json.dll");
            Assert.True(File.Exists(installed), installed);
            Assert.True(File.Exists(installedNewtonsoft), installedNewtonsoft);
            var editor = Path.Combine(Root, "fake-unity", "Hub", "Editor", "6000.5.6f1", "Editor", "Unity");
            Directory.CreateDirectory(Path.GetDirectoryName(editor)!);
            File.WriteAllBytes(editor, Array.Empty<byte>());
            var managed = Path.Combine(Path.GetDirectoryName(editor)!, "Data", "Managed");
            Directory.CreateDirectory(managed);
            File.Copy(installedNewtonsoft, Path.Combine(managed, "Newtonsoft.Json.dll"), true);
            return editor;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
