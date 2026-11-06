using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Spire.Tool;
using Xunit;

namespace DevTools.Tests;

internal sealed class WindowsProcessFixture : IDisposable
{
    private readonly string scriptPath;
    private readonly string childScriptPath;
    private readonly string childPidPath;
    private readonly string parentPidPath;
    private readonly string childReadyPath;
    private readonly string parentReadyPath;
    private int childPid;
    private int parentPid;
    private Process? parentProcess;
    private Task<ProcessResult>? runParentCompletion;

    private WindowsProcessFixture(string root)
    {
        Root = root;
        scriptPath = Path.Combine(root, "single.ps1");
        childScriptPath = Path.Combine(root, "child.ps1");
        childPidPath = Path.Combine(root, "child.pid");
        parentPidPath = Path.Combine(root, "parent.pid");
        childReadyPath = Path.Combine(root, "child.ready");
        parentReadyPath = Path.Combine(root, "parent.ready");
        Directory.CreateDirectory(root);
    }

    public string Root { get; }
    public int ChildPid => childPid;

    public static WindowsProcessFixture Create()
        => new(Path.Combine(FindRepositoryRoot(), "tmp", "devtools-windows-process-tests", Guid.NewGuid().ToString("N")));

    public Process StartSingleScript(string body)
    {
        File.WriteAllText(
            scriptPath,
            "param([string]$ready,[string]$parentPid) " +
            "[IO.File]::WriteAllText($parentPid, [string]$PID); " + body);
        parentProcess = ProcessRunner.Start(
            "powershell.exe",
            new[]
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath,
                parentReadyPath, parentPidPath,
            },
            Root,
            ownProcessGroup: false);
        return parentProcess;
    }

    public Process StartParent(string mode)
    {
        File.WriteAllText(
            childScriptPath,
            "param([string]$ready) " +
            "[Console]::Out.WriteLine('child-stdout-ready'); [Console]::Out.Flush(); " +
            "[Console]::Error.WriteLine('child-stderr-ready'); [Console]::Error.Flush(); " +
            "[IO.File]::WriteAllText($ready, 'ready'); Start-Sleep -Seconds 30");
        File.WriteAllText(scriptPath, ParentScript);
        parentProcess = ProcessRunner.Start(
            "powershell.exe",
            new[]
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath,
                childScriptPath, childPidPath, parentPidPath, childReadyPath, parentReadyPath, mode,
            },
            Root,
            ownProcessGroup: false);
        childPid = 0;
        parentPid = 0;
        return parentProcess;
    }

    public Task<ProcessResult> RunParentAsync(string mode, TimeSpan timeout)
    {
        File.WriteAllText(childScriptPath, ChildScript);
        File.WriteAllText(scriptPath, ParentScript);
        runParentCompletion = ProcessRunner.RunAsync(
            "powershell.exe",
            new[]
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath,
                childScriptPath, childPidPath, parentPidPath, childReadyPath, parentReadyPath, mode,
            },
            Root,
            timeout,
            ownProcessGroup: false);
        return runParentCompletion;
    }

    public async Task WaitForReadyAsync()
    {
        await EnsureReadyAsync(parentReadyPath, "single");
        Assert.True(int.TryParse(File.ReadAllText(parentPidPath).Trim(), out parentPid) && parentPid > 0);
    }

    public async Task WaitForParentReadyAsync()
    {
        await EnsureReadyAsync(parentReadyPath, "parent");
        Assert.True(int.TryParse(File.ReadAllText(childPidPath).Trim(), out childPid) && childPid > 0);
        Assert.True(int.TryParse(File.ReadAllText(parentPidPath).Trim(), out parentPid) && parentPid > 0);
        Assert.True(IsRunning(childPid), $"Child process {childPid} exited before parent readiness.");
    }

    public bool ParentHasExited()
    {
        if (!File.Exists(parentPidPath))
            return false;
        return int.TryParse(File.ReadAllText(parentPidPath).Trim(), out var pid) && pid > 0 && !IsRunning(pid);
    }

    public async Task KillOwnedProcessesAsync()
    {
        if (childPid <= 0 && File.Exists(childPidPath))
            int.TryParse(File.ReadAllText(childPidPath).Trim(), out childPid);
        if (parentPid <= 0 && File.Exists(parentPidPath))
            int.TryParse(File.ReadAllText(parentPidPath).Trim(), out parentPid);
        await KillAsync(parentPid).ConfigureAwait(false);
        await KillAsync(childPid).ConfigureAwait(false);
    }

    public async Task<bool> ReadOtherStreamToEndAsync(Process process, string mode)
    {
        if (mode == "both")
            return true;
        var reader = mode == "stdout" ? process.StandardError : process.StandardOutput;
        var read = reader.ReadToEndAsync();
        var winner = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(8))).ConfigureAwait(false);
        if (winner != read)
            return false;
        await read.ConfigureAwait(false);
        return true;
    }

    public async Task KillAsync(int pid)
    {
        if (pid <= 0)
            return;
        try
        {
            using var process = Process.GetProcessById(pid);
            if (!process.HasExited)
                process.Kill();
            var wait = DevTools.ProcessCompatibility.WaitForExitAsync(process);
            var winner = await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(false);
            if (winner == wait)
            {
                try { await wait.ConfigureAwait(false); }
                catch (Exception) { }
            }
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
    }

    public async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
                return false;
            await Task.Delay(25).ConfigureAwait(false);
        }
        return true;
    }

    public static bool IsRunning(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return !process.HasExited; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    private async Task EnsureReadyAsync(string readyPath, string owner)
    {
        if (await WaitUntilAsync(() => File.Exists(readyPath), TimeSpan.FromSeconds(8)).ConfigureAwait(false))
            return;

        var diagnostic = $"Windows {owner} process fixture did not become ready.";
        if (parentProcess != null)
        {
            try
            {
                if (parentProcess.HasExited)
                {
                    diagnostic += $" Parent exit code: {parentProcess.ExitCode}.";
                    diagnostic += await ReadExitedProcessOutputAsync(parentProcess).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                diagnostic += " Parent diagnostics failed: " + exception.Message;
            }
        }

        if (runParentCompletion?.IsCompleted == true)
        {
            try
            {
                var result = await runParentCompletion.ConfigureAwait(false);
                diagnostic += $" RunAsync exit code: {result.ExitCode}. stdout: {result.StandardOutput} stderr: {result.StandardError}";
            }
            catch (Exception exception)
            {
                diagnostic += " RunAsync diagnostics: " + exception;
            }
        }

        diagnostic += " child pid file: " + ReadFileForDiagnostics(childPidPath);
        diagnostic += " parent pid file: " + ReadFileForDiagnostics(parentPidPath);
        throw new InvalidOperationException(diagnostic);
    }

    private static string ReadFileForDiagnostics(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : "<missing>"; }
        catch (Exception exception) { return "<unreadable: " + exception.Message + ">"; }
    }

    private static async Task<string> ReadExitedProcessOutputAsync(Process process)
    {
        Task<string> standardOutput;
        Task<string> standardError;
        try
        {
            standardOutput = process.StandardOutput.ReadToEndAsync();
            standardError = process.StandardError.ReadToEndAsync();
        }
        catch (Exception exception)
        {
            return " output diagnostics failed: " + exception.Message;
        }

        var combined = Task.WhenAll(standardOutput, standardError);
        var winner = await Task.WhenAny(combined, Task.Delay(TimeSpan.FromSeconds(1))).ConfigureAwait(false);
        if (winner != combined)
        {
            _ = combined.ContinueWith(
                completed => _ = completed.Exception,
                TaskScheduler.Default);
            return " stdout: <pipe still open> stderr: <pipe still open>";
        }

        try
        {
            var output = await combined.ConfigureAwait(false);
            return " stdout: " + output[0] + " stderr: " + output[1];
        }
        catch (Exception exception)
        {
            return " output diagnostics failed: " + exception.Message;
        }
    }

    private const string ChildScript =
        "param([string]$ready) " +
        "[Console]::Out.WriteLine('child-stdout-ready'); [Console]::Out.Flush(); " +
        "[Console]::Error.WriteLine('child-stderr-ready'); [Console]::Error.Flush(); " +
        "[IO.File]::WriteAllText($ready, 'ready'); Start-Sleep -Seconds 30";

    private const string HandleInheritanceSource = """
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

public static class SpireWindowsHandleInheritance
{
    private const uint HandleFlagInherit = 1;
    private const uint GenericRead = 2147483648;
    private const uint GenericWrite = 1073741824;
    private const uint FileShareRead = 1;
    private const uint FileShareWrite = 2;
    private const uint OpenExisting = 3;
    private const uint StartfUseStdHandles = 256;
    private const uint ExtendedStartupInfoPresent = 524288;
    private const uint CreateNoWindow = 134217728;
    private const int AttributeHandleList = 131074;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Cb;
        public string Reserved;
        public string Desktop;
        public string Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;
        public IntPtr Reserved2Pointer;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo
    {
        public IntPtr Process;
        public IntPtr Thread;
        public uint ProcessId;
        public uint ThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int number);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, int flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returnSize);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inherit, uint creationFlags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInfo process);

    private static IntPtr RequireHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return handle;
    }

    private static void Inherit(IntPtr handle)
    {
        if (!SetHandleInformation(handle, HandleFlagInherit, HandleFlagInherit))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static int StartChild(string executable, string arguments, string mode)
    {
        if (mode != "stdout" && mode != "stderr" && mode != "both" && mode != "live")
            throw new ArgumentException("Unknown handle mode: " + mode);

        var originalOutput = RequireHandle(GetStdHandle(-11));
        var originalError = RequireHandle(GetStdHandle(-12));
        Inherit(originalOutput);
        Inherit(originalError);
        var nulInput = RequireHandle(CreateFile("NUL", GenericRead, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero));
        IntPtr nulWrite = IntPtr.Zero;
        IntPtr attributes = IntPtr.Zero;
        IntPtr handles = IntPtr.Zero;
        var initialized = false;
        try
        {
            Inherit(nulInput);
            var childOutput = originalOutput;
            var childError = originalError;
            if (mode == "stdout")
            {
                nulWrite = RequireHandle(CreateFile("NUL", GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero));
                Inherit(nulWrite);
                childError = nulWrite;
            }
            else if (mode == "stderr")
            {
                nulWrite = RequireHandle(CreateFile("NUL", GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero));
                Inherit(nulWrite);
                childOutput = nulWrite;
            }

            var size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attributes = Marshal.AllocHGlobal(size);
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            initialized = true;
            handles = Marshal.AllocHGlobal(IntPtr.Size * 3);
            Marshal.WriteIntPtr(handles, 0, nulInput);
            Marshal.WriteIntPtr(handles, IntPtr.Size, childOutput);
            Marshal.WriteIntPtr(handles, IntPtr.Size * 2, childError);
            if (!UpdateProcThreadAttribute(attributes, 0, (IntPtr)AttributeHandleList, handles, (IntPtr)(IntPtr.Size * 3), IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var startup = new StartupInfoEx();
            startup.StartupInfo.Cb = Marshal.SizeOf(typeof(StartupInfoEx));
            startup.StartupInfo.Flags = (int)StartfUseStdHandles;
            startup.StartupInfo.StandardInput = nulInput;
            startup.StartupInfo.StandardOutput = childOutput;
            startup.StartupInfo.StandardError = childError;
            startup.AttributeList = attributes;
            var command = new StringBuilder("\"" + executable + "\" " + arguments);
            ProcessInfo process;
            if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, true, ExtendedStartupInfoPresent | CreateNoWindow, IntPtr.Zero, null, ref startup, out process))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            CloseHandle(process.Thread);
            CloseHandle(process.Process);
            return checked((int) process.ProcessId);
        }
        finally
        {
            if (initialized)
                DeleteProcThreadAttributeList(attributes);
            if (handles != IntPtr.Zero)
                Marshal.FreeHGlobal(handles);
            if (attributes != IntPtr.Zero)
                Marshal.FreeHGlobal(attributes);
            if (nulWrite != IntPtr.Zero)
                CloseHandle(nulWrite);
            CloseHandle(nulInput);
        }
    }
}
""";

    private static string ParentScript =>
        "param([string]$childScript,[string]$childPid,[string]$parentPid,[string]$childReady,[string]$parentReady,[string]$mode) " +
        "$ErrorActionPreference='Stop'; [IO.File]::WriteAllText($parentPid,[string]$PID); " +
        "Add-Type -TypeDefinition '" + HandleInheritanceSource + "'; " +
        "$childArguments='-NoProfile -NonInteractive -ExecutionPolicy Bypass -File '+[char]34+$childScript+[char]34+' '+[char]34+$childReady+[char]34; " +
        "$childPidValue=[SpireWindowsHandleInheritance]::StartChild($PSHOME+'\\powershell.exe',$childArguments,$mode); " +
        "[IO.File]::WriteAllText($childPid,[string]$childPidValue); " +
        "while(-not (Test-Path $childReady)){Start-Sleep -Milliseconds 10}; " +
        "[IO.File]::WriteAllText($parentPid,[string]$PID); [IO.File]::WriteAllText($parentReady,'ready'); " +
        "if($mode -eq 'live'){[Diagnostics.Process]::GetProcessById($childPidValue).WaitForExit()}";

    private static string FindRepositoryRoot()
    {
        var configured = Environment.GetEnvironmentVariable("SPIRE_UNITY_TEST_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var full = Path.GetFullPath(configured);
            if (File.Exists(Path.Combine(full, "Spire.Analyzers.slnx")))
                return full;
            throw new InvalidOperationException("Configured Unity test repository root is invalid: " + full);
        }

        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Spire.Analyzers.slnx")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    public void Dispose()
    {
        try { KillOwnedProcessesAsync().GetAwaiter().GetResult(); }
        finally { TestDirectories.DeleteOwned(Root); }
    }
}
