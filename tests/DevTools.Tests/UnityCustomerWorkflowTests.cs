using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class UnityCustomerWorkflowTests
{
    private const string Configuration = "Debug";
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task PrepareOnly_CleanProjectCopiesFixtureWithoutPackageOrShowcaseAndNeedsNoEditor()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = WorkflowFixture.Create(repositoryRoot);
        await BuildPackageAsync(repositoryRoot, fixture.CandidatePath);

        var result = await RunCustomerAsync(
            repositoryRoot,
            fixture.ProjectPath,
            fixture.CandidatePath,
            editor: Path.Combine(fixture.Root, "editor-that-must-not-be-launched"));

        AssertSuccess(result);
        AssertTreeEqual(
            Path.Combine(repositoryRoot, "tests", "UnityCustomerAcceptance", "Assets"),
            Path.Combine(fixture.ProjectPath, "Assets"));
        AssertTreeEqual(
            Path.Combine(repositoryRoot, "tests", "UnityCustomerAcceptance", "Packages"),
            Path.Combine(fixture.ProjectPath, "Packages"));
        AssertTreeEqual(
            Path.Combine(repositoryRoot, "tests", "UnityPackageSample", "ProjectSettings"),
            Path.Combine(fixture.ProjectPath, "ProjectSettings"));
        Assert.False(Directory.Exists(Path.Combine(fixture.ProjectPath, "Assets", "Samples", "SpireAcceptance")));
        Assert.False(Directory.Exists(Path.Combine(fixture.ProjectPath, "Packages", "individual.antonio-pedro-goncalves-ferreira.spire")));
        AssertSpirePackageDependency(fixture.ProjectPath, baseline: null);
        AssertCustomerInputs(fixture.ProjectPath, fixture.CandidatePath, baseline: null);
    }

    [Fact]
    public async Task PrepareOnly_BaselineCopiesConsumerShowcaseAndPreservesRealPackageInputs()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = WorkflowFixture.Create(repositoryRoot);
        await BuildPackageAsync(repositoryRoot, fixture.CandidatePath);
        await BuildPackageAsync(repositoryRoot, fixture.BaselinePath);
        var baselineOnlyAsset = Path.Combine(
            fixture.BaselinePath,
            "Samples~",
            "Showcase",
            "baseline-only-acceptance-marker.txt");
        File.WriteAllText(baselineOnlyAsset, "copied from baseline package only\n");

        var result = await RunCustomerAsync(
            repositoryRoot,
            fixture.ProjectPath,
            fixture.CandidatePath,
            fixture.BaselinePath);

        AssertSuccess(result);
        AssertTreeSubset(
            Path.Combine(repositoryRoot, "tests", "UnityCustomerAcceptance", "Assets"),
            Path.Combine(fixture.ProjectPath, "Assets"));
        AssertTreeEqual(
            Path.Combine(fixture.BaselinePath, "Samples~", "Showcase"),
            Path.Combine(fixture.ProjectPath, "Assets", "Samples", "SpireAcceptance"));
        Assert.Equal(
            "copied from baseline package only\n",
            File.ReadAllText(Path.Combine(
                fixture.ProjectPath,
                "Assets",
                "Samples",
                "SpireAcceptance",
                "baseline-only-acceptance-marker.txt")));
        AssertTreeSubset(
            Path.Combine(repositoryRoot, "tests", "UnityCustomerAcceptance", "Packages"),
            Path.Combine(fixture.ProjectPath, "Packages"),
            skipFile: "manifest.json");
        AssertSpirePackageDependency(fixture.ProjectPath, fixture.BaselinePath);
        AssertCustomerInputs(fixture.ProjectPath, fixture.CandidatePath, fixture.BaselinePath);
    }

    [Theory]
    [InlineData("candidate", "missing")]
    [InlineData("candidate", "wrong-name")]
    [InlineData("candidate", "missing-dll")]
    [InlineData("baseline", "missing")]
    [InlineData("baseline", "wrong-name")]
    [InlineData("baseline", "missing-dll")]
    public async Task PrepareOnly_InvalidCandidateOrBaselineFailsBeforeProjectMutation(
        string role,
        string invalidKind)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = WorkflowFixture.Create(repositoryRoot);
        var packagePath = role == "candidate" ? fixture.CandidatePath : fixture.BaselinePath;
        string? baselinePath = null;
        if (role == "baseline")
        {
            await BuildPackageAsync(repositoryRoot, fixture.CandidatePath);
            baselinePath = invalidKind == "missing" ? packagePath : fixture.BaselinePath;
        }
        if (invalidKind != "missing")
        {
            await BuildPackageAsync(repositoryRoot, packagePath);
            InvalidatePackage(packagePath, invalidKind);
        }

        CreateUnrelatedProject(fixture.ProjectPath);
        var before = Snapshot(fixture.ProjectPath);

        var result = await RunCustomerAsync(
            repositoryRoot,
            fixture.ProjectPath,
            role == "candidate" ? packagePath : fixture.CandidatePath,
            baselinePath);

        AssertFailure(result);
        Assert.Equal(before, Snapshot(fixture.ProjectPath));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, "customer-inputs.json")));
    }

    [Fact]
    public async Task PrepareOnly_RejectsAnUnrelatedExistingProjectWithoutTouchingIt()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = WorkflowFixture.Create(repositoryRoot);
        await BuildPackageAsync(repositoryRoot, fixture.CandidatePath);
        CreateUnrelatedProject(fixture.ProjectPath);
        var before = Snapshot(fixture.ProjectPath);

        var result = await RunCustomerAsync(repositoryRoot, fixture.ProjectPath, fixture.CandidatePath);

        AssertFailure(result);
        Assert.Contains("unrelated", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(fixture.ProjectPath));
    }

    [Fact]
    public async Task PrepareOnly_RejectsTheTrackedCustomerAcceptanceFixtureWithoutChangingIt()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = WorkflowFixture.Create(repositoryRoot);
        await BuildPackageAsync(repositoryRoot, fixture.CandidatePath);
        var trackedFixture = Path.Combine(repositoryRoot, "tests", "UnityCustomerAcceptance");
        var before = Snapshot(trackedFixture);

        var result = await RunCustomerAsync(repositoryRoot, trackedFixture, fixture.CandidatePath);

        AssertFailure(result);
        Assert.Equal(before, Snapshot(trackedFixture));
    }

    [Fact]
    public async Task PrepareOnly_RejectsCandidatePackageInsideAnIsolatedRepositoryExamplesDirectoryBeforeMutation()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = WorkflowFixture.Create(repositoryRoot);
        await BuildPackageAsync(repositoryRoot, fixture.CandidatePath);

        var isolatedExamples = Path.Combine(fixture.Root, "examples", "candidate");
        CopyDirectory(fixture.CandidatePath, isolatedExamples);
        CreateUnrelatedProject(fixture.ProjectPath);
        var before = Snapshot(fixture.ProjectPath);

        var result = await RunCustomerAsync(
            repositoryRoot,
            fixture.ProjectPath,
            isolatedExamples,
            editor: Path.Combine(fixture.Root, "editor-that-must-not-be-launched"),
            rootOverride: fixture.Root);

        AssertFailure(result);
        Assert.Contains("repository examples directory", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(fixture.ProjectPath));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, ".spire-unity-customer")));
    }

    [Fact]
    public async Task PrepareOnly_RejectsBaselinePackageInsideAnIsolatedRepositoryExamplesDirectoryBeforeMutation()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = WorkflowFixture.Create(repositoryRoot);
        await BuildPackageAsync(repositoryRoot, fixture.CandidatePath);
        await BuildPackageAsync(repositoryRoot, fixture.BaselinePath);

        var isolatedExamples = Path.Combine(fixture.Root, "examples", "baseline");
        CopyDirectory(fixture.BaselinePath, isolatedExamples);
        CreateUnrelatedProject(fixture.ProjectPath);
        var before = Snapshot(fixture.ProjectPath);

        var result = await RunCustomerAsync(
            repositoryRoot,
            fixture.ProjectPath,
            fixture.CandidatePath,
            isolatedExamples,
            editor: Path.Combine(fixture.Root, "editor-that-must-not-be-launched"),
            rootOverride: fixture.Root);

        AssertFailure(result);
        Assert.Contains("repository examples directory", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(fixture.ProjectPath));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, ".spire-unity-customer")));
    }

    [Fact]
    public async Task PrepareOnly_MissingBaselineShowcaseFailsBeforeMutatingTheProject()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = WorkflowFixture.Create(repositoryRoot);
        await BuildPackageAsync(repositoryRoot, fixture.CandidatePath);
        await BuildPackageAsync(repositoryRoot, fixture.BaselinePath);
        Directory.Delete(Path.Combine(fixture.BaselinePath, "Samples~", "Showcase"), recursive: true);
        CreateUnrelatedProject(fixture.ProjectPath);
        var before = Snapshot(fixture.ProjectPath);

        var result = await RunCustomerAsync(
            repositoryRoot,
            fixture.ProjectPath,
            fixture.CandidatePath,
            fixture.BaselinePath,
            editor: Path.Combine(fixture.Root, "editor-that-must-not-be-launched"));

        AssertFailure(result);
        Assert.Contains("Showcase sample", CombinedOutput(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(fixture.ProjectPath));
        Assert.False(File.Exists(Path.Combine(fixture.ProjectPath, ".spire-unity-customer")));
    }

    private static async Task BuildPackageAsync(string repositoryRoot, string outputPath)
    {
        var result = await RunCliAsync(
            repositoryRoot,
            "package",
            "--output",
            outputPath,
            "--configuration",
            Configuration,
            "--no-build");
        AssertSuccess(result);
    }

    private static async Task<ProcessResult> RunCustomerAsync(
        string repositoryRoot,
        string projectPath,
        string packagePath,
        string? baselinePath = null,
        string? editor = null,
        string? rootOverride = null)
    {
        var arguments = new List<string>
        {
            "unity-customer",
            "--package",
            packagePath,
            "--project",
            projectPath,
            "--configuration",
            Configuration,
            "--no-build",
            "--prepare-only",
        };
        if (baselinePath is not null)
        {
            arguments.Add("--baseline");
            arguments.Add(baselinePath);
        }
        if (editor is not null)
        {
            arguments.Add("--editor");
            arguments.Add(editor);
        }

        return rootOverride is null
            ? await RunCliAsync(repositoryRoot, arguments.ToArray())
            : await RunCliWithRootAsync(repositoryRoot, rootOverride, arguments.ToArray());
    }

    private static async Task<ProcessResult> RunCliWithRootAsync(
        string repositoryRoot,
        string rootOverride,
        params string[] arguments)
        => await RunCliAsync(repositoryRoot, new[] { "--root", rootOverride }.Concat(arguments).ToArray());

    private static async Task<ProcessResult> RunCliAsync(string repositoryRoot, params string[] arguments)
    {
        var launcherConfiguration = Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_CONFIGURATION") ??
            (AppContext.BaseDirectory.Contains(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal) ? "Release" : "Debug");
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

    private static void AssertCustomerInputs(string projectPath, string packagePath, string? baseline)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectPath, "customer-inputs.json")));
        var inputs = document.RootElement;
        Assert.Equal(Path.GetFullPath(packagePath), inputs.GetProperty("package").GetString());
        Assert.Equal(baseline is null ? null : Path.GetFullPath(baseline), inputs.GetProperty("baseline").GetString());
        Assert.Equal(Hash(Path.Combine(packagePath, "package.json")), inputs.GetProperty("candidateManifestSha256").GetString());
        Assert.Equal(
            baseline is null ? null : Hash(Path.Combine(baseline, "package.json")),
            inputs.GetProperty("baselineManifestSha256").GetString());
    }

    private static void AssertSpirePackageDependency(string projectPath, string? baseline)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(projectPath, "Packages", "manifest.json")));
        var dependencies = document.RootElement.GetProperty("dependencies");
        if (baseline is null)
        {
            Assert.False(dependencies.TryGetProperty("individual.antonio-pedro-goncalves-ferreira.spire", out _));
            return;
        }

        Assert.Equal(
            "file:" + Path.GetFullPath(baseline),
            dependencies.GetProperty("individual.antonio-pedro-goncalves-ferreira.spire").GetString());
    }

    private static void InvalidatePackage(string packagePath, string invalidKind)
    {
        switch (invalidKind)
        {
            case "wrong-name":
                var manifest = Path.Combine(packagePath, "package.json");
                var text = File.ReadAllText(manifest);
                Assert.Contains("individual.antonio-pedro-goncalves-ferreira.spire", text, StringComparison.Ordinal);
                File.WriteAllText(manifest, text.Replace("individual.antonio-pedro-goncalves-ferreira.spire", "com.example.wrong", StringComparison.Ordinal));
                break;
            case "missing-dll":
                File.Delete(Path.Combine(packagePath, "Runtime", "Houtamelo.Spire.dll"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidKind), invalidKind, null);
        }
    }

    private static void CreateUnrelatedProject(string projectPath)
    {
        Directory.CreateDirectory(Path.Combine(projectPath, "Assets"));
        File.WriteAllText(Path.Combine(projectPath, "unrelated-sentinel.txt"), "preserve this file\n");
        File.WriteAllText(Path.Combine(projectPath, "Assets", "unrelated.asset"), "preserve this asset\n");
    }

    private static void CopyDirectory(string source, string target)
    {
        Assert.True(Directory.Exists(source), source);
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    private static void AssertTreeEqual(string source, string target)
    {
        Assert.Equal(Snapshot(source), Snapshot(target));
    }

    private static void AssertTreeSubset(string source, string target, string? skipFile = null)
    {
        Assert.True(Directory.Exists(target), target);
        foreach (var sourcePath in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (skipFile is not null &&
                string.Equals(Path.GetFileName(sourcePath), skipFile, StringComparison.Ordinal))
                continue;
            var targetPath = Path.Combine(target, RelativePath(source, sourcePath).Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(targetPath), targetPath);
            Assert.Equal(File.ReadAllBytes(sourcePath), File.ReadAllBytes(targetPath));
        }
    }

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

        throw new InvalidOperationException("Could not locate the repository root for Unity customer workflow tests.");
    }

    private sealed class WorkflowFixture : IDisposable
    {
        private WorkflowFixture(string root)
        {
            Root = root;
            ProjectPath = Path.Combine(root, "project");
            CandidatePath = Path.Combine(root, "candidate");
            BaselinePath = Path.Combine(root, "baseline");
            Directory.CreateDirectory(root);
        }

        public string Root { get; }
        public string ProjectPath { get; }
        public string CandidatePath { get; }
        public string BaselinePath { get; }

        public static WorkflowFixture Create(string repositoryRoot)
            => new(Path.Combine(repositoryRoot, "tmp", "devtools-unity-customer-tests", Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
