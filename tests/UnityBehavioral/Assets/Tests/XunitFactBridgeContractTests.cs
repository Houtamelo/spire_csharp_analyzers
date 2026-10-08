using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
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

        Assert.That(names, Has.Length.EqualTo(129));
        Assert.That(names, Is.Unique);
        Assert.That(names, Is.EqualTo(names.OrderBy(name => name, StringComparer.Ordinal).ToArray()));
        Assert.That(names, Does.Contain("Spire.BehavioralTests.Tests.AdditiveTests.Circle_TagAndRadius"));
        Assert.That(names, Does.Contain("Spire.BehavioralTests.Tests.UnsafeOverlapTests.RefFields"));
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
