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
    public IValueDomain Inner => _inner;

    /// Whether null is still an uncovered value in this domain.
    internal bool HasNull => _hasNull;

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
            // If this inner is empty, result inner stays empty.
            // If other inner is empty, nothing to subtract from the inner.
            IValueDomain subtractedInner;
            if (_inner is EmptyDomain)
                subtractedInner = _inner;
            else if (otherNullable._inner is EmptyDomain)
                subtractedInner = _inner;
            else
                subtractedInner = _inner.Subtract(otherNullable._inner);

            return new NullableDomain(
                Type,
                subtractedInner,
                hasNull: _hasNull && !otherNullable._hasNull);
        }

        // Bare inner domain — subtract from inner only, null is unaffected
        if (_inner is EmptyDomain)
            return this;
        return new NullableDomain(Type, _inner.Subtract(other), hasNull: _hasNull);
    }

    public IValueDomain Intersect(IValueDomain other)
    {
        if (other is NullableDomain otherNullable)
        {
            // If either inner is empty, the intersection inner is empty.
            // This avoids calling Intersect between incompatible domain types
            // (e.g., EmptyDomain vs BoolDomain).
            IValueDomain intersectedInner;
            if (_inner is EmptyDomain || otherNullable._inner is EmptyDomain)
                intersectedInner = new EmptyDomain(_inner.Type);
            else
                intersectedInner = _inner.Intersect(otherNullable._inner);

            return new NullableDomain(
                Type,
                intersectedInner,
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
            var emptyInner = new EmptyDomain(_inner.Type);
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
