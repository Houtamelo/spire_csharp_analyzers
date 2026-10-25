using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Houtamelo.Spire;
using Spire.BehavioralTests.Types;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class PublicUtilityTests
{
    [Fact]
    public void OfKind_InferredGenericCall_ReturnsMatchingVariant()
    {
        IEnumerable<PublicUtilityUnion> source = new[]
        {
            PublicUtilityUnion.Second(20),
            PublicUtilityUnion.First(10)
        };

        IEnumerable<PublicUtilityUnion> filtered =
            source.OfKind(PublicUtilityUnion.Kind.First);

        var result = Assert.Single(filtered);
        Assert.Equal(PublicUtilityUnion.Kind.First, result.kind);
        Assert.Equal(10, result.first.id);
    }

    [Fact]
    public void OfKind_FiltersAndPreservesSourceOrder()
    {
        var source = new[]
        {
            PublicUtilityUnion.Second(2),
            PublicUtilityUnion.First(1),
            PublicUtilityUnion.First(3),
            PublicUtilityUnion.Third(4),
            PublicUtilityUnion.First(5)
        };

        var result = source
            .OfKind(PublicUtilityUnion.Kind.First)
            .Select(item => item.first.id)
            .ToArray();

        Assert.Equal(new[] { 1, 3, 5 }, result);
    }

    [Fact]
    public void OfKind_EmptySource_ReturnsEmpty()
    {
        var result = Array.Empty<PublicUtilityUnion>()
            .OfKind(PublicUtilityUnion.Kind.First);

        Assert.Empty(result);
    }

    [Fact]
    public void OfKind_KnownKindAbsentFromSource_ReturnsEmpty()
    {
        var source = new[]
        {
            PublicUtilityUnion.First(1),
            PublicUtilityUnion.Second(2)
        };

        var result = source.OfKind(PublicUtilityUnion.Kind.Third);

        Assert.Empty(result);
    }

    [Fact]
    public void OfKind_UndefinedKindValue_ReturnsEmpty()
    {
        var source = new[]
        {
            PublicUtilityUnion.First(1),
            PublicUtilityUnion.Second(2),
            PublicUtilityUnion.Third(3)
        };

#pragma warning disable SPIRE016 // Intentional invalid enum input for OfKind characterization.
        var result = source.OfKind((PublicUtilityUnion.Kind)255);
#pragma warning restore SPIRE016

        Assert.Empty(result);
    }

    [Fact]
    public void OfKind_IsDeferred_AndFirstMoveNextDoesNotMaterializeSource()
    {
        var source = new CountingEnumerable(
            PublicUtilityUnion.First(1),
            PublicUtilityUnion.Second(2),
            PublicUtilityUnion.Third(3),
            PublicUtilityUnion.First(4));

        var filtered = source.OfKind(PublicUtilityUnion.Kind.First);

        Assert.Equal(0, source.GetEnumeratorCalls);
        Assert.Equal(0, source.MoveNextCalls);

        using var enumerator = filtered.GetEnumerator();
        Assert.Equal(0, source.GetEnumeratorCalls);
        Assert.Equal(0, source.MoveNextCalls);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, source.GetEnumeratorCalls);
        Assert.Equal(1, source.MoveNextCalls);
        Assert.Equal(1, enumerator.Current.first.id);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, source.MoveNextCalls);
        Assert.Equal(4, enumerator.Current.first.id);
    }

    [Fact]
    public void OfKind_NullSource_ThrowsWhenEnumerationStarts()
    {
        IEnumerable<PublicUtilityUnion>? source = null;
        var filtered = source!.OfKind(PublicUtilityUnion.Kind.First);

        using var enumerator = filtered.GetEnumerator();

        Assert.Throws<NullReferenceException>(() => enumerator.MoveNext());
    }

    private sealed class CountingEnumerable : IEnumerable<PublicUtilityUnion>
    {
        private readonly PublicUtilityUnion[] _items;

        public CountingEnumerable(params PublicUtilityUnion[] items)
        {
            _items = items;
        }

        public int GetEnumeratorCalls { get; private set; }

        public int MoveNextCalls { get; private set; }

        public IEnumerator<PublicUtilityUnion> GetEnumerator()
        {
            GetEnumeratorCalls++;
            return new CountingEnumerator(this, _items);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class CountingEnumerator : IEnumerator<PublicUtilityUnion>
        {
            private readonly CountingEnumerable _owner;
            private readonly PublicUtilityUnion[] _items;
            private int _index = -1;

            public CountingEnumerator(
                CountingEnumerable owner,
                PublicUtilityUnion[] items)
            {
                _owner = owner;
                _items = items;
            }

            public PublicUtilityUnion Current { get; private set; }

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                _owner.MoveNextCalls++;
                _index++;
                if (_index >= _items.Length)
                    return false;

                Current = _items[_index];
                return true;
            }

            public void Reset() => _index = -1;

            public void Dispose()
            {
            }
        }
    }
}
