using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DevTools.Tests;

public sealed class ProcessCompatibilityTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AddArgument_PreservesSpacesQuotesAndBackslashes()
    {
        using var fixture = ProcessFixture.Create();
        var expected = new[]
        {
            "value with spaces",
            "quote\"value",
            "slash\\value",
            "mixed \\ and \"quotes\"",
        };

        using var process = StartShell(
            fixture.Root,
            "for value in \"$@\"; do printf '%s\\n' \"$value\"; done",
            "print-arguments",
            expected);
        var result = await WaitForShellAsync(process);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expected, result.StandardOutput.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task WaitForExitAsync_CancellationDoesNotLeaveTheProcessRunning()
    {
        using var fixture = ProcessFixture.Create();
        using var process = StartShell(fixture.Root, "sleep 30", "sleep");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await DevTools.ProcessCompatibility.WaitForExitAsync(process, cancellation.Token));

        try
        {
            Assert.False(process.HasExited);
            DevTools.ProcessCompatibility.KillTree(process);
            await DevTools.ProcessCompatibility.WaitForExitAsync(process);
            Assert.True(process.HasExited);
        }
        finally
        {
            if (!process.HasExited)
                DevTools.ProcessCompatibility.KillTree(process);
        }
    }

    [Fact]
    public async Task KillTree_StopsAChildProcessToo()
    {
        using var fixture = ProcessFixture.Create();
        var childPidFile = Path.Combine(fixture.Root, "child.pid");
        using var process = StartShell(
            fixture.Root,
            "sleep 30 & child=$!; printf '%s' \"$child\" > \"$1\"; wait \"$child\"",
            "spawn-child",
            childPidFile);
        try
        {
            var childPid = await WaitForChildPidAsync(childPidFile);
            Assert.True(IsProcessRunning(childPid), $"Child process {childPid} exited before the tree was killed.");

            DevTools.ProcessCompatibility.KillTree(process);
            await DevTools.ProcessCompatibility.WaitForExitAsync(process);
            Assert.True(process.HasExited);
            Assert.True(
                await WaitUntilAsync(() => !IsProcessRunning(childPid), TimeSpan.FromSeconds(5)),
                $"Child process {childPid} survived KillTree.");
        }
        finally
        {
            if (!process.HasExited)
                DevTools.ProcessCompatibility.KillTree(process);
        }
    }

    [Fact]
    public void HashData_HashesARepositoryFileWithSha256()
    {
        var repositoryRoot = FindRepositoryRoot();
        var license = Path.Combine(repositoryRoot, "LICENSE");
        Assert.True(File.Exists(license), license);

        var actual = DevTools.ProcessCompatibility.ToHexString(
            DevTools.ProcessCompatibility.HashData(File.ReadAllBytes(license)));

        Assert.Equal(
            "2A573CDA76C2EBCC19D1663B954631D623E8374919101D7485EC2173696DB3C3",
            actual);
    }

    private static Process StartShell(string workingDirectory, string command, string zero, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
        };
        DevTools.ProcessCompatibility.AddArgument(info, "-c");
        DevTools.ProcessCompatibility.AddArgument(info, command);
        DevTools.ProcessCompatibility.AddArgument(info, zero);
        foreach (var argument in arguments)
            DevTools.ProcessCompatibility.AddArgument(info, argument);

        var process = new Process { StartInfo = info };
        Assert.True(process.Start(), "Could not start /bin/sh.");
        return process;
    }

    private static async Task<ProcessResult> WaitForShellAsync(Process process)
    {
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(ProcessTimeout);
        try
        {
            await DevTools.ProcessCompatibility.WaitForExitAsync(process, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!process.HasExited)
                DevTools.ProcessCompatibility.KillTree(process);
            await DevTools.ProcessCompatibility.WaitForExitAsync(process);
        }

        return new ProcessResult(process.ExitCode, await output, await error);
    }

    private static async Task<int> WaitForChildPidAsync(string path)
    {
        var found = await WaitUntilAsync(() => File.Exists(path), TimeSpan.FromSeconds(5));
        Assert.True(found, "The child process did not publish its PID.");
        var value = File.ReadAllText(path);
        Assert.True(int.TryParse(value, out var pid), value);
        return pid;
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                return false;
            await Task.Delay(25);
        }

        return true;
    }

    private static bool IsProcessRunning(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
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

        throw new InvalidOperationException("Could not locate the repository root for process compatibility tests.");
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
            => new(Path.Combine(FindRepositoryRoot(), "tmp", "devtools-process-tests", Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
