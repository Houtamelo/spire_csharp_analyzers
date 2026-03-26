using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains;

/// Domain representing the {true, false} set.
internal sealed class BoolDomain : IValueDomain
{
    readonly bool _hasTrue;
    readonly bool _hasFalse;

    public ITypeSymbol Type { get; }

    public BoolDomain(ITypeSymbol type, bool hasTrue, bool hasFalse)
    {
        Type = type;
        _hasTrue = hasTrue;
        _hasFalse = hasFalse;
    }

    public static BoolDomain Universe(ITypeSymbol type) => new(type, hasTrue: true, hasFalse: true);

    public bool IsEmpty => !_hasTrue && !_hasFalse;

    public bool IsUniverse => _hasTrue && _hasFalse;

    public ImmutableArray<IValueDomain> Split()
    {
        if (IsEmpty)
            return ImmutableArray<IValueDomain>.Empty;

        var builder = ImmutableArray.CreateBuilder<IValueDomain>();

        if (_hasTrue)
            builder.Add(new BoolDomain(Type, hasTrue: true, hasFalse: false));
        if (_hasFalse)
            builder.Add(new BoolDomain(Type, hasTrue: false, hasFalse: true));

        return builder.ToImmutable();
    }

    public IValueDomain Subtract(IValueDomain other)
    {
        if (other is not BoolDomain otherBool)
            throw new ArgumentException($"Cannot subtract {other.GetType().Name} from BoolDomain");

        return new BoolDomain(
            Type,
            hasTrue: _hasTrue && !otherBool._hasTrue,
            hasFalse: _hasFalse && !otherBool._hasFalse);
    }

    public IValueDomain Intersect(IValueDomain other)
    {
        if (other is not BoolDomain otherBool)
            throw new ArgumentException($"Cannot intersect {other.GetType().Name} with BoolDomain");

        return new BoolDomain(
            Type,
            hasTrue: _hasTrue && otherBool._hasTrue,
            hasFalse: _hasFalse && otherBool._hasFalse);
    }

    public IValueDomain Complement() => new BoolDomain(Type, hasTrue: !_hasTrue, hasFalse: !_hasFalse);
}
