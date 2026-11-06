using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

public sealed class VirtualDisplayTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ServerObservationTimeout = TimeSpan.FromSeconds(5);

    [LinuxFact]
    public async Task RunAsync_ProvidesAWorkingDisplayThenReturnsTheCallbackResultAndCleansUp()
    {
        using var fixture = VirtualDisplayFixture.Create();
        OwnedDisplay? ownedDisplay = null;

        var result = await InvokeRunAsync(
            fixture.Root,
            fixture.ErrorLog,
            async display =>
            {
                ownedDisplay = await CaptureDisplayAsync(fixture.Root, display);
                return "callback-result";
            });

        Assert.True(File.Exists(fixture.ErrorLog), fixture.ErrorLog);
        Assert.Equal("callback-result", result);
        Assert.NotNull(ownedDisplay);
        await AssertServerExitedAsync(ownedDisplay!);
    }

    [LinuxFact]
    public async Task RunAsync_PropagatesTheCallbackExceptionAndCleansUp()
    {
        using var fixture = VirtualDisplayFixture.Create();
        OwnedDisplay? ownedDisplay = null;
        var sentinel = new InvalidOperationException("callback sentinel");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InvokeRunAsync(
                fixture.Root,
                fixture.ErrorLog,
                async display =>
                {
                    ownedDisplay = await CaptureDisplayAsync(fixture.Root, display);
                    return await Task.FromException<string>(sentinel).ConfigureAwait(false);
                }));

        Assert.True(File.Exists(fixture.ErrorLog), fixture.ErrorLog);
        Assert.Same(sentinel, exception);
        Assert.NotNull(ownedDisplay);
        await AssertServerExitedAsync(ownedDisplay!);
    }

    [LinuxFact]
    public async Task RunAsync_PreservesARealProcessRunnerResultAndCleansUp()
    {
        using var fixture = VirtualDisplayFixture.Create();
        OwnedDisplay? ownedDisplay = null;

        var result = await InvokeRunAsync(
            fixture.Root,
            fixture.ErrorLog,
            async display =>
            {
                ownedDisplay = await CaptureDisplayAsync(fixture.Root, display);
                return await ProcessRunner.RunAsync(
                    "/bin/sh",
                    new[] { "-c", "exit 37" },
                    fixture.Root,
                    ProcessTimeout).ConfigureAwait(false);
            });

        Assert.True(File.Exists(fixture.ErrorLog), fixture.ErrorLog);
        Assert.Equal(37, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
        Assert.NotNull(ownedDisplay);
        await AssertServerExitedAsync(ownedDisplay!);
    }

    [LinuxFact]
    public async Task RunAsync_PropagatesCallbackCancellationAndCleansUp()
    {
        using var fixture = VirtualDisplayFixture.Create();
        OwnedDisplay? ownedDisplay = null;
        using var cancellation = new CancellationTokenSource();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await InvokeRunAsync<int>(
                fixture.Root,
                fixture.ErrorLog,
                async display =>
                {
                    ownedDisplay = await CaptureDisplayAsync(fixture.Root, display);
                    cancellation.Cancel();
                    return await Task.FromCanceled<int>(cancellation.Token).ConfigureAwait(false);
                }));

        Assert.True(File.Exists(fixture.ErrorLog), fixture.ErrorLog);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.NotNull(ownedDisplay);
        await AssertServerExitedAsync(ownedDisplay!);
    }

    private static async Task<T> InvokeRunAsync<T>(
        string workingDirectory,
        string? errorLog,
        Func<string, Task<T>> action)
    {
        var virtualDisplayType = typeof(ToolContext).Assembly.GetType("Spire.Tool.VirtualDisplay");
        Assert.NotNull(virtualDisplayType);

        var runMethod = virtualDisplayType!.GetMethod(
            "RunAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(runMethod);
        Assert.True(runMethod!.IsGenericMethodDefinition);
        Assert.Single(runMethod.GetGenericArguments());
        Assert.Equal(3, runMethod.GetParameters().Length);

        var closedRunMethod = runMethod.MakeGenericMethod(typeof(T));
        var task = (Task<T>)closedRunMethod.Invoke(
            obj: null,
            parameters: new object?[] { workingDirectory, errorLog, action })!;
        return await task.ConfigureAwait(false);
    }

    private static async Task<OwnedDisplay> CaptureDisplayAsync(string workingDirectory, string display)
    {
        ParseDisplayNumber(display);
        var processId = await WaitForOwnedXvfbPidAsync(workingDirectory).ConfigureAwait(false);
        Assert.True(IsProcessRunning(processId), $"Xvfb process {processId} was not running inside the callback.");

        var result = await ProcessRunner.RunAsync(
            "xdpyinfo",
            new[] { "-display", display },
            workingDirectory,
            ProcessTimeout).ConfigureAwait(false);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(display, result.StandardOutput, StringComparison.Ordinal);

        return new OwnedDisplay(display, processId);
    }

    private static int ParseDisplayNumber(string display)
    {
        var value = display.Trim();
        Assert.StartsWith(":", value, StringComparison.Ordinal);
        Assert.True(
            int.TryParse(value.Substring(1), out var displayNumber) && displayNumber >= 0,
            $"VirtualDisplay supplied an invalid X display name: '{display}'.");
        return displayNumber;
    }

    private static async Task<int> WaitForOwnedXvfbPidAsync(string workingDirectory)
    {
        var processId = 0;
        var found = await WaitUntilAsync(
            () => TryFindOwnedXvfbPid(workingDirectory, out processId),
            ServerObservationTimeout).ConfigureAwait(false);
        Assert.True(
            found,
            $"Xvfb did not start with working directory '{workingDirectory}' within {ServerObservationTimeout}.");
        return processId;
    }

    private static async Task AssertServerExitedAsync(OwnedDisplay ownedDisplay)
    {
        var exited = await WaitUntilAsync(
            () => !IsProcessRunning(ownedDisplay.ProcessId),
            ServerObservationTimeout).ConfigureAwait(false);
        Assert.True(
            exited,
            $"The Xvfb process {ownedDisplay.ProcessId} for {ownedDisplay.Display} survived VirtualDisplay cleanup.");
    }

    private static bool TryFindOwnedXvfbPid(string workingDirectory, out int processId)
    {
        processId = 0;

        try
        {
            var expectedRoot = new ToolContext(workingDirectory).Root;
            if (TryFindOwnedXvfbPid(
                    Process.GetProcessesByName("Xvfb"),
                    expectedRoot,
                    filterByProcessName: false,
                    out processId))
                return true;

            // Unity's Mono reports the full executable path as ProcessName, so its
            // exact-name lookup misses the Xvfb process that .NET finds directly.
            return TryFindOwnedXvfbPid(
                Process.GetProcesses(),
                expectedRoot,
                filterByProcessName: true,
                out processId);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        return false;
    }

    private static bool TryFindOwnedXvfbPid(
        Process[] processes,
        string expectedRoot,
        bool filterByProcessName,
        out int processId)
    {
        processId = 0;
        foreach (var process in processes)
        {
            using (process)
            {
                if (processId != 0)
                    continue;

                try
                {
                    if (filterByProcessName && !IsXvfbProcessName(process.ProcessName))
                        continue;

                    var processWorkingDirectory = Path.Combine(
                        "/proc",
                        process.Id.ToString(),
                        "cwd");
                    var actualRoot = new ToolContext(processWorkingDirectory).Root;
                    if (string.Equals(expectedRoot, actualRoot, StringComparison.Ordinal))
                        processId = process.Id;
                }
                catch (ArgumentException)
                {
                }
                catch (InvalidOperationException)
                {
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        return processId != 0;
    }

    private static bool IsXvfbProcessName(string processName)
        => string.Equals(processName, "Xvfb", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(processName), "Xvfb", StringComparison.OrdinalIgnoreCase);

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

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools", "Spire.Tool.Core", "Spire.Tool.Core.csproj")) &&
                    File.Exists(Path.Combine(directory.FullName, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root for virtual display tests.");
    }

    private sealed class OwnedDisplay
    {
        public OwnedDisplay(string display, int processId)
        {
            Display = display;
            ProcessId = processId;
        }

        public string Display { get; }
        public int ProcessId { get; }
    }

    private sealed class VirtualDisplayFixture : IDisposable
    {
        private VirtualDisplayFixture(string root)
        {
            Root = root;
            Directory.CreateDirectory(root);
        }

        public string Root { get; }
        public string ErrorLog => Path.Combine(Root, "xvfb-error.log");

        public static VirtualDisplayFixture Create()
            => new(Path.Combine(
                FindRepositoryRoot(),
                "tmp",
                "devtools-virtual-display-tests",
                Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
