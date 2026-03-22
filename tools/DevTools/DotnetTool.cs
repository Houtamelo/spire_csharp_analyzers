using System;
using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;

namespace DevTools;

[McpServerToolType]
public static class DotnetTool
{
    [McpServerTool(Name = "dotnet_build")]
    [Description("Run 'dotnet build' on a project or solution. Returns stdout and stderr. Use to verify compilation after writing code.")]
    public static string DotnetBuild(
        [Description("Project or solution path relative to repo root (e.g. 'src/Spire.Analyzers/Spire.Analyzers.csproj'). Omit to build entire solution.")] string? project = null)
    {
        var args = "build --verbosity quiet";
        if (!string.IsNullOrWhiteSpace(project))
            args += $" {project}";
        return RunDotnet(args);
    }

    [McpServerTool(Name = "dotnet_test")]
    [Description("Run 'dotnet test' on a project or solution. Returns stdout and stderr. Use to verify tests pass after implementation.")]
    public static string DotnetTest(
        [Description("Project path relative to repo root (e.g. 'tests/Spire.Analyzers.Tests/'). Omit to test entire solution.")] string? project = null,
        [Description("Test filter expression (e.g. 'FullyQualifiedName~SPIRE001'). Omit to run all tests.")] string? filter = null)
    {
        var args = "test --verbosity quiet";
        if (!string.IsNullOrWhiteSpace(project))
            args += $" {project}";
        if (!string.IsNullOrWhiteSpace(filter))
            args += $" --filter \"{filter}\"";
        return RunDotnet(args);
    }

    static string RunDotnet(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(TimeSpan.FromMinutes(5));

        var result = "";
        if (!string.IsNullOrWhiteSpace(stdout))
            result += stdout;
        if (!string.IsNullOrWhiteSpace(stderr))
            result += (result.Length > 0 ? "\n" : "") + stderr;
        if (process.ExitCode != 0)
            result += $"\n\nExit code: {process.ExitCode}";
        return string.IsNullOrWhiteSpace(result) ? "Build succeeded." : result;
    }
}
