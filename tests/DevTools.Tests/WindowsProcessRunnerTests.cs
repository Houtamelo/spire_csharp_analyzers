using System;
using System.IO;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class WindowsProcessRunnerTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan RunAsyncTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(8);

    [WindowsFact]
    public async Task CompleteAsync_HotStdoutHonorsTheDeadlineAfterReadyOutput()
    {
        using var fixture = WindowsProcessFixture.Create();
        using var process = fixture.StartSingleScript(
            "[Console]::Out.WriteLine('hot-stdout'); [Console]::Out.Flush(); " +
            "[IO.File]::WriteAllText($ready, 'ready'); " +
            "while ($true) { [Console]::Out.WriteLine('hot-stdout'); [Console]::Out.Flush() }");
        await fixture.WaitForReadyAsync();
        var logPath = Path.Combine(fixture.Root, "hot.log");
        var completion = ProcessRunner.CompleteAsync(process, ProcessTimeout, logPath);
        try
        {
            await AssertTimeoutAsync(completion);
            Assert.Contains("hot-stdout", File.ReadAllText(logPath), StringComparison.Ordinal);
        }
        finally
        {
            ProcessRunner.Kill(process);
            await ObserveAsync(completion);
        }
    }

    [WindowsFact]
    public async Task CompleteAsync_TimeoutCoversStdoutHeldByExitedParent()
        => await AssertExitedParentKeepsPipeOpenAsync("stdout");

    [WindowsFact]
    public async Task CompleteAsync_TimeoutCoversStderrHeldByExitedParent()
        => await AssertExitedParentKeepsPipeOpenAsync("stderr");

    [WindowsFact]
    public async Task CompleteAsync_TimeoutCoversBothPipesHeldByExitedParent()
        => await AssertExitedParentKeepsPipeOpenAsync("both");

    [WindowsFact]
    public async Task CompleteAsync_TimeoutKillsAnOwnedLiveParentAndChild()
    {
        using var fixture = WindowsProcessFixture.Create();
        using var process = fixture.StartParent("live");
        await fixture.WaitForParentReadyAsync();
        var childPid = fixture.ChildPid;
        Assert.True(WindowsProcessFixture.IsRunning(childPid));
        var completion = ProcessRunner.CompleteAsync(process, ProcessTimeout);
        try
        {
            await AssertTimeoutAsync(completion);
            Assert.True(
                await fixture.WaitUntilAsync(() => !WindowsProcessFixture.IsRunning(childPid), ObservationTimeout),
                $"Owned child process {childPid} survived Windows timeout cleanup.");
        }
        finally
        {
            ProcessRunner.Kill(process);
            await fixture.KillOwnedProcessesAsync();
            await ObserveAsync(completion);
        }
    }

    [WindowsFact]
    public async Task CompleteAsync_TimeoutPreservesOutputCapturedBeforeKill()
    {
        using var fixture = WindowsProcessFixture.Create();
        using var process = fixture.StartSingleScript(
            "[Console]::Out.WriteLine('before-stdout'); [Console]::Error.WriteLine('before-stderr'); " +
            "[IO.File]::WriteAllText($ready, 'ready'); Start-Sleep -Seconds 30");
        await fixture.WaitForReadyAsync();
        var logPath = Path.Combine(fixture.Root, "timeout.log");
        var completion = ProcessRunner.CompleteAsync(process, ProcessTimeout, logPath);
        try
        {
            await AssertTimeoutAsync(completion);
            var log = File.ReadAllText(logPath);
            Assert.Contains("before-stdout", log, StringComparison.Ordinal);
            Assert.Contains("before-stderr", log, StringComparison.Ordinal);
        }
        finally
        {
            ProcessRunner.Kill(process);
            await ObserveAsync(completion);
        }
    }

    [WindowsFact]
    public async Task CompleteAsync_NonOwnedExitedParentStillHonorsTheDeadline()
        => await AssertExitedParentKeepsPipeOpenAsync("stdout");

    [WindowsFact]
    public async Task RunAsync_NonOwnedExitedParentHoldingStdoutTimesOutBeforeManualChildCleanup()
    {
        using var fixture = WindowsProcessFixture.Create();
        var completion = fixture.RunParentAsync("stdout", RunAsyncTimeout);
        await fixture.WaitForParentReadyAsync();
        var childPid = fixture.ChildPid;
        Assert.True(
            await fixture.WaitUntilAsync(fixture.ParentHasExited, ObservationTimeout),
            "The parent must exit before RunAsync waits on the inherited pipe.");
        Assert.True(WindowsProcessFixture.IsRunning(childPid));
        try
        {
            await AssertTimeoutAsync(completion);
        }
        finally
        {
            await fixture.KillOwnedProcessesAsync();
            await ObserveAsync(completion);
        }
    }

    private static async Task AssertExitedParentKeepsPipeOpenAsync(string mode)
    {
        using var fixture = WindowsProcessFixture.Create();
        using var process = fixture.StartParent(mode);
        await fixture.WaitForParentReadyAsync();
        var childPid = fixture.ChildPid;
        Assert.True(
            await fixture.WaitUntilAsync(fixture.ParentHasExited, ObservationTimeout),
            "The Windows parent did not exit after child readiness.");
        Assert.True(WindowsProcessFixture.IsRunning(childPid));
        Assert.True(
            await fixture.ReadOtherStreamToEndAsync(process, mode),
            "The non-selected parent stream did not reach EOF after parent exit.");
        var completion = ProcessRunner.CompleteAsync(process, ProcessTimeout, ownProcessGroup: false);
        try
        {
            await AssertTimeoutAsync(completion);
        }
        finally
        {
            ProcessRunner.Kill(process);
            await fixture.KillOwnedProcessesAsync();
            await ObserveAsync(completion);
        }
    }

    private static async Task AssertTimeoutAsync(Task<ProcessResult> completion)
    {
        var winner = await Task.WhenAny(completion, Task.Delay(ObservationTimeout));
        Assert.Same(completion, winner);
        var exception = await Record.ExceptionAsync(async () => await completion.ConfigureAwait(false));
        Assert.IsType<TimeoutException>(exception);
    }

    private static async Task ObserveAsync(Task<ProcessResult> completion)
    {
        var winner = await Task.WhenAny(completion, Task.Delay(ObservationTimeout));
        if (winner == completion)
        {
            try { await completion.ConfigureAwait(false); }
            catch (Exception) { }
        }
    }
}
