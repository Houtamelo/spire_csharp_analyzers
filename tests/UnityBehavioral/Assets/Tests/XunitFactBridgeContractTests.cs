using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using XunitFactAttribute = Xunit.FactAttribute;
using XunitTheoryAttribute = Xunit.TheoryAttribute;

namespace Spire.UnityBehavioralTests;

[XunitBridgeInfrastructure]
public sealed class XunitFactBridgeContractTests
{
    [Test]
    public void Discover_FindsAllCopiedFactsWithStableNames()
    {
        var cases = XunitTestBridge.Discover(typeof(XunitFactNUnitRunner).Assembly);
        var names = cases.Select(testCase => testCase.FullName).ToArray();

        Assert.That(names, Has.Length.EqualTo(73));
        Assert.That(names, Is.Unique);
        Assert.That(names, Is.EqualTo(names.OrderBy(name => name, StringComparer.Ordinal).ToArray()));
        Assert.That(names, Does.Contain("Spire.BehavioralTests.Tests.AdditiveTests.Circle_TagAndRadius"));
        Assert.That(names, Does.Contain("Spire.BehavioralTests.Tests.InlinerTests.InlinerStruct_ForwardsStaticCall"));
    }

    [Test]
    public void RuntimeBackend_ReportsActualIl2CppPlayerWhenRequested()
    {
#if ENABLE_IL2CPP
        const string backend = "IL2CPP";
        Assert.That(Application.platform, Is.EqualTo(RuntimePlatform.LinuxPlayer));

        var reportPath = ResolveBackendReportPath();
        if (reportPath == null)
        {
            return;
        }

        var report = new BackendRuntimeEvidence
        {
            unityVersion = Application.unityVersion,
            backend = backend,
            runtimePlatform = Application.platform.ToString(),
            testAssembly = typeof(XunitFactBridgeContractTests).Assembly.GetName().Name ?? string.Empty,
        };
        var directory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(reportPath, JsonUtility.ToJson(report, prettyPrint: true));
#else
        // Mono EditMode runs intentionally keep the ordinary bridge behavior and produce no player report.
        return;
#endif
    }

#if ENABLE_IL2CPP
    [Test]
    public void Il2Cpp_DynamicMethodAndIlGeneratorReportUnsupportedRuntime()
    {
        Exception? captured = null;
        var succeeded = false;
        try
        {
            var method = new DynamicMethod("SpireIl2CppProbe", typeof(int), Type.EmptyTypes);
            var generator = method.GetILGenerator();
            generator.Emit(OpCodes.Ldc_I4, 42);
            generator.Emit(OpCodes.Ret);
            var function = (Func<int>)method.CreateDelegate(typeof(Func<int>));
            _ = function();
            succeeded = true;
        }
        catch (Exception exception)
        {
            captured = exception;
        }

        AssertUnsupportedRuntimeOperation("DynamicMethod/ILGenerator", succeeded, captured);
    }

    [Test]
    public void Il2Cpp_ProcessStartReportsUnsupportedRuntime()
    {
        Process? process = null;
        Exception? captured = null;
        var started = false;
        try
        {
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/true",
                    UseShellExecute = false,
                },
            };
            started = process.Start();
            if (started)
            {
                process.WaitForExit();
            }
        }
        catch (Exception exception)
        {
            captured = exception;
        }
        finally
        {
            if (process != null)
            {
                try
                {
                    if (started && !process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                }
                catch
                {
                    // Preserve the original capability result; the process is disposed below.
                }

                process.Dispose();
            }
        }

        AssertProcessStartUnsupported(started, captured);
    }
#endif

    private static void AssertUnsupportedRuntimeOperation(
        string operation,
        bool succeeded,
        Exception? captured)
    {
        if (captured == null)
        {
            Assert.That(succeeded, Is.False, operation + " unexpectedly succeeded.");
            Assert.Fail(operation + " returned without an expected exception.");
        }

        TestContext.WriteLine(
            $"{operation} unsupported exception: {captured!.GetType().FullName}: {captured.Message}");
        Assert.That(
            captured is NotSupportedException || captured is NotImplementedException,
            Is.True,
            $"{operation} produced an unexpected exception type {captured.GetType().FullName}: {captured.Message}");
    }

