using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class ToolCliTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Help_DocumentsEverySupportedSubcommand()
    {
        var repositoryRoot = FindRepositoryRoot();

        var result = await RunCliAsync(repositoryRoot, "--help");

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        foreach (var command in new[]
        {
            "package",
            "unity-integration",
            "unity-behavioral",
            "unity-harness",
            "unity-asmdefs",
            "jetbrains",
            "hook",
        })
        {
            Assert.Contains(command, result.CombinedOutput, StringComparison.Ordinal);
        }

        Assert.Contains("--backend", result.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Mono", result.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("IL2CPP", result.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnityBehavioral_InvalidBackendReturnsArgumentErrorBeforeEditorValidation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var missingEditor = Path.Combine(repositoryRoot, "tmp", "missing-editor-for-backend-test");
        var missingProject = Path.Combine(repositoryRoot, "tmp", "missing-project-for-backend-test");

        var result = await RunCliAsync(
            repositoryRoot,
            "unity-behavioral",
            "--backend",
            "Vulkan",
            "--editor",
            missingEditor,
            "--project",
            missingProject);

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 2, result.CombinedOutput);
        Assert.Contains("backend", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("editor executable", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Package_BackendOptionIsRejectedAsAnInappropriateCommandOption()
    {
        var repositoryRoot = FindRepositoryRoot();

        var result = await RunCliAsync(repositoryRoot, "package", "--backend", "Mono");

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 2, result.CombinedOutput);
        Assert.Contains("backend", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownCommand_ReturnsArgumentError()
    {
        var repositoryRoot = FindRepositoryRoot();

        var result = await RunCliAsync(repositoryRoot, "unknown-command");

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 2, result.CombinedOutput);
        Assert.Contains("unknown", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownGlobalOption_ReturnsArgumentError()
    {
        var repositoryRoot = FindRepositoryRoot();

        var result = await RunCliAsync(repositoryRoot, "--definitely-not-a-real-option");

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 2, result.CombinedOutput);
        Assert.Contains("option", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hook_SessionStartWritesExactlyOneJsonDocumentWithoutBuildNoise()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var fixture = CliFixture.Create(repositoryRoot);
        var input = "{\"hook_event_name\":\"SessionStart\",\"session_id\":\"cli-session\",\"transcript_path\":\"unavailable\"}\n";

        var result = await RunCliWithInputAsync(
            repositoryRoot,
            input,
            "--root",
            fixture.Root,
            "hook",
            "SessionStart");

        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Empty(result.StandardError);

        using var document = JsonDocument.Parse(result.StandardOutput);
        var hook = document.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("SessionStart", hook.GetProperty("hookEventName").GetString());
        Assert.Contains(
            Path.Combine(fixture.Root, "session-reviews", "cli-session.md"),
            hook.GetProperty("additionalContext").GetString()!,
            StringComparison.Ordinal);
        Assert.True(
            File.Exists(Path.Combine(fixture.Root, "session-reviews", "cli-session.md")),
            "The SessionStart hook did not create its review file.");
    }

    private static async Task<CliResult> RunCliAsync(
        string repositoryRoot,
        params string[] arguments)
        => await RunCliProcessAsync(repositoryRoot, null, arguments);

    private static async Task<CliResult> RunCliWithInputAsync(
        string repositoryRoot,
        string? input,
        params string[] arguments)
        => await RunCliProcessAsync(repositoryRoot, input, arguments);

    private static async Task<CliResult> RunCliProcessAsync(
        string repositoryRoot,
        string? input,
        params string[] arguments)
    {
        var cliPath = Path.Combine(
            repositoryRoot,
            "tools",
            "Spire.Tool",
            "bin",
            BuildConfiguration(),
            "net10.0",
            "Spire.Tool.dll");
        Assert.True(File.Exists(cliPath), $"Missing Spire.Tool CLI: {cliPath}");

        var info = new ProcessStartInfo
        {
            FileName = new ToolContext(repositoryRoot).Dotnet,
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.Environment["CI"] = "true";
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        DevTools.ProcessCompatibility.AddArgument(info, cliPath);
        foreach (var argument in arguments)
            DevTools.ProcessCompatibility.AddArgument(info, argument);

        using var process = new Process { StartInfo = info };
        Assert.True(process.Start(), $"Could not start CLI through '{info.FileName}'.");

        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (input is null)
            process.StandardInput.Close();
        else
        {
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
        }

        var timedOut = false;
        using (var cancellation = new CancellationTokenSource(ProcessTimeout))
        {
            try
            {
                await DevTools.ProcessCompatibility.WaitForExitAsync(process, cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                timedOut = true;
                if (!process.HasExited)
                    DevTools.ProcessCompatibility.KillTree(process);
                await DevTools.ProcessCompatibility.WaitForExitAsync(process);
            }
        }

        return new CliResult(process.ExitCode, await output, await error, timedOut);
    }

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

        throw new InvalidOperationException("Could not locate the repository root containing the Spire.Tool CLI.");
    }

    private sealed class CliFixture : IDisposable
    {
        private CliFixture(string root)
        {
            Root = root;
            Directory.CreateDirectory(root);
        }

        public string Root { get; }

        public static CliFixture Create(string repositoryRoot)
            => new(Path.Combine(repositoryRoot, "tmp", "devtools-cli-tests", Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut)
    {
        public string CombinedOutput => StandardOutput + Environment.NewLine + StandardError;
    }
}
