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
        _intervals = IsIntegralType(type) ? NormalizeIntegral(intervals) : intervals;
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

    /// Returns true for integer types where intervals should be normalized to whole numbers.
    static bool IsIntegralType(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Byte   => true,
        SpecialType.System_SByte  => true,
        SpecialType.System_Int16  => true,
        SpecialType.System_UInt16 => true,
        SpecialType.System_Int32  => true,
        SpecialType.System_UInt32 => true,
        SpecialType.System_Int64  => true,
        SpecialType.System_UInt64 => true,
        _                         => false,
    };

    /// Normalize intervals for integer types.
    /// For integers, open bounds are tightened to the next integer:
    ///   (a, b) → [ceil(a+eps), floor(b-eps)] = [floor(a)+1, ceil(b)-1] for non-integer bounds
    ///   (3, 7) → [4, 6],  (-1, 1) → [0, 0],  (-1, 0) → empty
    static IntervalSet NormalizeIntegral(IntervalSet intervals)
    {
        if (intervals.IsEmpty)
            return intervals;

        var builder = ImmutableArray.CreateBuilder<Interval>();

        foreach (var iv in intervals.Intervals)
        {
            if (iv.IsEmpty)
                continue;

            // Tighten lo bound: if exclusive, round up to next integer
            double lo = iv.Lo;
            bool loInc = iv.LoInclusive;
            if (!loInc)
            {
                lo = Math.Floor(lo) + 1;
                loInc = true;
            }
            else
            {
                // If lo is inclusive but not a whole number, round up
                lo = Math.Ceiling(lo);
            }

            // Tighten hi bound: if exclusive, round down to previous integer
            double hi = iv.Hi;
            bool hiInc = iv.HiInclusive;
            if (!hiInc)
            {
                hi = Math.Ceiling(hi) - 1;
                hiInc = true;
            }
            else
            {
                // If hi is inclusive but not a whole number, round down
                hi = Math.Floor(hi);
            }

            var normalized = new Interval(lo, hi, loInc, hiInc);
            if (!normalized.IsEmpty)
                builder.Add(normalized);
        }

        if (builder.Count == 0)
            return IntervalSet.Empty;

        // Re-wrap without re-normalizing (intervals are already sorted/disjoint from the source)
        return IntervalSet.FromIntervals(builder.ToImmutable());
    }
}
