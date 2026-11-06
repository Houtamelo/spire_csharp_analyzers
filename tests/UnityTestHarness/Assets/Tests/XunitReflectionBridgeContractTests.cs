#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
