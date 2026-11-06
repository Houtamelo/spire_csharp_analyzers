using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class LifecycleHookTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task SessionStart_CreatesSanitizedReviewAndDoesNotOverwriteIt()
    {
        using var fixture = LifecycleFixture.Create();
        var input = Event(
            "SessionStart",
            "session/with spaces",
            transcriptPath: Path.Combine(fixture.Root, "incoming.jsonl"));

        var first = await RunAsync(fixture, input);

        var reviewPath = Path.Combine(
            fixture.Root,
            "session-reviews",
            "session_with_spaces.md");
        Assert.True(File.Exists(reviewPath), reviewPath);
        var firstContents = File.ReadAllText(reviewPath);
        Assert.Contains("# Session Review", firstContents, StringComparison.Ordinal);
        Assert.Contains("session_with_spaces", firstContents, StringComparison.Ordinal);
        Assert.Contains("incoming.jsonl", firstContents, StringComparison.Ordinal);
        Assert.Contains("- **Started at**:", firstContents, StringComparison.Ordinal);

        var hookOutput = first?["hookSpecificOutput"]?.AsObject();
        Assert.NotNull(hookOutput);
        Assert.Equal("SessionStart", hookOutput!["hookEventName"]!.GetValue<string>());
        Assert.Contains(reviewPath, hookOutput["additionalContext"]!.GetValue<string>(), StringComparison.Ordinal);

        File.AppendAllText(reviewPath, "\nmanual review note\n", Encoding.UTF8);

        var second = await RunAsync(fixture, input);

        Assert.NotNull(second);
        Assert.Contains("manual review note", File.ReadAllText(reviewPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stop_WithCleanGitRepository_WritesSummaryAndContinues()
    {
        using var fixture = LifecycleFixture.Create();
        await fixture.InitializeGitRepositoryAsync();

        var result = await RunAsync(
            fixture,
            Event(
                "Stop",
                "clean-session",
                lastAssistantMessage: "The task is complete."));

        var summaryPath = Path.Combine(
            fixture.Root,
            "feedback",
            "summaries",
            "clean-session.md");
        Assert.True(File.Exists(summaryPath), summaryPath);
        var summary = File.ReadAllText(summaryPath);
        Assert.Contains("# Codex Session Summary", summary, StringComparison.Ordinal);
        Assert.Contains("The task is complete.", summary, StringComparison.Ordinal);
        Assert.Equal(true, result?["continue"]?.GetValue<bool>());
        Assert.Null(result?["decision"]);
    }

    [Fact]
    public async Task Stop_WhenGitStatusFails_BlocksWithAnActionableReason()
    {
        using var fixture = LifecycleFixture.Create();

        var result = await RunAsync(
            fixture,
            Event("Stop", "non-repository"));

        Assert.Equal("block", result?["decision"]?.GetValue<string>());
        var reason = result?["reason"]?.GetValue<string>();
        Assert.NotNull(reason);
        Assert.Contains("git", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stop_WhenChangedCSharpFileWasDeleted_DoesNotRunFormatGate()
    {
        using var fixture = LifecycleFixture.Create();
        await fixture.InitializeGitRepositoryAsync();

        var deletedSource = Path.Combine(fixture.Root, "src", "Deleted.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(deletedSource)!);
        File.WriteAllText(
            deletedSource,
            "namespace LifecycleFixture; public static class Deleted { public static int Value => 1; }\n",
            Encoding.UTF8);
        await fixture.GitAsync("add", "src/Deleted.cs");
        await fixture.GitAsync("commit", "-m", "add source");
        File.Delete(deletedSource);

        var result = await RunAsync(
            fixture,
            Event("Stop", "deleted-source"));

        Assert.Equal(true, result?["continue"]?.GetValue<bool>());
        Assert.Null(result?["decision"]);
        Assert.True(
            File.Exists(Path.Combine(fixture.Root, "feedback", "summaries", "deleted-source.md")));
    }

    [Fact]
    public async Task Stop_WithChangedSourceAndNoProject_BlocksWhenRealFormatGateFails()
    {
        using var fixture = LifecycleFixture.Create();
        await fixture.InitializeGitRepositoryAsync();

        var changedSource = Path.Combine(fixture.Root, "src", "Changed.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(changedSource)!);
        File.WriteAllText(
            changedSource,
            "namespace LifecycleFixture; public static class Changed { public static int Value => 1; }\n",
            Encoding.UTF8);
        await fixture.GitAsync("add", "src/Changed.cs");
        await fixture.GitAsync("commit", "-m", "add source");
        File.AppendAllText(changedSource, "// changed after commit\n", Encoding.UTF8);

        var result = await RunAsync(
            fixture,
            Event("Stop", "format-failure"));

        Assert.Equal("block", result?["decision"]?.GetValue<string>());
        var reason = result?["reason"]?.GetValue<string>();
        Assert.NotNull(reason);
        Assert.Contains("format", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SessionEnd_CopiesTheSuppliedTranscriptToASanitizedPath()
    {
        using var fixture = LifecycleFixture.Create();
        var transcriptPath = Path.Combine(fixture.Root, "actual-transcript.jsonl");
        const string transcript = "{\"role\":\"user\",\"text\":\"actual\"}\n";
        File.WriteAllText(transcriptPath, transcript, Encoding.UTF8);

        var result = await RunAsync(
            fixture,
            Event("SessionEnd", "end/session", transcriptPath));

        var copiedPath = Path.Combine(
            fixture.Root,
            "feedback",
            "transcripts",
            "end_session.jsonl");
        Assert.True(File.Exists(copiedPath), copiedPath);
        Assert.Equal(transcript, File.ReadAllText(copiedPath));
        Assert.Null(result);
    }

    [Fact]
    public async Task SessionEnd_WithMissingTranscriptIsANoOp()
    {
        using var fixture = LifecycleFixture.Create();
        var missingTranscript = Path.Combine(fixture.Root, "does-not-exist.jsonl");

        var result = await RunAsync(
            fixture,
            Event("SessionEnd", "missing-transcript", missingTranscript));

        Assert.Null(result);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "feedback", "transcripts")));
    }

    private static JsonObject Event(
        string kind,
        string session,
        string? transcriptPath = null,
        string? lastAssistantMessage = null)
    {
        var input = new JsonObject
        {
            ["hook_event_name"] = kind,
            ["session_id"] = session,
        };

        if (transcriptPath is not null)
            input["transcript_path"] = transcriptPath;
        if (lastAssistantMessage is not null)
            input["last_assistant_message"] = lastAssistantMessage;

        return input;
    }

    private static Task<JsonObject?> RunAsync(LifecycleFixture fixture, JsonObject input)
        => LifecycleWorkflow.RunAsync(new ToolContext(fixture.Root), input);

    private sealed class LifecycleFixture : IDisposable
    {
        private LifecycleFixture(string root)
        {
            Root = root;
            Directory.CreateDirectory(root);
        }

        public string Root { get; }

        public static LifecycleFixture Create()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "spire-devtools-lifecycle-tests",
                Guid.NewGuid().ToString("N"));
            return new LifecycleFixture(root);
        }

        public async Task InitializeGitRepositoryAsync()
        {
            await GitAsync("init", "-q");
            await GitAsync("config", "user.email", "spire-tests@example.invalid");
            await GitAsync("config", "user.name", "Spire Lifecycle Tests");
            File.WriteAllText(
                Path.Combine(Root, "README.md"),
                "isolated lifecycle fixture\n",
                Encoding.UTF8);
            await GitAsync("add", "README.md");
            await GitAsync("commit", "-qm", "initial fixture");
        }

        public async Task GitAsync(params string[] arguments)
        {
            var result = await RunProcessAsync("git", Root, arguments);
            Assert.False(
                result.TimedOut,
                $"git {string.Join(" ", arguments)} timed out.\n{result.CombinedOutput}");
            Assert.Equal(
                0,
                result.ExitCode);
        }

        public void Dispose()
        {
            TestDirectories.DeleteOwned(Root);
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string executable,
        string workingDirectory,
        params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment =
                {
                    ["CI"] = "true",
                    ["GIT_TERMINAL_PROMPT"] = "0",
                },
            },
        };

        foreach (var argument in arguments)
            DevTools.ProcessCompatibility.AddArgument(process.StartInfo, argument);

        Assert.True(process.Start(), $"Could not start '{executable}'.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
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
            await output,
            await error,
            timedOut);
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
