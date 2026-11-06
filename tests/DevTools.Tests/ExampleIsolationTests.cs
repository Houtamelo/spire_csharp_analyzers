using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class ExampleIsolationTests
{
    private static readonly TimeSpan WorkflowTimeout = TimeSpan.FromSeconds(1);

    [Theory]
    [InlineData("directory")]
    [InlineData("nested")]
    [InlineData("normalized")]
    public async Task AsmdefPreparation_RejectsEveryExamplesPathBeforeMutation(string pathKind)
    {
        using var fixture = IsolationFixture.Create(pathKind);
        await AssertGuardedAsync(
            fixture,
            () => AsmdefWorkflow.PrepareAsync(
                fixture.Context,
                fixture.ProjectPath,
                "Debug",
                build: false));
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("nested")]
    [InlineData("normalized")]
    public async Task BehavioralPreparation_RejectsEveryExamplesPathBeforeMutation(string pathKind)
    {
        using var fixture = IsolationFixture.Create(pathKind);
        await AssertGuardedAsync(
            fixture,
            () => BehavioralWorkflow.PrepareAsync(
                fixture.Context,
                fixture.ProjectPath,
                "Debug",
                build: false,
                editor: fixture.MissingEditor));
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("nested")]
    [InlineData("normalized")]
    public async Task HarnessPreparation_RejectsEveryExamplesPathBeforeArchiving(string pathKind)
    {
        using var fixture = IsolationFixture.Create(pathKind);
        await AssertGuardedAsync(
            fixture,
            () => HarnessWorkflow.PrepareAsync(
                fixture.Context,
                fixture.ProjectPath,
                fixture.MissingEditor,
                "Debug",
                build: false));
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("nested")]
    [InlineData("normalized")]
    public async Task IntegrationWorkflow_RejectsEveryExamplesPathBeforeEditorAccess(string pathKind)
    {
        using var fixture = IsolationFixture.Create(pathKind);
        await AssertGuardedAsync(
            fixture,
            () => IntegrationWorkflow.RunAsync(
                fixture.Context,
                fixture.ProjectPath,
                fixture.MissingEditor,
                "Debug",
                build: false,
                graphics: false,
                "Spire.UnityIntegration.SpireUnityIntegration.Run",
                WorkflowTimeout));
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("nested")]
    [InlineData("normalized")]
    public async Task JetBrainsWorkflow_RejectsEveryExamplesPathBeforeToolAccess(string pathKind)
    {
        using var fixture = IsolationFixture.Create(pathKind);
        await AssertGuardedAsync(
            fixture,
            () => JetBrainsWorkflow.RunAsync(
                fixture.Context,
                fixture.ProjectPath,
                fixture.MissingRider,
                fixture.MissingCli,
                fixture.MissingEditor,
                "all",
                WorkflowTimeout));
    }

    private static async Task AssertGuardedAsync(IsolationFixture fixture, Func<Task> operation)
    {
        var before = fixture.Snapshot();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(operation);
        Assert.Contains("repository examples directory", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, fixture.Snapshot());
        Assert.DoesNotContain(
            fixture.Snapshot(),
            path => path.Contains(".spire-", StringComparison.Ordinal));
    }

    private sealed class IsolationFixture : IDisposable
    {
        private IsolationFixture(string root, string projectPath)
        {
            Root = root;
            ProjectPath = projectPath;
            Context = new ToolContext(root);
            MissingEditor = Path.Combine(root, "missing", "Unity.exe");
            MissingRider = Path.Combine(root, "missing", "rider");
            MissingCli = Path.Combine(root, "missing", "inspectcode");

            Directory.CreateDirectory(ProjectPath);
            foreach (var name in new[]
                     {
                         "asmdef-results.json",
                         "behavioral-results.xml",
                         "harness-results.xml",
                         "harness-inputs.json",
                         "unity-integration-1.log",
                         "jetbrains-inspections.sarif",
                     })
            {
                File.WriteAllText(Path.Combine(ProjectPath, name), "preserve " + name + "\n");
            }

            File.WriteAllText(Path.Combine(ProjectPath, "sentinel.txt"), "preserve this file\n");
        }

        public string Root { get; }
        public string ProjectPath { get; }
        public ToolContext Context { get; }
        public string MissingEditor { get; }
        public string MissingRider { get; }
        public string MissingCli { get; }

        public static IsolationFixture Create(string pathKind)
        {
            var repositoryRoot = FindRepositoryRoot();
            var root = Path.Combine(
                repositoryRoot,
                "tmp",
                "example-isolation-tests",
                Guid.NewGuid().ToString("N"));
            var examples = Path.Combine(root, "examples");
            var project = pathKind switch
            {
                "directory" => examples,
                "nested" => Path.Combine(examples, "project"),
                "normalized" => Path.Combine(root, "outside", "..", "examples", "project"),
                _ => throw new ArgumentOutOfRangeException(nameof(pathKind), pathKind, null),
            };
            return new IsolationFixture(root, project);
        }

        public string[] Snapshot()
            => Directory.EnumerateFiles(ProjectPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(ProjectPath, path) + "=" + File.ReadAllText(path))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Spire.Analyzers.slnx")) &&
                    File.Exists(Path.Combine(directory.FullName, "tools", "Spire.Tool", "Spire.Tool.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root for example-isolation tests.");
    }
}
