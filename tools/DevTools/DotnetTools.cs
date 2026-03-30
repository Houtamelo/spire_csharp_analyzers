using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using ModelContextProtocol.Server;

namespace DevTools;

[McpServerToolType]
public static class DotnetTools
{
    static readonly string RepoRoot;

    static DotnetTools()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "rev-parse --show-toplevel",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi)!;
        RepoRoot = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit(TimeSpan.FromSeconds(5));
        if (process.ExitCode != 0 || string.IsNullOrEmpty(RepoRoot))
            throw new InvalidOperationException("Failed to resolve git repo root.");
    }

    static (int exitCode, string output) RunDotnet(string arguments, int timeoutSeconds)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)!;

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(TimeSpan.FromSeconds(timeoutSeconds)))
        {
            process.Kill(entireProcessTree: true);
            return (-1, $"Process timed out after {timeoutSeconds}s.\n\nStdout:\n{stdout}\n\nStderr:\n{stderr}");
        }

        var output = new StringBuilder();
        if (stdout.Length > 0)
            output.Append(stdout);
        if (stderr.Length > 0)
        {
            if (output.Length > 0) output.AppendLine();
            output.Append(stderr);
        }

        return (process.ExitCode, output.ToString());
    }

    [McpServerTool(Name = "dotnet_build")]
    [Description("Build the solution or a specific project. Returns build output including errors and warnings.")]
    public static string DotnetBuild(
        [Description("Project or solution path relative to repo root. Empty = build entire repo.")] string project = "",
        [Description("Build configuration (Debug or Release)")] string configuration = "Debug",
        [Description("Timeout in seconds")] int timeout = 120)
    {
        var args = new StringBuilder("build");

        if (!string.IsNullOrEmpty(project))
            args.Append($" \"{project}\"");

        args.Append($" -c {configuration}");
        args.Append(" --no-restore");

        var (exitCode, output) = RunDotnet(args.ToString(), timeout);

        return $"Exit code: {exitCode}\n\n{output}";
    }

    [McpServerTool(Name = "dotnet_test")]
    [Description("Run tests for the solution or a specific test project. Returns test results including pass/fail counts and failure details.")]
    public static string DotnetTest(
        [Description("Test project path relative to repo root. Empty = run all tests.")] string project = "",
        [Description("Filter expression (e.g. 'FullyQualifiedName~SPIRE001'). Empty = run all.")] string filter = "",
        [Description("Build configuration (Debug or Release)")] string configuration = "Debug",
        [Description("Timeout in seconds")] int timeout = 300)
    {
        var args = new StringBuilder("test");

        if (!string.IsNullOrEmpty(project))
            args.Append($" \"{project}\"");

        args.Append($" -c {configuration}");

        if (!string.IsNullOrEmpty(filter))
            args.Append($" --filter \"{filter}\"");

        args.Append(" --no-restore");

        var (exitCode, output) = RunDotnet(args.ToString(), timeout);

        return $"Exit code: {exitCode}\n\n{output}";
    }

    [McpServerTool(Name = "dotnet_restore")]
    [Description("Restore NuGet packages for the solution or a specific project.")]
    public static string DotnetRestore(
        [Description("Project or solution path relative to repo root. Empty = restore entire repo.")] string project = "",
        [Description("Timeout in seconds")] int timeout = 120)
    {
        var args = new StringBuilder("restore");

        if (!string.IsNullOrEmpty(project))
            args.Append($" \"{project}\"");

        var (exitCode, output) = RunDotnet(args.ToString(), timeout);

        return $"Exit code: {exitCode}\n\n{output}";
    }
}
