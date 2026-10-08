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
        var script = Path.Combine(fixture.Root, "print-arguments.py");
        File.WriteAllText(
            script,
            "import sys\nfor value in sys.argv[1:]: print(value)\n",
            Encoding.UTF8);

        var expected = new[]
        {
            "value with spaces",
            "quote\"value",
            "slash\\value",
            "mixed \\ and \"quotes\"",
        };
        var result = await RunPythonAsync(script, expected);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expected, result.StandardOutput.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task WaitForExitAsync_CancellationDoesNotLeaveTheProcessRunning()
    {
        using var fixture = ProcessFixture.Create();
        var script = Path.Combine(fixture.Root, "sleep.py");
        File.WriteAllText(script, "import time\ntime.sleep(30)\n", Encoding.UTF8);

        using var process = StartPython(script);
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
        var script = Path.Combine(fixture.Root, "spawn-child.py");
        var childPidFile = Path.Combine(fixture.Root, "child.pid");
        File.WriteAllText(
            script,
            "import pathlib, subprocess, sys\n"
            + "child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'])\n"
            + "pathlib.Path(sys.argv[1]).write_text(str(child.pid))\n"
            + "child.wait()\n",
            Encoding.UTF8);

        using var process = StartPython(script, childPidFile);
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

    private static Process StartPython(string script, params string[] arguments)
    {
        var python = Environment.GetEnvironmentVariable("PYTHON") ?? "python3";
        var info = new ProcessStartInfo
        {
            FileName = python,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(script)!,
        };
        DevTools.ProcessCompatibility.AddArgument(info, script);
        foreach (var argument in arguments)
            DevTools.ProcessCompatibility.AddArgument(info, argument);

        var process = new Process { StartInfo = info };
        Assert.True(process.Start(), $"Could not start Python process '{python}'.");
        return process;
    }

    private static async Task<ProcessResult> RunPythonAsync(string script, params string[] arguments)
    {
        using var process = StartPython(script, arguments);
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
        Assert.True(int.TryParse(File.ReadAllText(path), out var pid), File.ReadAllText(path));
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
                if (File.Exists(Path.Combine(directory.FullName, "tools", "build-unity-package.py")) &&
                    File.Exists(Path.Combine(directory.FullName, "src", "Houtamelo.Spire", "Houtamelo.Spire.csproj")))
                {
                    return directory.FullName;
                }

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
        {
            var root = Path.Combine(
                FindRepositoryRoot(),
                "tmp",
                "devtools-process-tests",
                Guid.NewGuid().ToString("N"));
            return new ProcessFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
