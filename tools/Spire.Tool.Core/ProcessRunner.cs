using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Spire.Tool;

public sealed class ProcessResult
{
    public ProcessResult(int exitCode, string standardOutput, string standardError)
    { ExitCode = exitCode; StandardOutput = standardOutput; StandardError = standardError; }
    public int ExitCode { get; }
    public string StandardOutput { get; }
    public string StandardError { get; }
    public void EnsureSuccess(string action)
    {
        if (ExitCode != 0) throw new InvalidOperationException(action + " failed (exit " + ExitCode + ").\n" + StandardOutput + StandardError);
    }
}

public static class ProcessRunner
{
    public static Process Start(string executable, IEnumerable<string> arguments, string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment = null, bool ownProcessGroup = false)
    {
        var grouped = ownProcessGroup && RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        var info = new ProcessStartInfo(grouped ? "setsid" : executable)
        { WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Environment["CI"] = "true";
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        info.Environment["NUGET_HTTP_CACHE_PATH"] = Environment.GetEnvironmentVariable("NUGET_HTTP_CACHE_PATH")
            ?? Path.Combine(Path.GetTempPath(), "spire-nuget-http-cache");
        if (environment != null)
            foreach (var value in environment)
                if (value.Value == null) info.Environment.Remove(value.Key);
                else info.Environment[value.Key] = value.Value;
        if (grouped) DevTools.ProcessCompatibility.AddArgument(info, executable);
        foreach (var argument in arguments) DevTools.ProcessCompatibility.AddArgument(info, argument);
        return Process.Start(info) ?? throw new InvalidOperationException("Could not start " + executable);
    }

    public static async Task<ProcessResult> RunAsync(string executable, IEnumerable<string> arguments,
        string workingDirectory, TimeSpan timeout, IReadOnlyDictionary<string, string?>? environment = null,
        string? logFile = null, bool ownProcessGroup = false)
    {
        using var process = Start(executable, arguments, workingDirectory, environment, ownProcessGroup);
        return await CompleteAsync(process, timeout, logFile, ownProcessGroup).ConfigureAwait(false);
    }

    public static async Task<ProcessResult> CompleteAsync(Process process, TimeSpan timeout, string? logFile = null, bool ownProcessGroup = false)
    {
        TextWriter? log = null;
        if (logFile != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logFile))!);
            log = TextWriter.Synchronized(new StreamWriter(logFile, false, FileOps.Utf8) { AutoFlush = true });
        }
        var stdout = PumpAsync(process.StandardOutput, log);
        var stderr = PumpAsync(process.StandardError, log);
        var timedOut = false;
        try
        {
            var clock = Stopwatch.StartNew();
            while (!process.HasExited)
            {
                if (clock.Elapsed >= timeout) { timedOut = true; Kill(process, ownProcessGroup); break; }
                await Task.Delay(10).ConfigureAwait(false);
            }
            await DevTools.ProcessCompatibility.WaitForExitAsync(process).ConfigureAwait(false);
            var result = new ProcessResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
            if (timedOut) throw new TimeoutException("Process timed out after " + timeout.TotalSeconds + " seconds: " + process.StartInfo.FileName);
            return result;
        }
        finally
        {
            if (!process.HasExited) Kill(process, ownProcessGroup);
            log?.Dispose();
        }
    }

    private static async Task<string> PumpAsync(StreamReader reader, TextWriter? log)
    {
        var text = new StringBuilder();
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
        { text.AppendLine(line); log?.WriteLine(line); }
        return text.ToString();
    }

    public static void Kill(Process process, bool ownProcessGroup = false)
    {
        if (ownProcessGroup && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) KillGroup(-process.Id, 9);
        if (!process.HasExited) DevTools.ProcessCompatibility.KillTree(process);
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int KillGroup(int processGroup, int signal);
}
