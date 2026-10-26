using System.Collections.Immutable;
using System.Linq;
using System.Numerics;

namespace Houtamelo.Spire.PatternAnalysis.Domains.Numeric;

/// Immutable set of sorted, non-overlapping, canonical closed integral ranges.
///
/// IntegralInterval retains open bounds at the construction boundary, while
/// this type turns them into exact closed ranges. This makes adjacency (for
/// example, [1, 3] followed by [4, 5]) explicit without introducing a
/// floating-point sentinel or losing an endpoint at a machine-width extreme.
internal readonly struct IntegralIntervalSet
{
    public ImmutableArray<IntegralInterval> Intervals { get; }

    IntegralIntervalSet(ImmutableArray<IntegralInterval> intervals)
    {
        Intervals = intervals;
    }

    /// Empty set containing no intervals.
    public static IntegralIntervalSet Empty =>
        new(ImmutableArray<IntegralInterval>.Empty);

    /// Full universe from min to max (inclusive).
    public static IntegralIntervalSet Universe(BigInteger min, BigInteger max) =>
        Single(IntegralInterval.Universe(min, max));

    /// Creates a set from one interval, canonicalizing it to a closed range.
    public static IntegralIntervalSet Single(IntegralInterval interval)
    {
        if (!TryCanonicalize(interval, out var canonical))
            return Empty;

        return new IntegralIntervalSet(ImmutableArray.Create(canonical));
    }

    /// Creates a normalized set from arbitrary intervals.
    public static IntegralIntervalSet FromIntervals(ImmutableArray<IntegralInterval> intervals)
    {
        if (intervals.IsDefaultOrEmpty)
            return Empty;

        return Normalize(intervals);
    }

    public bool IsEmpty => Intervals.IsDefaultOrEmpty;

    /// True when this set covers every integral value in [min, max].
    public bool CoversAll(BigInteger min, BigInteger max)
    {
        if (IsEmpty || min > max)
            return false;

        var first = Intervals[0];
        if (first.Lo > min)
            return false;

        var last = Intervals[Intervals.Length - 1];
        if (last.Hi < max)
            return false;

        for (int i = 0; i < Intervals.Length - 1; i++)
        {
            var current = Intervals[i];
            var next = Intervals[i + 1];
            if (current.Hi + BigInteger.One < next.Lo)
                return false;
        }

        return true;
    }

    /// Merge two sets, producing a normalized union.
    public IntegralIntervalSet Union(IntegralIntervalSet other)
    {
        if (IsEmpty)
            return other;
        if (other.IsEmpty)
            return this;

        return Normalize(Intervals.AddRange(other.Intervals));
    }

    /// Remove intervals covered by other from this set.
    public IntegralIntervalSet Subtract(IntegralIntervalSet other)
    {
        if (IsEmpty || other.IsEmpty)
            return this;

        var result = ImmutableArray.CreateBuilder<IntegralInterval>();
        foreach (var source in Intervals)
            SubtractFromInterval(source, other.Intervals, result);

        return FromNormalized(result.ToImmutable());
    }

    /// Values present in both this and other.
    public IntegralIntervalSet Intersect(IntegralIntervalSet other)
    {
        if (IsEmpty || other.IsEmpty)
            return Empty;

        var result = ImmutableArray.CreateBuilder<IntegralInterval>();
        int i = 0;
        int j = 0;

        while (i < Intervals.Length && j < other.Intervals.Length)
        {
            var left = Intervals[i];
            var right = other.Intervals[j];

            var lo = BigInteger.Max(left.Lo, right.Lo);
            var hi = BigInteger.Min(left.Hi, right.Hi);
            if (lo <= hi)
                result.Add(new IntegralInterval(lo, hi, loInclusive: true, hiInclusive: true));

            if (left.Hi < right.Hi)
                i++;
            else
                j++;
        }

        return FromNormalized(result.ToImmutable());
    }

    /// Invert this set within the given exact universe bounds.
    public IntegralIntervalSet Complement(BigInteger universeMin, BigInteger universeMax)
    {
        return Universe(universeMin, universeMax).Subtract(this);
    }

    /// Split using the set's first and last endpoints as its effective universe.
    /// NumericDomain uses the overload with explicit type bounds; this overload
    /// keeps the interval-set API convenient for focused unit tests.
    public ImmutableArray<IntegralIntervalSet> SplitByBoundaries(
        ImmutableArray<BigInteger> boundaries)
    {
        if (IsEmpty)
        {
            var emptyPartitions = ImmutableArray.CreateBuilder<IntegralIntervalSet>(
                boundaries.IsDefault ? 1 : boundaries.Length + 1);
            int count = boundaries.IsDefault ? 1 : boundaries.Length + 1;
            for (int i = 0; i < count; i++)
                emptyPartitions.Add(Empty);
            return emptyPartitions.MoveToImmutable();
        }

        return SplitByBoundaries(
            boundaries,
            Intervals[0].Lo,
            Intervals[Intervals.Length - 1].Hi);
    }

    /// Split this set into N+1 discrete partitions. Each boundary B produces
    /// a left partition <= B and a right partition >= B+1.
    public ImmutableArray<IntegralIntervalSet> SplitByBoundaries(
        ImmutableArray<BigInteger> boundaries,
        BigInteger universeMin,
        BigInteger universeMax)
    {
        if (boundaries.IsDefaultOrEmpty)
            return ImmutableArray.Create(this);

        var sorted = boundaries
            .Distinct()
            .OrderBy(value => value)
            .ToImmutableArray();

        var result = ImmutableArray.CreateBuilder<IntegralIntervalSet>(sorted.Length + 1);
        var remaining = this;

        foreach (var boundary in sorted)
        {
            // The masks are bounded by the exact source universe. In
            // particular, no artificial BigInteger infinity can overflow or
            // create a phantom value beside ulong.MaxValue.
            var leftMask = Single(new IntegralInterval(
                universeMin,
                boundary,
                loInclusive: true,
                hiInclusive: true));
            result.Add(remaining.Intersect(leftMask));

            var rightMask = Single(new IntegralInterval(
                boundary + BigInteger.One,
                universeMax,
                loInclusive: true,
                hiInclusive: true));
            remaining = remaining.Intersect(rightMask);
        }

        result.Add(remaining);
        return result.MoveToImmutable();
    }

    static bool TryCanonicalize(IntegralInterval interval, out IntegralInterval canonical)
    {
        canonical = IntegralInterval.Empty;
        if (interval.IsEmpty)
            return false;

        var lo = interval.LoInclusive ? interval.Lo : interval.Lo + BigInteger.One;
        var hi = interval.HiInclusive ? interval.Hi : interval.Hi - BigInteger.One;
        if (lo > hi)
            return false;

        canonical = new IntegralInterval(lo, hi, loInclusive: true, hiInclusive: true);
        return true;
    }

    static IntegralIntervalSet Normalize(ImmutableArray<IntegralInterval> intervals)
    {
        var canonical = ImmutableArray.CreateBuilder<IntegralInterval>();
        foreach (var interval in intervals)
        {
            if (TryCanonicalize(interval, out var value))
                canonical.Add(value);
        }

        if (canonical.Count == 0)
            return Empty;

        var sorted = canonical
            .OrderBy(interval => interval.Lo)
            .ToArray();

        var merged = ImmutableArray.CreateBuilder<IntegralInterval>();
        var current = sorted[0];

        for (int i = 1; i < sorted.Length; i++)
        {
            var next = sorted[i];
            if (current.Hi + BigInteger.One >= next.Lo)
            {
                if (next.Hi > current.Hi)
                {
                    current = new IntegralInterval(
                        current.Lo,
                        next.Hi,
                        loInclusive: true,
                        hiInclusive: true);
                }
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return new IntegralIntervalSet(merged.ToImmutable());
    }

    static void SubtractFromInterval(
        IntegralInterval source,
        ImmutableArray<IntegralInterval> toSubtract,
        ImmutableArray<IntegralInterval>.Builder result)
    {
        if (source.IsEmpty)
            return;

        var cursor = source.Lo;
        foreach (var sub in toSubtract)
        {
            if (sub.Hi < cursor)
                continue;
            if (sub.Lo > source.Hi)
                break;

            if (sub.Lo > cursor)
            {
                var leftHi = BigInteger.Min(source.Hi, sub.Lo - BigInteger.One);
                if (cursor <= leftHi)
                {
                    result.Add(new IntegralInterval(
                        cursor,
                        leftHi,
                        loInclusive: true,
                        hiInclusive: true));
                }
            }

            if (sub.Hi >= source.Hi)
            {
                cursor = source.Hi + BigInteger.One;
                break;
            }

            var nextCursor = sub.Hi + BigInteger.One;
            if (nextCursor > cursor)
                cursor = nextCursor;
        }

        if (cursor <= source.Hi)
        {
            result.Add(new IntegralInterval(
                cursor,
                source.Hi,
                loInclusive: true,
                hiInclusive: true));
        }
    }

    static IntegralIntervalSet FromNormalized(ImmutableArray<IntegralInterval> intervals)
    {
        if (intervals.IsDefaultOrEmpty)
            return Empty;

        var builder = ImmutableArray.CreateBuilder<IntegralInterval>();
        foreach (var interval in intervals)
        {
            if (TryCanonicalize(interval, out var canonical))
                builder.Add(canonical);
        }

        return builder.Count == 0
            ? Empty
            : new IntegralIntervalSet(builder.ToImmutable());
    }
}
