using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class ProcessRunnerCorrectnessTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task RunAsync_ReturnsExitCodeAndCapturesBothStreams()
    {
        using var fixture = ProcessFixture.Create();

        var result = await ProcessRunner.RunAsync(
            Shell,
            ShellArguments(
                "printf 'stdout-line\\n'; printf 'stderr-line\\n' >&2; exit 37",
                windowsCommand: "echo stdout-line & echo stderr-line 1>&2 & exit /b 37"),
            fixture.Root,
            TimeSpan.FromSeconds(5));

        Assert.Equal(37, result.ExitCode);
        Assert.Equal("stdout-line" + Environment.NewLine, result.StandardOutput);
        Assert.Equal("stderr-line" + Environment.NewLine, result.StandardError);
    }

    [Fact]
    public async Task CompleteAsync_CapturesPartialLinesAndFlushesTheCombinedLog()
    {
        using var fixture = ProcessFixture.Create();
        var logPath = Path.Combine(fixture.Root, "process.log");
        using var process = ProcessRunner.Start(
            Shell,
            ShellArguments(
                "printf 'partial-stdout'; printf 'partial-stderr' >&2; exit 0",
                windowsCommand: "set /p =partial-stdout<nul & set /p =partial-stderr 1>&2<nul & exit /b 0"),
            fixture.Root);

        var result = await ProcessRunner.CompleteAsync(
            process,
            TimeSpan.FromSeconds(5),
            logPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("partial-stdout" + Environment.NewLine, result.StandardOutput);
        Assert.Equal("partial-stderr" + Environment.NewLine, result.StandardError);
        var log = File.ReadAllText(logPath);
        Assert.Contains("partial-stdout", log, StringComparison.Ordinal);
        Assert.Contains("partial-stderr", log, StringComparison.Ordinal);
        Assert.True(File.Exists(logPath));
    }

    [Fact]
    public async Task RunAsync_SuccessWithNoOutputReturnsEmptyStreams()
    {
        using var fixture = ProcessFixture.Create();

        var result = await ProcessRunner.RunAsync(
            Shell,
            ShellArguments("exit 0", windowsCommand: "exit /b 0"),
            fixture.Root,
            TimeSpan.FromSeconds(5));

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public async Task CompleteAsync_HotStdoutStillHonorsTheDeadline()
    {
        RequireLinuxProcessFixtures();
        using var fixture = ProcessFixture.Create();
        using var process = ProcessRunner.Start(
            Shell,
            ShellArguments("while :; do printf 'hot-stdout\\n'; done"),
            fixture.Root,
            ownProcessGroup: true);

        var completion = ProcessRunner.CompleteAsync(
            process,
            TimeSpan.FromMilliseconds(100),
            ownProcessGroup: true);
        try
        {
            var winner = await Task.WhenAny(completion, Task.Delay(ObservationTimeout));
            Assert.Same(completion, winner);
            var exception = await Record.ExceptionAsync(async () => await completion.ConfigureAwait(false));
            Assert.IsType<TimeoutException>(exception);
        }
        finally
        {
            ProcessRunner.Kill(process, ownProcessGroup: true);
            await ObserveCompletionAsync(completion);
        }
    }

    [Fact]
    public async Task CompleteAsync_TimeoutCoversStdoutHeldByExitedParent()
    {
        await AssertTimeoutWhenExitedParentKeepsPipeOpenAsync(
            "sleep 30 2>/dev/null & child=$!; printf '%s' \"$child\" > \"$1\"; exit 0",
            "stdout");
    }

    [Fact]
    public async Task CompleteAsync_TimeoutCoversStderrHeldByExitedParent()
    {
        await AssertTimeoutWhenExitedParentKeepsPipeOpenAsync(
            "sleep 30 1>/dev/null & child=$!; printf '%s' \"$child\" > \"$1\"; exit 0",
            "stderr");
    }

    [Fact]
    public async Task CompleteAsync_TimeoutCoversBothPipesHeldByExitedParent()
    {
        await AssertTimeoutWhenExitedParentKeepsPipeOpenAsync(
            "sleep 30 & child=$!; printf '%s' \"$child\" > \"$1\"; exit 0",
            "both");
    }

    [Fact]
    public async Task CompleteAsync_TimeoutKillsAnOwnedLiveProcessGroup()
    {
        RequireLinuxProcessFixtures();
        using var fixture = ProcessFixture.Create();
        var pidPath = Path.Combine(fixture.Root, "child.pid");
        using var process = ProcessRunner.Start(
            Shell,
            ShellArguments("sleep 30 & child=$!; printf '%s' \"$child\" > \"$1\"; wait \"$child\"", pidPath),
            fixture.Root,
            ownProcessGroup: true);

        var completion = ProcessRunner.CompleteAsync(
            process,
            ProcessTimeout,
            ownProcessGroup: true);
        try
        {
            var childPid = await WaitForPidAsync(pidPath);
            var winner = await Task.WhenAny(completion, Task.Delay(ObservationTimeout));
            Assert.Same(completion, winner);
            var exception = await Record.ExceptionAsync(async () => await completion.ConfigureAwait(false));
            Assert.IsType<TimeoutException>(exception);
            Assert.True(
                await WaitUntilAsync(() => !IsProcessRunning(childPid), ObservationTimeout),
                $"Owned child process {childPid} survived timeout cleanup.");
        }
        finally
        {
            ProcessRunner.Kill(process, ownProcessGroup: true);
            await ObserveCompletionAsync(completion);
        }
    }

    [Fact]
    public async Task CompleteAsync_TimeoutPreservesOutputCapturedBeforeKill()
    {
        RequireLinuxProcessFixtures();
        using var fixture = ProcessFixture.Create();
        var logPath = Path.Combine(fixture.Root, "timeout.log");
        using var process = ProcessRunner.Start(
            Shell,
            ShellArguments("printf 'before-stdout\\n'; printf 'before-stderr\\n' >&2; sleep 30"),
            fixture.Root,
            ownProcessGroup: true);

        var completion = ProcessRunner.CompleteAsync(
            process,
            ProcessTimeout,
            logPath,
            ownProcessGroup: true);
        try
        {
            var winner = await Task.WhenAny(completion, Task.Delay(ObservationTimeout));
            Assert.Same(completion, winner);
            var exception = await Record.ExceptionAsync(async () => await completion.ConfigureAwait(false));
            Assert.IsType<TimeoutException>(exception);
            var log = File.ReadAllText(logPath);
            Assert.Contains("before-stdout", log, StringComparison.Ordinal);
            Assert.Contains("before-stderr", log, StringComparison.Ordinal);
        }
        finally
        {
            ProcessRunner.Kill(process, ownProcessGroup: true);
            await ObserveCompletionAsync(completion);
        }
    }

    [Fact]
    public async Task CompleteAsync_NonOwnedExitedParentStillHonorsTheDeadline()
    {
        RequireLinuxProcessFixtures();
        using var fixture = ProcessFixture.Create();
        var pidPath = Path.Combine(fixture.Root, "non-owned-child.pid");
        using var process = ProcessRunner.Start(
            Shell,
            ShellArguments(
                "sleep 30 2>/dev/null & child=$!; printf '%s' \"$child\" > \"$1\"; exit 0",
                pidPath),
            fixture.Root);

        var completion = ProcessRunner.CompleteAsync(
            process,
            ProcessTimeout,
            ownProcessGroup: false);
        var childPid = 0;
        try
        {
            childPid = await WaitForPidAsync(pidPath);
            Assert.True(
                await WaitUntilAsync(() => process.HasExited, ObservationTimeout),
                "The non-owned parent did not exit before the pipe-draining deadline.");
            var winner = await Task.WhenAny(completion, Task.Delay(ObservationTimeout));
            Assert.Same(completion, winner);
            var exception = await Record.ExceptionAsync(async () => await completion.ConfigureAwait(false));
            Assert.IsType<TimeoutException>(exception);
        }
        finally
        {
            ProcessRunner.Kill(process);
            if (childPid <= 0 && TryReadPid(pidPath, out var observedChildPid))
                childPid = observedChildPid;
            await KillProcessByIdAsync(childPid);
            await ObserveCompletionAsync(completion);
        }
    }

    [Fact]
    public async Task RunAsync_NonOwnedExitedParentHoldingStdoutTimesOutBeforeManualChildCleanup()
    {
        RequireLinuxProcessFixtures();
        using var fixture = ProcessFixture.Create();
        var pidPath = Path.Combine(fixture.Root, "run-child.pid");
        var completion = ProcessRunner.RunAsync(
            Shell,
            ShellArguments(
                "sleep 30 2>/dev/null & child=$!; printf '%s' \"$child\" > \"$1\"; exit 0",
                pidPath),
            fixture.Root,
            ProcessTimeout,
            ownProcessGroup: false);
        var childPid = 0;
        try
        {
            childPid = await WaitForPidAsync(pidPath);
            Assert.True(IsProcessRunning(childPid), $"Child process {childPid} exited before RunAsync was observed.");

            var winner = await Task.WhenAny(completion, Task.Delay(ObservationTimeout));
            Assert.Same(completion, winner);
            var exception = await Record.ExceptionAsync(async () => await completion.ConfigureAwait(false));
            Assert.IsType<TimeoutException>(exception);
        }
        finally
        {
            if (childPid <= 0 && TryReadPid(pidPath, out var observedChildPid))
                childPid = observedChildPid;
            await KillProcessByIdAsync(childPid);
            await ObserveCompletionAsync(completion);
        }
    }

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static string Shell => IsWindows ? "cmd.exe" : "/bin/sh";

    private static string[] ShellArguments(
        string unixCommand,
        string? argument = null,
        string? windowsCommand = null)
    {
        if (IsWindows)
        {
            if (windowsCommand == null)
                throw new PlatformNotSupportedException("This process fixture requires a Linux shell command.");
            return new[] { "/c", windowsCommand };
        }

        return argument == null
            ? new[] { "-c", unixCommand, "spire-process-test" }
            : new[] { "-c", unixCommand, "spire-process-test", argument };
    }

    private static async Task AssertTimeoutWhenExitedParentKeepsPipeOpenAsync(string command, string label)
    {
        RequireLinuxProcessFixtures();

        using var fixture = ProcessFixture.Create();
        var pidPath = Path.Combine(fixture.Root, label + ".pid");
        using var process = ProcessRunner.Start(
            Shell,
            ShellArguments(command, pidPath),
            fixture.Root,
            ownProcessGroup: true);

        var completion = ProcessRunner.CompleteAsync(
            process,
            ProcessTimeout,
            ownProcessGroup: true);
        var childPid = 0;
        try
        {
            childPid = await WaitForPidAsync(pidPath);
            Assert.True(
                await WaitUntilAsync(() => process.HasExited, ObservationTimeout),
                $"The exited-parent {label} fixture did not exit before the pipe-draining deadline.");
            var winner = await Task.WhenAny(completion, Task.Delay(ObservationTimeout));
            Assert.Same(completion, winner);
            var exception = await Record.ExceptionAsync(async () => await completion.ConfigureAwait(false));
            Assert.IsType<TimeoutException>(exception);
            Assert.True(
                await WaitUntilAsync(() => !IsProcessRunning(childPid), ObservationTimeout),
                $"Owned child process {childPid} survived inherited-{label}-pipe cleanup.");
        }
        finally
        {
            ProcessRunner.Kill(process, ownProcessGroup: true);
            if (childPid <= 0 && TryReadPid(pidPath, out var observedChildPid))
                childPid = observedChildPid;
            await KillProcessByIdAsync(childPid);
            await ObserveCompletionAsync(completion);
        }
    }

    private static async Task<int> WaitForPidAsync(string path)
    {
        var found = await WaitUntilAsync(
            () => TryReadPid(path, out _),
            ObservationTimeout);
        Assert.True(found, $"No valid child PID was published at '{path}'.");
        Assert.True(TryReadPid(path, out var pid));
        Assert.True(IsProcessRunning(pid), $"Child process {pid} exited before observation.");
        return pid;
    }

    private static void RequireLinuxProcessFixtures()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            throw new PlatformNotSupportedException("The process-descendant fixture requires Linux.");
    }

    private static bool TryReadPid(string path, out int pid)
    {
        pid = 0;
        try
        {
            return File.Exists(path) &&
                int.TryParse(File.ReadAllText(path).Trim(), out pid) &&
                pid > 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                return false;
            await Task.Delay(25).ConfigureAwait(false);
        }

        return true;
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task KillProcessByIdAsync(int processId)
    {
        if (processId <= 0)
            return;

        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
                process.Kill();
            var wait = DevTools.ProcessCompatibility.WaitForExitAsync(process);
            await Task.WhenAny(wait, Task.Delay(ObservationTimeout)).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task ObserveCompletionAsync(Task<ProcessResult> completion)
    {
        var winner = await Task.WhenAny(completion, Task.Delay(ObservationTimeout)).ConfigureAwait(false);
        if (ReferenceEquals(winner, completion))
        {
            try
            {
                await completion.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Cleanup observes the operation without replacing the assertion above.
            }
        }
    }

    private sealed class ProcessFixture : IDisposable
    {
        private ProcessFixture(string root)
        {
            Root = root;
            Directory.CreateDirectory(root);
        }

        public string Root { get; }

        public static ProcessFixture Create()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "spire-process-runner-" + Guid.NewGuid().ToString("N"));
            return new ProcessFixture(root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

