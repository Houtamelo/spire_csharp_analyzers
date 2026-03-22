using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;

namespace DevTools;

[McpServerToolType]
public static class GitTool
{
    static readonly HashSet<string> AllowedCommands = ["log", "diff", "status", "blame", "show", "rev-parse"];

    [McpServerTool(Name = "git_query")]
    [Description("Run read-only git commands (log, diff, status, blame, show, rev-parse). Rejects write operations (push, commit, reset, checkout, etc.).")]
    public static string GitQuery(
        [Description("Git subcommand (e.g. 'log', 'diff', 'status', 'blame')")] string command,
        [Description("Arguments to pass after the subcommand (e.g. '--oneline -10', '--stat HEAD~3..HEAD')")] string? arguments = null)
    {
        if (!AllowedCommands.Contains(command))
            return $"Error: '{command}' is not allowed. Only read-only commands: {string.Join(", ", AllowedCommands)}";

        var args = command;
        if (!string.IsNullOrWhiteSpace(arguments))
            args += $" {arguments}";

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(TimeSpan.FromSeconds(30));

        var result = "";
        if (!string.IsNullOrWhiteSpace(stdout))
            result += stdout;
        if (!string.IsNullOrWhiteSpace(stderr))
            result += (result.Length > 0 ? "\n" : "") + stderr;
        if (process.ExitCode != 0)
            result += $"\n\nExit code: {process.ExitCode}";
        return string.IsNullOrWhiteSpace(result) ? "(no output)" : result;
    }
}
