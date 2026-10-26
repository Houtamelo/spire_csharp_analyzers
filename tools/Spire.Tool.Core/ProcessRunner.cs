using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

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
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(1);

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
        var clock = Stopwatch.StartNew();
        using var log = CreateLog(logFile);
        using var pumpCancellation = new CancellationTokenSource();
        var cancellationSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        StreamReader? stdoutReader = null;
        StreamReader? stderrReader = null;
        Task<string>? stdout = null;
        Task<string>? stderr = null;
        Task? completion = null;
        var completedSuccessfully = false;
        try
        {
            stdoutReader = process.StandardOutput;
            stderrReader = process.StandardError;

            // Run each pump away from a caller's synchronization context. Apart from keeping
            // Unity's main thread available, this prevents a hot pipe from completing reads
            // inline indefinitely before the deadline observer gets to run.
            stdout = Task.Run(() => PumpAsync(stdoutReader, log, pumpCancellation.Token, cancellationSignal.Task));
            stderr = Task.Run(() => PumpAsync(stderrReader, log, pumpCancellation.Token, cancellationSignal.Task));
            var processExit = DevTools.ProcessCompatibility.WaitForExitAsync(process);
            completion = Task.WhenAll(processExit, stdout, stderr);

            if (!await WaitForCompletionAsync(completion, clock, timeout).ConfigureAwait(false))
                throw new TimeoutException(
                    "Process timed out after " + timeout.TotalSeconds + " seconds: " + process.StartInfo.FileName);

            await completion.ConfigureAwait(false);
            var result = new ProcessResult(
                process.ExitCode,
                await stdout.ConfigureAwait(false),
                await stderr.ConfigureAwait(false));
            completedSuccessfully = true;
            return result;
        }
        finally
        {
            if (!completedSuccessfully)
                await StopAndObserveAsync(
                    process, completion, stdoutReader, stderrReader, pumpCancellation, cancellationSignal, log, ownProcessGroup).ConfigureAwait(false);
        }
    }

    private static async Task<bool> WaitForCompletionAsync(Task completion, Stopwatch clock, TimeSpan timeout)
    {
        while (true)
        {
            var remaining = timeout - clock.Elapsed;
            if (remaining <= TimeSpan.Zero)
                return false;
            if (completion.IsCompleted)
                return true;

            var delay = remaining < PollInterval ? remaining : PollInterval;
            var winner = await Task.WhenAny(completion, Task.Delay(delay)).ConfigureAwait(false);
            if (winner == completion)
                return clock.Elapsed < timeout;
        }
    }

    private static OutputLog? CreateLog(string? logFile)
    {
        if (logFile == null)
            return null;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logFile))!);
        return new OutputLog(new StreamWriter(logFile, false, FileOps.Utf8) { AutoFlush = true });
    }

    private static async Task StopAndObserveAsync(
        Process process,
        Task? completion,
        StreamReader? stdoutReader,
        StreamReader? stderrReader,
        CancellationTokenSource pumpCancellation,
        TaskCompletionSource<bool> cancellationSignal,
        OutputLog? log,
        bool ownProcessGroup)
    {
        log?.StopWrites();
        pumpCancellation.Cancel();
        cancellationSignal.TrySetResult(true);
        TryKill(process, ownProcessGroup);
        DisposeReader(stdoutReader);
        DisposeReader(stderrReader);
        if (completion != null)
            await ObserveWithinAsync(completion, CleanupTimeout).ConfigureAwait(false);
    }

    private static async Task<string> PumpAsync(
        StreamReader reader,
        OutputLog? log,
        CancellationToken cancellationToken,
        Task cancellationSignal)
    {
        var text = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested)
        {
            Task<string?> pendingRead;
            try
            {
                // StreamReader.ReadLineAsync has no portable cancellation overload on all
                // hosts supported by this assembly. Race it against our shared signal instead
                // of relying on cancellation-token support in Mono's StreamReader.
                pendingRead = reader.ReadLineAsync();
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var winner = await Task.WhenAny(pendingRead, cancellationSignal).ConfigureAwait(false);
            if (winner != pendingRead)
            {
                ObserveFault(pendingRead);
                break;
            }

            string? line;
            try
            {
                line = await pendingRead.ConfigureAwait(false);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (line == null || cancellationToken.IsCancellationRequested)
                break;

            text.AppendLine(line);
            log?.WriteLine(line);
        }

        return text.ToString();
    }

    private static async Task ObserveWithinAsync(Task task, TimeSpan timeout)
    {
        var winner = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        if (winner == task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Cleanup failures must be observed without replacing the public result.
            }
        }
        else
        {
            ObserveFault(task);
        }
    }

    private static void ObserveFault(Task task)
    {
        if (task.IsCompleted)
        {
            _ = task.Exception;
            return;
        }

        _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void DisposeReader(StreamReader? reader)
    {
        if (reader == null)
            return;

        try
        {
            reader.Dispose();
        }
        catch (Exception)
        {
            // Reader closure is best-effort; the operation is already being torn down.
        }
    }

    private static void TryKill(Process process, bool ownProcessGroup)
    {
        try
        {
            Kill(process, ownProcessGroup);
        }
        catch (Exception)
        {
            // A process can exit between the group/tree kill checks.
        }
    }

    private sealed class OutputLog : IDisposable
    {
        private readonly object gate = new();
        private TextWriter? writer;
        private bool writesStopped;

        public OutputLog(TextWriter writer)
        {
            this.writer = writer;
        }

        public void WriteLine(string value)
        {
            lock (gate)
            {
                if (!writesStopped)
                    writer?.WriteLine(value);
            }
        }

        public void StopWrites()
        {
            lock (gate)
                writesStopped = true;
        }

        public void Dispose()
        {
            lock (gate)
            {
                writesStopped = true;
                var current = writer;
                writer = null;
                current?.Dispose();
            }
        }
    }

    public static void Kill(Process process, bool ownProcessGroup = false)
    {
        if (ownProcessGroup && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) KillGroup(-process.Id, 9);
        if (!process.HasExited) DevTools.ProcessCompatibility.KillTree(process);
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int KillGroup(int processGroup, int signal);
}
