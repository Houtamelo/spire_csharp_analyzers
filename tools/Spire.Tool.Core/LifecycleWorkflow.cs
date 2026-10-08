using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Spire.Tool;

public static class LifecycleWorkflow
{
    public static async Task<JsonObject?> RunAsync(ToolContext context, JsonObject input)
    {
        var kind = input["hook_event_name"]?.GetValue<string>();
        var sessionId = input["session_id"]?.ToString();
        var session = Regex.Replace(string.IsNullOrEmpty(sessionId) ? "unknown" : sessionId, "[^a-zA-Z0-9_-]", "_");
        var transcript = input["transcript_path"]?.ToString();
        var timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        if (kind == "SessionStart")
        {
            var review = context.PathOf("session-reviews/" + session + ".md");
            if (!File.Exists(review)) FileOps.Write(review, "# Session Review\n\n- **Session ID**: " + session + "\n- **Transcript**: " +
                (string.IsNullOrEmpty(transcript) ? "unavailable" : transcript) + "\n- **Started at**: " + timestamp + "\n");
            return new JsonObject { ["hookSpecificOutput"] = new JsonObject { ["hookEventName"] = "SessionStart", ["additionalContext"] = "Session reflection file: " + review } };
        }
        if (kind == "SessionEnd")
        {
            if (!string.IsNullOrEmpty(transcript) && File.Exists(transcript))
            {
                var target = context.PathOf("feedback/transcripts/" + session + ".jsonl");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(transcript, target, true);
            }
            return null;
        }
        if (kind != "Stop") return null;
        var message = input["last_assistant_message"]?.ToString();
        FileOps.Write(context.PathOf("feedback/summaries/" + session + ".md"), "# Codex Session Summary\n\n**Session ID**: " + session +
            "\n**Updated at**: " + timestamp + "\n\n## Last assistant message\n\n" +
            (string.IsNullOrEmpty(message) ? "_No final assistant message available._" : message) + "\n");
        try
        {
            var status = await ProcessRunner.RunAsync("git", new[] { "status", "--porcelain", "--untracked-files=all", "-z" }, context.Root, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            if (status.ExitCode != 0) return Block("Could not inspect changed files with git: " + Tail(status.StandardError, 600));
            var changed = status.StandardOutput.Split('\0').Where(value => value.Length >= 3).Select(value => value.Substring(3))
                .Where(name => (name.StartsWith("src/", StringComparison.Ordinal) || name.StartsWith("tests/", StringComparison.Ordinal) || name.StartsWith("tools/", StringComparison.Ordinal))
                    && name.EndsWith(".cs", StringComparison.Ordinal) && File.Exists(context.PathOf(name))).ToArray();
            if (changed.Length == 0) return new JsonObject { ["continue"] = true };
            var formatted = await ProcessRunner.RunAsync(context.Dotnet, new[] { "format", "Spire.Analyzers.slnx", "--include" }.Concat(changed)
                .Concat(new[] { "--no-restore", "--verbosity", "quiet" }), context.Root, TimeSpan.FromSeconds(180), ownProcessGroup: true).ConfigureAwait(false);
            if (formatted.ExitCode != 0) return Block("C# formatting failed: " + Tail(formatted.StandardOutput + formatted.StandardError, 1200));
            foreach (var project in new[] { "tests/Houtamelo.Spire.SourceGenerators.Tests/", "tests/Houtamelo.Spire.Analyzers.Tests/", "tests/DevTools.Tests/" })
            {
                var test = await ProcessRunner.RunAsync(context.Dotnet, new[] { "test", project, "--nologo" }, context.Root, TimeSpan.FromSeconds(480), ownProcessGroup: true).ConfigureAwait(false);
                if (test.ExitCode != 0) return Block(project + " tests failed. Fix and rerun.\n" + Tail(test.StandardOutput + test.StandardError, 1800));
            }
            return new JsonObject { ["continue"] = true };
        }
        catch (Exception error) when (error is TimeoutException || error is IOException || error is System.ComponentModel.Win32Exception)
        { return Block("Could not finish C# verification: " + error.Message); }
    }
    private static JsonObject Block(string reason) => new() { ["decision"] = "block", ["reason"] = reason };
    private static string Tail(string text, int maximum) => text.Substring(Math.Max(0, text.Length - maximum));
}
