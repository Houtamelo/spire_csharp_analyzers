#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Assert = NUnit.Framework.Assert;
using Does = NUnit.Framework.Does;
using Has = NUnit.Framework.Has;
using Is = NUnit.Framework.Is;
using NUnitTestAttribute = NUnit.Framework.TestAttribute;
using XunitFactAttribute = Xunit.FactAttribute;
using Xunit.Abstractions;
using XunitInlineDataAttribute = Xunit.InlineDataAttribute;
using XunitMemberDataAttribute = Xunit.MemberDataAttribute;
using XunitTheoryAttribute = Xunit.TheoryAttribute;
using XunitAsyncLifetime = Xunit.IAsyncLifetime;

namespace Spire.UnityTestHarness;

[XunitBridgeInfrastructure]
public sealed class XunitReflectionBridgeContractTests
{
    [NUnitTestAttribute]
    public void Discover_ExpandsFactsInlineDataMemberDataAndInheritedGenericMethods()
    {
        var cases = XunitReflectionBridge.Discover(new[]
        {
            typeof(DiscoveryFixture),
            typeof(ClosedGenericFixture),
        });

        Assert.That(cases, Has.Length.EqualTo(6));
        Assert.That(cases.Select(testCase => testCase.Identity), Is.Unique);
        Assert.That(cases.Count(testCase => testCase.Method.Name == nameof(DiscoveryFixture.Fact)), Is.EqualTo(1));
        Assert.That(cases.Count(testCase => testCase.Method.Name == nameof(DiscoveryFixture.Inline)), Is.EqualTo(2));
        Assert.That(cases.Count(testCase => testCase.Method.Name == nameof(DiscoveryFixture.Member)), Is.EqualTo(2));
        Assert.That(cases.Single(testCase => testCase.Method.Name == nameof(GenericBase<int>.Inherited)).FixtureType,
            Is.EqualTo(typeof(ClosedGenericFixture)));

        var manifest = XunitReflectionBridge.ExportDiscoveryManifest(cases);
        Assert.That(manifest, Does.Contain("\"displayName\""));
        Assert.That(manifest, Does.Contain("\"arguments\":[1]"));

        DiscoveryFixture.ReverseRows = false;
        var originalIdentities = XunitReflectionBridge.Discover(new[] { typeof(DiscoveryFixture) })
            .Where(testCase => testCase.Method.Name == nameof(DiscoveryFixture.Member))
            .Select(testCase => testCase.Identity)
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();
        DiscoveryFixture.ReverseRows = true;
        var reversedIdentities = XunitReflectionBridge.Discover(new[] { typeof(DiscoveryFixture) })
            .Where(testCase => testCase.Method.Name == nameof(DiscoveryFixture.Member))
            .Select(testCase => testCase.Identity)
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();
        DiscoveryFixture.ReverseRows = false;
        Assert.That(reversedIdentities, Is.EqualTo(originalIdentities));
    }

    [NUnitTestAttribute]
    public async Task Invoke_AwaitsAsyncLifecycleAndDisposesFreshFixture()
    {
        AsyncFixture.Reset();
        var testCase = XunitReflectionBridge.Discover(new[] { typeof(AsyncFixture) }).Single();

        await XunitReflectionBridge.InvokeAsync(testCase);

        Assert.That(AsyncFixture.Initialized, Is.True);
        Assert.That(AsyncFixture.Ran, Is.True);
        Assert.That(AsyncFixture.Disposed, Is.True);
    }

    [NUnitTestAttribute]
    public async Task Invoke_InjectsOutputHelperAndPropagatesAsyncFailure()
    {
        OutputFixture.OutputReceived = false;
        var outputCase = XunitReflectionBridge.Discover(new[] { typeof(OutputFixture) }).Single();
        await XunitReflectionBridge.InvokeAsync(outputCase);
        Assert.That(OutputFixture.OutputReceived, Is.True);

        var failureCase = XunitReflectionBridge.Discover(new[] { typeof(FailingAsyncFixture) }).Single();
        InvalidOperationException? failure = null;
        try
        {
            await XunitReflectionBridge.InvokeAsync(failureCase);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception;
        }

        Assert.That(failure, Is.Not.Null);
        Assert.That(failure!.Message, Is.EqualTo("async bridge failure"));
    }