#if ENABLE_IL2CPP
    private static void AssertProcessStartUnsupported(bool started, Exception? captured)
    {
        if (captured == null)
        {
            Assert.Fail("Process.Start(/bin/true) unexpectedly succeeded.");
            return;
        }

        if (captured is not Win32Exception win32)
        {
            Assert.Fail(
                $"Process.Start(/bin/true) produced an unexpected exception type {captured!.GetType().FullName}: {captured.Message}");
            return;
        }

        TestContext.WriteLine(
            $"Process.Start(/bin/true) unsupported exception: {win32.GetType().FullName}: {win32.Message}");
        Assert.That(started, Is.False, "Process.Start(/bin/true) returned success before reporting an error.");
        Assert.That(win32.NativeErrorCode, Is.EqualTo(0));
        Assert.That(
            win32.Message,
            Is.EqualTo("ApplicationName='/bin/true', CommandLine='', CurrentDirectory='', Native error= Success"));
        // Unity's IL2CPP Process::CreateProcess_internal icall is an unimplemented stub
        // that returns false in non-debug builds, which becomes Win32Exception(0) here.
    }
#endif

    private static string? ResolveBackendReportPath()
    {
        var arguments = Environment.GetCommandLineArgs();
        for (var index = 0; index < arguments.Length; index++)
        {
            const string argumentName = "-spire-backend-report";
            var argument = arguments[index];
            if (string.Equals(argument, argumentName, StringComparison.Ordinal))
            {
                return index + 1 < arguments.Length && !string.IsNullOrWhiteSpace(arguments[index + 1])
                    ? Path.GetFullPath(arguments[index + 1])
                    : null;
            }

            if (argument.StartsWith(argumentName + "=", StringComparison.Ordinal))
            {
                var value = argument[(argumentName.Length + 1)..];
                return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value);
            }
        }

        return null;
    }

    [Serializable]
    private sealed class BackendRuntimeEvidence
    {
        public string unityVersion = string.Empty;
        public string backend = string.Empty;
        public string runtimePlatform = string.Empty;
        public string testAssembly = string.Empty;
    }

    [Test]
    public async Task Invoke_UnwrapsFactExceptionAndPreservesOriginalStack()
    {
        var method = typeof(ThrowingFixture).GetMethod(nameof(ThrowingFixture.Throw), BindingFlags.Public | BindingFlags.Instance);
        var testCase = new XunitFactCase(typeof(ThrowingFixture), method!);

        InvalidOperationException? exception = null;
        try
        {
            await XunitTestBridge.InvokeAsync(testCase);
        }
        catch (InvalidOperationException caught)
        {
            exception = caught;
        }

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Is.EqualTo("bridge failure"));
        Assert.That(exception.StackTrace, Does.Contain(nameof(ThrowingFixture.Throw)));
    }

    [Test]
    public async Task Invoke_AwaitsTaskAndDisposesFixture()
    {
        DisposableFixture.WasDisposed = false;
        var method = typeof(DisposableFixture).GetMethod(nameof(DisposableFixture.RunAsync), BindingFlags.Public | BindingFlags.Instance);
        var testCase = new XunitFactCase(typeof(DisposableFixture), method!);

        await XunitTestBridge.InvokeAsync(testCase);
        Assert.That(DisposableFixture.WasDisposed, Is.True);
    }

    [Test]
    public void Discover_RejectsTheoryMethods()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => XunitTestBridge.Discover(new[] { typeof(UnsupportedTheoryFixture) }));

        Assert.That(exception.Message, Does.Contain("Xunit.Theory is not supported"));
    }

    [Test]
    public void Discover_RejectsSkippedFacts()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => XunitTestBridge.Discover(new[] { typeof(UnsupportedSkippedFixture) }));

        Assert.That(exception.Message, Does.Contain("Skipped Xunit.Fact methods are not supported"));
    }

    [XunitBridgeInfrastructure]
    public sealed class ThrowingFixture
    {
        public void Throw()
        {
            throw new InvalidOperationException("bridge failure");
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class DisposableFixture : IDisposable
    {
        public static bool WasDisposed { get; set; }

        public async Task RunAsync()
        {
            await Task.Yield();
        }

        public void Dispose()
        {
            WasDisposed = true;
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class UnsupportedTheoryFixture
    {
        [XunitTheoryAttribute]
        public void Theory(int value)
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class UnsupportedSkippedFixture
    {
        [XunitFactAttribute(Skip = "bridge contract")]
        public void Skipped()
        {
        }
    }
}
