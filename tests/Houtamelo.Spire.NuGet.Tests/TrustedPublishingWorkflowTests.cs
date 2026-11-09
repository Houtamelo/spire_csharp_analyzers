using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace Houtamelo.Spire.NuGet.Tests;

public sealed class TrustedPublishingWorkflowTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    private static string BashExecutable => Path.DirectorySeparatorChar == '\\'
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe")
        : "bash";

    [Theory]
    [InlineData("push", "v5.0.0", "refs/tags/v5.0.0")]
    [InlineData("push", "v5.0.0-preview.1", "refs/tags/v5.0.0-preview.1")]
    [InlineData("workflow_dispatch", "v5.0.0", "refs/heads/main")]
    public async Task ValidateReleaseRequest_AcceptsSupportedReleaseRequests(
        string eventName,
        string releaseTag,
        string gitHubRef)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = TempFixture.Create(repositoryRoot);

        var result = await RunValidationStepAsync(
            repositoryRoot,
            fixture,
            eventName,
            releaseTag,
            gitHubRef);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.False(File.Exists(fixture.SummaryPath));
    }

    [Theory]
    [InlineData("", "v5.0.0", "refs/tags/v5.0.0")]
    [InlineData("push", "5.0.0", "refs/tags/5.0.0")]
    [InlineData("push", "v5.0", "refs/tags/v5.0")]
    [InlineData("push", "v5.0.0/../../tmp", "refs/tags/v5.0.0/../../tmp")]
    [InlineData("push", "v5.0.0; touch injected", "refs/tags/v5.0.0; touch injected")]
    [InlineData("push", "v5.0.0$(touch injected)", "refs/tags/v5.0.0$(touch injected)")]
    public async Task ValidateReleaseRequest_RejectsMalformedTagsWithoutExecutingThem(
        string eventName,
        string releaseTag,
        string gitHubRef)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = TempFixture.Create(repositoryRoot);

        var result = await RunValidationStepAsync(
            repositoryRoot,
            fixture,
            eventName,
            releaseTag,
            gitHubRef);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ERROR:", result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.InjectedPath));
        Assert.False(File.Exists(fixture.SummaryPath));
    }

    [Theory]
    [InlineData("workflow_dispatch", "refs/heads/release")]
    [InlineData("workflow_dispatch", "refs/tags/v5.0.0")]
    [InlineData("push", "refs/heads/main")]
    [InlineData("push", "refs/tags/v4.0.0")]
    [InlineData("pull_request", "refs/pull/12/merge")]
    public async Task ValidateReleaseRequest_RejectsUnsupportedRefOrEvent(
        string eventName,
        string gitHubRef)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = TempFixture.Create(repositoryRoot);

        var result = await RunValidationStepAsync(
            repositoryRoot,
            fixture,
            eventName,
            "v5.0.0",
            gitHubRef);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ERROR:", result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.SummaryPath));
    }

    [Fact]
    public async Task ManualDispatch_ResolvesCheckoutToTagEvenWhenBranchHasTheSameName()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = TempFixture.Create(repositoryRoot);
        await InitializeVersionedRepositoryAsync(repositoryRoot, fixture.RootPath, "4.9.0");

        var oldCommit = await ReadGitRevisionAsync(fixture.RootPath, "refs/tags/v4.9.0^{commit}");
        var mainCommit = await ReadGitRevisionAsync(fixture.RootPath, "refs/heads/main^{commit}");
        var sameNamedBranchCommit = await ReadGitRevisionAsync(fixture.RootPath, "refs/heads/v4.9.0^{commit}");
        Assert.NotEqual(oldCommit, mainCommit);
        Assert.Equal(mainCommit, sameNamedBranchCommit);

        var flow = await RunTaggedVersionFlowAsync(repositoryRoot, fixture, "v4.9.0");
        Assert.Equal(0, flow.ExitCode);
        Assert.True(File.Exists(fixture.SummaryPath));
        Assert.True(File.Exists(fixture.PublishMarkerPath));
        Assert.Contains($"Release commit: {oldCommit}", File.ReadAllText(fixture.SummaryPath), StringComparison.Ordinal);

        Assert.Equal(oldCommit, await ReadGitRevisionAsync(fixture.RootPath, "HEAD"));
        Assert.Equal("4.9.0", ReadProjectVersion(fixture.RootPath, "Houtamelo.Spire"));
        Assert.Equal("4.9.0", ReadProjectVersion(fixture.RootPath, "Houtamelo.Spire.Analyzers"));
        Assert.Equal("4.9.0", ReadProjectVersion(fixture.RootPath, "Houtamelo.Spire.CodeFixes"));
    }

    [Fact]
    public async Task MissingTag_FailsCheckoutBeforeVersionCheckOrPublishMarker()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = TempFixture.Create(repositoryRoot);
        await InitializeVersionedRepositoryAsync(repositoryRoot, fixture.RootPath, "4.9.0");

        var checkout = await RunTaggedVersionFlowAsync(repositoryRoot, fixture, "v9.9.9");

        Assert.NotEqual(0, checkout.ExitCode);
        Assert.False(File.Exists(fixture.PublishMarkerPath));
        Assert.False(File.Exists(fixture.SummaryPath));
    }

    [Fact]
    public async Task VersionCheck_RejectsMismatchedPackagesBeforePublishMarker()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = TempFixture.Create(repositoryRoot);
        await InitializeMismatchedRepositoryAsync(repositoryRoot, fixture.RootPath);

        var versionCheck = await RunTaggedVersionFlowAsync(repositoryRoot, fixture, "v4.9.0");

        Assert.NotEqual(0, versionCheck.ExitCode);
        var output = versionCheck.StandardOutput + versionCheck.StandardError;
        Assert.Contains("does not match", output, StringComparison.Ordinal);
        Assert.Contains("4.9.1", output, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.PublishMarkerPath));
        Assert.False(File.Exists(fixture.SummaryPath));
    }

    private static async Task<ProcessResult> RunValidationStepAsync(
        string repositoryRoot,
        TempFixture fixture,
        string eventName,
        string releaseTag,
        string gitHubRef)
    {
        var scriptPath = Path.Combine(fixture.RootPath, "validate-release-request.sh");
        File.WriteAllText(
            scriptPath,
            "#!/usr/bin/env bash\nset -euo pipefail\n" + ExtractValidationScript(repositoryRoot) + "\n");

        return await ProcessRunner.RunAsync(
            BashExecutable,
            new[] { "--noprofile", "--norc", "-e", "-o", "pipefail", scriptPath },
            fixture.RootPath,
            ProcessTimeout,
            new Dictionary<string, string?>
            {
                ["CI"] = "true",
                ["RELEASE_TAG"] = releaseTag,
                ["GITHUB_EVENT_NAME"] = eventName,
                ["GITHUB_REF"] = gitHubRef,
                ["GITHUB_STEP_SUMMARY"] = fixture.SummaryPath,
                ["INJECTED_PATH"] = fixture.InjectedPath,
            });
    }

    private static async Task<ProcessResult> RunTaggedVersionFlowAsync(
        string repositoryRoot,
        TempFixture fixture,
        string releaseTag)
    {
        var scriptPath = Path.Combine(fixture.RootPath, "run-release-validation.sh");
        var checkoutRef = ResolveCheckoutRef(repositoryRoot, releaseTag);
        File.WriteAllText(
            scriptPath,
            "#!/usr/bin/env bash\nset -euo pipefail\n" +
            "git checkout --detach \"$CHECKOUT_REF\"\n" +
            ExtractStepScript(repositoryRoot, "Validate tag matches .csproj version") + "\n" +
            "printf 'published\\n' > \"$PUBLISH_MARKER\"\n");

        return await ProcessRunner.RunAsync(
            BashExecutable,
            new[] { "--noprofile", "--norc", "-e", "-o", "pipefail", scriptPath },
            fixture.RootPath,
            ProcessTimeout,
            new Dictionary<string, string?>
            {
                ["CI"] = "true",
                ["GIT_TERMINAL_PROMPT"] = "0",
                ["RELEASE_TAG"] = releaseTag,
                ["CHECKOUT_REF"] = checkoutRef,
                ["GITHUB_STEP_SUMMARY"] = fixture.SummaryPath,
                ["PUBLISH_MARKER"] = fixture.PublishMarkerPath,
            });
    }

    private static async Task InitializeVersionedRepositoryAsync(
        string repositoryRoot,
        string path,
        string oldVersion)
    {
        Assert.Equal(0, (await RunGitAsync(path, "init", "-b", "main")).ExitCode);
        Assert.Equal(0, (await RunGitAsync(path, "config", "user.name", "Spire release test")).ExitCode);
        Assert.Equal(0, (await RunGitAsync(path, "config", "user.email", "spire-release-test@example.invalid")).ExitCode);
        WriteAllProjectVersions(path, oldVersion);
        CopyVersionCheckScript(repositoryRoot, path);
        Assert.Equal(0, (await RunGitAsync(path, "add", ".")).ExitCode);
        Assert.Equal(0, (await RunGitAsync(path, "update-index", "--chmod=+x", "--", "tools/check-tag-version.sh")).ExitCode);
        Assert.Equal(0, (await RunGitAsync(path, "commit", "-m", "old release")).ExitCode);
        Assert.Equal(0, (await RunGitAsync(path, "tag", "v4.9.0")).ExitCode);

        WriteAllProjectVersions(path, "5.0.0");
        Assert.Equal(0, (await RunGitAsync(path, "add", ".")).ExitCode);
        Assert.Equal(0, (await RunGitAsync(path, "commit", "-m", "main development")).ExitCode);
        Assert.Equal(0, (await RunGitAsync(path, "branch", "v4.9.0")).ExitCode);
    }

    private static async Task InitializeMismatchedRepositoryAsync(string repositoryRoot, string path)
    {
        Assert.Equal(0, (await RunGitAsync(path, "init", "-b", "main")).ExitCode);
        Assert.Equal(0, (await RunGitAsync(path, "config", "user.name", "Spire release test")).ExitCode);
        Assert.Equal(0, (await RunGitAsync(path, "config", "user.email", "spire-release-test@example.invalid")).ExitCode);
        WriteProjectVersion(path, "Houtamelo.Spire", "4.9.0");
        WriteProjectVersion(path, "Houtamelo.Spire.Analyzers", "4.9.1");
        WriteProjectVersion(path, "Houtamelo.Spire.CodeFixes", "4.9.0");
        CopyVersionCheckScript(repositoryRoot, path);
        Assert.Equal(0, (await RunGitAsync(path, "add", ".")).ExitCode);
        Assert.Equal(0, (await RunGitAsync(path, "update-index", "--chmod=+x", "--", "tools/check-tag-version.sh")).ExitCode);
        Assert.Equal(0, (await RunGitAsync(path, "commit", "-m", "mismatched release")).ExitCode);
        Assert.Equal(0, (await RunGitAsync(path, "tag", "v4.9.0")).ExitCode);
    }

    private static void CopyVersionCheckScript(string repositoryRoot, string repositoryPath)
    {
        var targetDirectory = Path.Combine(repositoryPath, "tools");
        Directory.CreateDirectory(targetDirectory);
        File.Copy(
            Path.Combine(repositoryRoot, "tools", "check-tag-version.sh"),
            Path.Combine(targetDirectory, "check-tag-version.sh"));
    }

    private static async Task<ProcessResult> RunGitAsync(string workingDirectory, params string[] arguments)
        => await ProcessRunner.RunAsync(
            "git",
            arguments,
            workingDirectory,
            ProcessTimeout,
            new Dictionary<string, string?>
            {
                ["CI"] = "true",
                ["GIT_TERMINAL_PROMPT"] = "0",
            });

    private static async Task<string> ReadGitRevisionAsync(string workingDirectory, string reference)
    {
        var result = await RunGitAsync(workingDirectory, "rev-parse", "--verify", reference);
        Assert.Equal(0, result.ExitCode);
        return result.StandardOutput.Trim();
    }

    private static string ResolveCheckoutRef(string repositoryRoot, string releaseTag)
    {
        var template = ReadCheckoutRefTemplate(repositoryRoot);
        const string Placeholder = "${{ env.RELEASE_TAG }}";
        Assert.Contains(Placeholder, template, StringComparison.Ordinal);
        return template.Replace(Placeholder, releaseTag, StringComparison.Ordinal);
    }

    private static string ExtractStepScript(string repositoryRoot, string stepName)
    {
        var lines = File.ReadAllLines(Path.Combine(repositoryRoot, ".github", "workflows", "publish.yml"));
        var step = Array.FindIndex(lines, line => line.Trim() == "- name: " + stepName);
        Assert.True(step >= 0, "The publish workflow must retain the expected step: " + stepName);

        var run = Array.FindIndex(lines, step, lines.Length - step, line => line.Trim() == "run: |");
        Assert.True(run >= 0, "The release-request validation step must use a literal Bash block.");

        var body = new List<string>();
        for (var index = run + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.StartsWith("      - ", StringComparison.Ordinal))
                break;

            if (line.StartsWith("          ", StringComparison.Ordinal))
                body.Add(line[10..]);
            else if (string.IsNullOrWhiteSpace(line))
                body.Add(string.Empty);
            else
                break;
        }

        Assert.NotEmpty(body);
        return string.Join(Environment.NewLine, body);
    }

    private static string ExtractValidationScript(string repositoryRoot)
        => ExtractStepScript(repositoryRoot, "Validate release request");

    private static string ReadCheckoutRefTemplate(string repositoryRoot)
    {
        var lines = File.ReadAllLines(Path.Combine(repositoryRoot, ".github", "workflows", "publish.yml"));
        var step = Array.FindIndex(lines, line => line.Trim() == "- name: Checkout");
        Assert.True(step >= 0, "The publish workflow must retain an explicit checkout step.");

        for (var index = step + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.StartsWith("      - ", StringComparison.Ordinal))
                break;
            if (line.TrimStart().StartsWith("ref:", StringComparison.Ordinal))
                return line[(line.IndexOf("ref:", StringComparison.Ordinal) + 5)..].Trim();
        }

        throw new InvalidOperationException("The publish workflow must specify the checkout ref.");
    }

    private static void WriteAllProjectVersions(string root, string version)
    {
        foreach (var package in PackageNames)
            WriteProjectVersion(root, package, version);
    }

    private static void WriteProjectVersion(string root, string package, string version)
    {
        var path = Path.Combine(root, "src", package);
        Directory.CreateDirectory(path);
        File.WriteAllText(
            Path.Combine(path, package + ".csproj"),
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><Version>{version}</Version></PropertyGroup></Project>\n");
    }

    private static string ReadProjectVersion(string root, string package)
    {
        var path = Path.Combine(root, "src", package, package + ".csproj");
        var line = File.ReadAllText(path);
        var start = line.IndexOf("<Version>", StringComparison.Ordinal) + "<Version>".Length;
        var end = line.IndexOf("</Version>", start, StringComparison.Ordinal);
        return line[start..end];
    }

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

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static readonly string[] PackageNames =
    [
        "Houtamelo.Spire",
        "Houtamelo.Spire.Analyzers",
        "Houtamelo.Spire.CodeFixes",
    ];

    private sealed class TempFixture : IDisposable
    {
        private TempFixture(string root)
        {
            RootPath = root;
            Directory.CreateDirectory(root);
        }

        public string RootPath { get; }

        public string SummaryPath => Path.Combine(RootPath, "summary.txt");

        public string InjectedPath => Path.Combine(RootPath, "injected");

        public string PublishMarkerPath => Path.Combine(RootPath, "published");

        public static TempFixture Create(string repositoryRoot)
            => new(Path.Combine(
                repositoryRoot,
                "tmp",
                "trusted-publishing-tests",
                Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