    [NUnitTestAttribute]
    public async Task Runner_YieldsSynchronousCompletionAndPreservesFailures()
    {
        var runner = new XunitReflectionNUnitRunner();
        OutputFixture.OutputReceived = false;
        var outputCase = XunitReflectionBridge.Discover(new[] { typeof(OutputFixture) }).Single();
        var completion = runner.Run(outputCase);

        Assert.That(completion.IsCompleted, Is.False);
        await completion;
        Assert.That(OutputFixture.OutputReceived, Is.True);

        var failureCase = XunitReflectionBridge.Discover(new[] { typeof(FailingAsyncFixture) }).Single();
        InvalidOperationException? failure = null;
        try
        {
            await runner.Run(failureCase);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception;
        }

        Assert.That(failure, Is.Not.Null);
        Assert.That(failure!.Message, Is.EqualTo("async bridge failure"));
    }

    [NUnitTestAttribute]
    public void Discover_RejectsSkippedMissingAndFailingData()
    {
        Assert.Throws<InvalidOperationException>(() =>
            XunitReflectionBridge.Discover(new[] { typeof(SkippedFixture) }));
        Assert.Throws<InvalidOperationException>(() =>
            XunitReflectionBridge.Discover(new[] { typeof(MissingDataFixture) }));
        Assert.Throws<InvalidOperationException>(() =>
            XunitReflectionBridge.Discover(new[] { typeof(FailingDataFixture) }));
    }

    [NUnitTestAttribute]
    public void DiscoverWithPlatformSelection_ExecutesSupportedCaseAndRecordsOneExplicitExclusion()
    {
        var result = XunitReflectionBridge.DiscoverWithPlatformSelection(
            new[] { typeof(PlatformSelectionFixture) });

        var currentPlatform = CurrentPlatform();
        var supportedMethod = currentPlatform switch
        {
            "Linux" => nameof(PlatformSelectionFixture.Linux),
            "Windows" => nameof(PlatformSelectionFixture.Windows),
            _ => null,
        };
        var excludedMethods = currentPlatform switch
        {
            "Linux" => new[] { nameof(PlatformSelectionFixture.Windows) },
            "Windows" => new[] { nameof(PlatformSelectionFixture.Linux) },
            _ => new[] { nameof(PlatformSelectionFixture.Linux), nameof(PlatformSelectionFixture.Windows) },
        };

        Assert.That(result.Cases.Count, Is.EqualTo(supportedMethod == null ? 0 : 1));
        if (supportedMethod != null)
            Assert.That(result.Cases.Single().Method.Name, Is.EqualTo(supportedMethod));
        Assert.That(result.PlatformExclusions.Count, Is.EqualTo(excludedMethods.Length));
        Assert.That(
            result.PlatformExclusions.Select(exclusion => exclusion.Case.Method.Name),
            Is.EqualTo(excludedMethods));
        Assert.That(
            result.PlatformExclusions.Select(exclusion => exclusion.RequiredPlatform),
            Is.EqualTo(excludedMethods));
        Assert.That(
            result.PlatformExclusions.Select(exclusion => exclusion.Reason),
            Is.EqualTo(excludedMethods.Select(method => "Requires " + method + "; current platform is " + currentPlatform + ".")));

        var manifest = XunitReflectionBridge.ExportDiscoveryManifest(result.Cases, result.PlatformExclusions);
        Assert.That(manifest, Does.Contain("\"platform\":\"" + currentPlatform + "\""));
        Assert.That(manifest, Does.Contain("\"cases\":["));
        Assert.That(manifest, Does.Contain("\"platformExclusions\":["));
        foreach (var exclusion in result.PlatformExclusions)
        {
            Assert.That(manifest, Does.Contain("\"identity\":\"" + exclusion.Case.Identity + "\""));
            Assert.That(manifest, Does.Contain("\"requiredPlatform\":\"" + exclusion.RequiredPlatform + "\""));
            Assert.That(manifest, Does.Contain("\"reason\":\"" + exclusion.Reason + "\""));
        }
    }

