using System;
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
        var expectedNames = ReflectedBehavioralFactNames();

        Assert.That(names, Is.EqualTo(expectedNames));
        Assert.That(names, Is.Unique);
        Assert.That(names, Is.EqualTo(names.OrderBy(name => name, StringComparer.Ordinal).ToArray()));
        Assert.That(names, Does.Contain("Spire.BehavioralTests.Tests.AdditiveTests.Circle_TagAndRadius"));
        Assert.That(names, Does.Contain("Spire.BehavioralTests.Tests.InlinerTests.InlinerStruct_ForwardsStaticCall"));
    }

    private static string[] ReflectedBehavioralFactNames()
        => typeof(XunitFactNUnitRunner).Assembly
            .GetTypes()
            .Where(type => type.IsClass &&
                type.IsPublic &&
                string.Equals(
                    type.Namespace,
                    "Spire.BehavioralTests.Tests",
                    StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly)
                .Where(method => method.IsDefined(typeof(XunitFactAttribute), inherit: false))
                .Select(method => string.Concat(type.FullName, ".", method.Name)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    [Test]
    public void RuntimeBackend_ReportsActualIl2CppPlayerWhenRequested()
    {
#if ENABLE_IL2CPP
        const string backend = "IL2CPP";
            Assert.That(Application.platform, Is.EqualTo(
#if UNITY_STANDALONE_WIN
                RuntimePlatform.WindowsPlayer
#else
                RuntimePlatform.LinuxPlayer
#endif
            ));

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
    public async Task Runner_YieldsSynchronousCompletionAndPreservesFailures()
    {
        var runner = new XunitFactNUnitRunner();
        var success = new XunitFactCase(typeof(SynchronousFixture), typeof(SynchronousFixture).GetMethod(nameof(SynchronousFixture.Run))!);
        var completion = runner.Run(success);

        Assert.That(completion.IsCompleted, Is.False);
        await completion;

        var failure = new XunitFactCase(typeof(ThrowingFixture), typeof(ThrowingFixture).GetMethod(nameof(ThrowingFixture.Throw))!);
        InvalidOperationException? exception = null;
        try
        {
            await runner.Run(failure);
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
    public sealed class SynchronousFixture
    {
        public void Run()
        {
        }
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
