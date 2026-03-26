using System;
using System.Collections.Immutable;
using Houtamelo.Spire.PatternAnalysis.Domains.Numeric;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Domains;

/// Domain wrapping IntervalSet with type-specific universe bounds.
internal sealed class NumericDomain : IValueDomain
{
    readonly IntervalSet _intervals;
    readonly double _universeMin;
    readonly double _universeMax;

    public ITypeSymbol Type { get; }

    /// Expose intervals for testing and downstream consumers.
    public IntervalSet Intervals => _intervals;

    public NumericDomain(ITypeSymbol type, IntervalSet intervals, double universeMin, double universeMax)
    {
        Type = type;
        _intervals = intervals;
        _universeMin = universeMin;
        _universeMax = universeMax;
    }

    /// Create a universe domain covering all values of the given numeric type.
    public static NumericDomain Universe(ITypeSymbol type)
    {
        var (min, max) = GetBounds(type);
        return new NumericDomain(type, IntervalSet.Universe(min, max), min, max);
    }

    public bool IsEmpty => _intervals.IsEmpty;

    public bool IsUniverse => _intervals.CoversAll(_universeMin, _universeMax);

    public IValueDomain Subtract(IValueDomain other)
    {
        if (other is not NumericDomain otherNumeric)
            throw new ArgumentException($"Cannot subtract {other.GetType().Name} from NumericDomain");

        return new NumericDomain(Type, _intervals.Subtract(otherNumeric._intervals), _universeMin, _universeMax);
    }

    public IValueDomain Intersect(IValueDomain other)
    {
        if (other is not NumericDomain otherNumeric)
            throw new ArgumentException($"Cannot intersect {other.GetType().Name} with NumericDomain");

        return new NumericDomain(Type, _intervals.Intersect(otherNumeric._intervals), _universeMin, _universeMax);
    }

    public IValueDomain Complement()
    {
        return new NumericDomain(Type, _intervals.Complement(_universeMin, _universeMax), _universeMin, _universeMax);
    }

    public ImmutableArray<IValueDomain> Split()
    {
        if (_intervals.IsEmpty)
            return ImmutableArray<IValueDomain>.Empty;

        // Collect all boundary points from current intervals.
        // Each interval endpoint (except the universe bounds themselves) is a potential split point.
        var boundaryBuilder = ImmutableArray.CreateBuilder<double>();
        foreach (var iv in _intervals.Intervals)
        {
            // Add lo and hi as boundaries, but skip universe extremes — they don't create useful splits
            if (iv.Lo > _universeMin)
                boundaryBuilder.Add(iv.Lo);
            if (iv.Hi < _universeMax)
                boundaryBuilder.Add(iv.Hi);
        }

        // Deduplicate
        var seen = new System.Collections.Generic.HashSet<double>();
        var uniqueBuilder = ImmutableArray.CreateBuilder<double>();
        foreach (var b in boundaryBuilder)
        {
            if (seen.Add(b))
                uniqueBuilder.Add(b);
        }

        var boundaries = uniqueBuilder.ToImmutable();

        if (boundaries.IsDefaultOrEmpty)
            return ImmutableArray.Create<IValueDomain>(this);

        var partitions = _intervals.SplitByBoundaries(boundaries);
        var result = ImmutableArray.CreateBuilder<IValueDomain>(partitions.Length);
        foreach (var partition in partitions)
        {
            if (!partition.IsEmpty)
                result.Add(new NumericDomain(Type, partition, _universeMin, _universeMax));
        }

        return result.ToImmutable();
    }

    static (double min, double max) GetBounds(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Byte    => (byte.MinValue, byte.MaxValue),
            SpecialType.System_SByte   => (sbyte.MinValue, sbyte.MaxValue),
            SpecialType.System_Int16   => (short.MinValue, short.MaxValue),
            SpecialType.System_UInt16  => (ushort.MinValue, ushort.MaxValue),
            SpecialType.System_Int32   => (int.MinValue, int.MaxValue),
            SpecialType.System_UInt32  => (uint.MinValue, uint.MaxValue),
            SpecialType.System_Int64   => (long.MinValue, long.MaxValue),
            SpecialType.System_UInt64  => (ulong.MinValue, ulong.MaxValue),
            SpecialType.System_Single  => (float.MinValue, float.MaxValue),
            SpecialType.System_Double  => (double.MinValue, double.MaxValue),
            SpecialType.System_Decimal => ((double)decimal.MinValue, (double)decimal.MaxValue),
            _ => throw new ArgumentException($"Unsupported numeric type: {type.ToDisplayString()}")
        };
    }
}