    [NUnitTestAttribute]
    public void DiscoverWithPlatformSelection_RejectsAnOverriddenPlatformSkipReason()
    {
        var currentPlatform = CurrentPlatform();
        var (fixtureType, methodName, requiredPlatform) = currentPlatform switch
        {
            "Linux" => (typeof(ModifiedWindowsPlatformSkipFixture), nameof(ModifiedWindowsPlatformSkipFixture.Windows), "Windows"),
            "Windows" => (typeof(ModifiedLinuxPlatformSkipFixture), nameof(ModifiedLinuxPlatformSkipFixture.Linux), "Linux"),
            _ => (typeof(ModifiedLinuxPlatformSkipFixture), nameof(ModifiedLinuxPlatformSkipFixture.Linux), "Linux"),
        };
        var method = fixtureType.GetMethod(methodName)!;
        Assert.That(XunitReflectionBridge.GetRequiredPlatform(method), Is.EqualTo(requiredPlatform));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            XunitReflectionBridge.DiscoverWithPlatformSelection(
                new[] { fixtureType }));

        Assert.That(exception!.Message, Does.Contain("platform"));
        Assert.That(exception.Message, Does.Contain("custom platform reason"));
    }

    [NUnitTestAttribute]
    public void DiscoverWithPlatformSelection_RejectsPlatformSkipWhenTheRequiredPlatformIsCurrent()
    {
        var currentPlatform = CurrentPlatform();
        var (fixtureType, methodName, requiredPlatform) = currentPlatform switch
        {
            "Windows" => (typeof(ModifiedCurrentWindowsPlatformSkipFixture), nameof(ModifiedCurrentWindowsPlatformSkipFixture.Windows), "Windows"),
            _ => (typeof(ModifiedCurrentLinuxPlatformSkipFixture), nameof(ModifiedCurrentLinuxPlatformSkipFixture.Linux), "Linux"),
        };
        var method = fixtureType.GetMethod(methodName)!;
        Assert.That(XunitReflectionBridge.GetRequiredPlatform(method), Is.EqualTo(requiredPlatform));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            XunitReflectionBridge.DiscoverWithPlatformSelection(
                new[] { fixtureType }));

        Assert.That(exception!.Message, Does.Contain("platform"));
        Assert.That(exception.Message, Does.Contain("custom current-platform reason"));
    }

    [NUnitTestAttribute]
    public void DiscoverWithPlatformSelection_RepresentsAnExcludedOnlyFixture()
    {
        var fixtureType = CurrentPlatform() == "Linux"
            ? typeof(WindowsOnlySelectionFixture)
            : typeof(LinuxOnlySelectionFixture);
        var result = XunitReflectionBridge.DiscoverWithPlatformSelection(new[] { fixtureType });

        Assert.That(result.Cases.Count, Is.EqualTo(0));
        Assert.That(result.PlatformExclusions.Count, Is.EqualTo(1));
        Assert.That(result.PlatformExclusions.Single().Case.FixtureType, Is.EqualTo(fixtureType));
    }

    [NUnitTestAttribute]
    public void PlatformHelpers_ExposeCanonicalHostAndReasonContracts()
    {
        var linux = typeof(PlatformSelectionFixture).GetMethod(nameof(PlatformSelectionFixture.Linux))!;
        var windows = typeof(PlatformSelectionFixture).GetMethod(nameof(PlatformSelectionFixture.Windows))!;

        Assert.That(XunitReflectionBridge.CurrentPlatform, Is.EqualTo(CurrentPlatform()));
        Assert.That(XunitReflectionBridge.GetRequiredPlatform(linux), Is.EqualTo("Linux"));
        Assert.That(XunitReflectionBridge.GetRequiredPlatform(windows), Is.EqualTo("Windows"));
        Assert.That(
            XunitReflectionBridge.PlatformSkipReason("Linux"),
            Is.EqualTo("Requires Linux; current platform is " + CurrentPlatform() + "."));
        Assert.That(
            XunitReflectionBridge.PlatformSkipReason("Windows"),
            Is.EqualTo("Requires Windows; current platform is " + CurrentPlatform() + "."));
    }

    private static string CurrentPlatform()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS"
            : "Other";

    [XunitBridgeInfrastructure]
    public sealed class DiscoveryFixture
    {
        public static bool ReverseRows { get; set; }

        public static IEnumerable<object[]> MemberRows => ReverseRows
            ? new[]
            {
                new object[] { "second" },
                new object[] { "first" },
            }
            : new[]
            {
                new object[] { "first" },
                new object[] { "second" },
            };

        [XunitFactAttribute]
        public void Fact()
        {
        }

        [XunitTheoryAttribute]
        [XunitInlineDataAttribute(1)]
        [XunitInlineDataAttribute(2)]
        public void Inline(int value)
        {
        }

        [XunitTheoryAttribute]
        [XunitMemberDataAttribute(nameof(MemberRows))]
        public void Member(string value)
        {
        }
    }

    [XunitBridgeInfrastructure]
    public abstract class GenericBase<T>
    {
        [XunitTheoryAttribute]
        [XunitInlineDataAttribute(7)]
        public void Inherited(T value)
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class ClosedGenericFixture : GenericBase<int>
    {
    }

    [XunitBridgeInfrastructure]
    public sealed class AsyncFixture : XunitAsyncLifetime, IDisposable
    {
        public static bool Initialized { get; private set; }
        public static bool Ran { get; private set; }
        public static bool Disposed { get; private set; }

        public static void Reset()
        {
            Initialized = false;
            Ran = false;
            Disposed = false;
        }

        [XunitFactAttribute]
        public async Task RunAsync()
        {
            await Task.Yield();
            Ran = true;
        }

        public Task InitializeAsync()
        {
            Initialized = true;
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            Disposed = true;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class OutputFixture
    {
        public static bool OutputReceived { get; set; }

        public OutputFixture(ITestOutputHelper output)
        {
            output.WriteLine("bridge output");
            OutputReceived = true;
        }

        [XunitFactAttribute]
        public void Fact()
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class FailingAsyncFixture
    {
        [XunitFactAttribute]
        public async Task FailsAsync()
        {
            await Task.Yield();
            throw new InvalidOperationException("async bridge failure");
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class SkippedFixture
    {
        [XunitFactAttribute(Skip = "bridge contract")]
        public void Skipped()
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class PlatformSelectionFixture
    {
        [DevTools.Tests.LinuxFact]
        public void Linux()
        {
        }

        [DevTools.Tests.WindowsFact]
        public void Windows()
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class ModifiedLinuxPlatformSkipFixture
    {
        [DevTools.Tests.LinuxFact(Skip = "custom platform reason")]
        public void Linux()
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class ModifiedWindowsPlatformSkipFixture
    {
        [DevTools.Tests.WindowsFact(Skip = "custom platform reason")]
        public void Windows()
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class ModifiedCurrentLinuxPlatformSkipFixture
    {
        [DevTools.Tests.LinuxFact(Skip = "custom current-platform reason")]
        public void Linux()
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class ModifiedCurrentWindowsPlatformSkipFixture
    {
        [DevTools.Tests.WindowsFact(Skip = "custom current-platform reason")]
        public void Windows()
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class LinuxOnlySelectionFixture
    {
        [DevTools.Tests.LinuxFact]
        public void Linux()
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class WindowsOnlySelectionFixture
    {
        [DevTools.Tests.WindowsFact]
        public void Windows()
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class MissingDataFixture
    {
        [XunitTheoryAttribute]
        public void Missing(int value)
        {
        }
    }

    [XunitBridgeInfrastructure]
    public sealed class FailingDataFixture
    {
        [XunitTheoryAttribute]
        [FailingData]
        public void Failing(int value)
        {
        }
    }

    private sealed class FailingDataAttribute : Xunit.Sdk.DataAttribute
    {
        public override IEnumerable<object[]> GetData(System.Reflection.MethodInfo testMethod)
            => throw new InvalidOperationException("data provider failure");
    }
}
