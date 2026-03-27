using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains;

/// Domain representing {null} ∪ DomainOf(T).
/// Wraps any inner IValueDomain and adds null as an additional possible value.
internal sealed class NullableDomain : IValueDomain
{
    readonly IValueDomain _inner;
    readonly bool _hasNull;

    public ITypeSymbol Type { get; }

    public NullableDomain(ITypeSymbol type, IValueDomain inner, bool hasNull)
    {
        Type = type;
        _inner = inner;
        _hasNull = hasNull;
    }

    public static NullableDomain Universe(ITypeSymbol type, IValueDomain innerUniverse)
        => new(type, innerUniverse, hasNull: true);

    public bool IsEmpty => !_hasNull && _inner.IsEmpty;

    public bool IsUniverse => _hasNull && _inner.IsUniverse;

    public IValueDomain Subtract(IValueDomain other)
    {
        if (other is NullableDomain otherNullable)
        {
            return new NullableDomain(
                Type,
                _inner.Subtract(otherNullable._inner),
                hasNull: _hasNull && !otherNullable._hasNull);
        }

        // Bare inner domain — subtract from inner only, null is unaffected
        return new NullableDomain(Type, _inner.Subtract(other), hasNull: _hasNull);
    }

    public IValueDomain Intersect(IValueDomain other)
    {
        if (other is NullableDomain otherNullable)
        {
            return new NullableDomain(
                Type,
                _inner.Intersect(otherNullable._inner),
                hasNull: _hasNull && otherNullable._hasNull);
        }

        // Bare inner domain — no null in other, so null drops out of intersection
        return new NullableDomain(Type, _inner.Intersect(other), hasNull: false);
    }

    public IValueDomain Complement()
        => new NullableDomain(Type, _inner.Complement(), hasNull: !_hasNull);

    public ImmutableArray<IValueDomain> Split()
    {
        if (IsEmpty)
            return ImmutableArray<IValueDomain>.Empty;

        var innerParts = _inner.Split();
        var capacity = innerParts.Length + (_hasNull ? 1 : 0);
        var builder = ImmutableArray.CreateBuilder<IValueDomain>(capacity);

        // Null partition comes first (if present)
        if (_hasNull)
        {
            var emptyInner = _inner.Subtract(_inner); // guaranteed empty inner
            builder.Add(new NullableDomain(Type, emptyInner, hasNull: true));
        }

        // Each inner partition wrapped as NullableDomain without null
        foreach (var part in innerParts)
        {
            builder.Add(new NullableDomain(Type, part, hasNull: false));
        }

        return builder.MoveToImmutable();
    }
}
