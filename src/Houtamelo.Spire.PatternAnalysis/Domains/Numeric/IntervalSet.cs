using System;
using System.Collections.Immutable;
using System.Linq;

namespace Houtamelo.Spire.PatternAnalysis.Domains.Numeric;

/// Immutable set of sorted, non-overlapping, normalized intervals.
internal readonly struct IntervalSet
{
    public ImmutableArray<Interval> Intervals { get; }

    IntervalSet(ImmutableArray<Interval> intervals)
    {
        Intervals = intervals;
    }

    /// Empty set containing no intervals.
    public static IntervalSet Empty => new(ImmutableArray<Interval>.Empty);

    /// Full universe from min to max (inclusive).
    public static IntervalSet Universe(double min, double max) =>
        Single(Interval.Universe(min, max));

    /// Creates a set from a single interval. Returns empty if the interval is empty.
    public static IntervalSet Single(Interval interval) =>
        interval.IsEmpty ? Empty : new IntervalSet(ImmutableArray.Create(interval));

    /// True when no intervals remain.
    public bool IsEmpty => Intervals.IsDefaultOrEmpty;

    /// True when this set covers the entire [min, max] range.
    public bool CoversAll(double min, double max)
    {
        if (IsEmpty)
            return false;

        // Check that the first interval starts at or before min (inclusive),
        // the last interval ends at or after max (inclusive),
        // and there are no gaps between consecutive intervals.
        var first = Intervals[0];
        if (first.Lo > min || (!first.LoInclusive && first.Lo == min))
            return false;

        var last = Intervals[Intervals.Length - 1];
        if (last.Hi < max || (!last.HiInclusive && last.Hi == max))
            return false;

        // Check for gaps between adjacent intervals
        for (int i = 0; i < Intervals.Length - 1; i++)
        {
            var current = Intervals[i];
            var next = Intervals[i + 1];

            // There's a gap if current.Hi < next.Lo
            if (current.Hi < next.Lo)
                return false;

            // If they meet at the same point, at least one must include it
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (current.Hi == next.Lo && !current.HiInclusive && !next.LoInclusive)
                return false;
        }

        return true;
    }

    /// Merge two sets, producing a normalized union.
    public IntervalSet Union(IntervalSet other)
    {
        if (IsEmpty)
            return other;
        if (other.IsEmpty)
            return this;

        return Normalize(Intervals.AddRange(other.Intervals));
    }

    /// Remove intervals covered by other from this set.
    public IntervalSet Subtract(IntervalSet other)
    {
        if (IsEmpty || other.IsEmpty)
            return this;

        var result = ImmutableArray.CreateBuilder<Interval>();

        foreach (var mine in Intervals)
        {
            SubtractFromInterval(mine, other.Intervals, result);
        }

        return FromNormalized(result.ToImmutable());
    }

    /// Values present in both this and other.
    public IntervalSet Intersect(IntervalSet other)
    {
        if (IsEmpty || other.IsEmpty)
            return Empty;

        var result = ImmutableArray.CreateBuilder<Interval>();

        int i = 0, j = 0;
        while (i < Intervals.Length && j < other.Intervals.Length)
        {
            var a = Intervals[i];
            var b = other.Intervals[j];

            var intersection = IntersectTwo(a, b);
            if (!intersection.IsEmpty)
            {
                result.Add(intersection);
            }

            // Advance the interval that ends first
            if (a.Hi < b.Hi || (a.Hi == b.Hi && !a.HiInclusive && b.HiInclusive))
                i++;
            else
                j++;
        }

        return FromNormalized(result.ToImmutable());
    }

    /// Invert this set within the given universe bounds.
    public IntervalSet Complement(double universeMin, double universeMax)
    {
        var universe = Universe(universeMin, universeMax);
        return universe.Subtract(this);
    }

    /// Split this set into partitions at the given boundary points.
    /// Each boundary B produces a split: everything <= B and everything > B.
    /// N boundaries produce N+1 partitions.
    public ImmutableArray<IntervalSet> SplitByBoundaries(ImmutableArray<double> boundaries)
    {
        if (boundaries.IsDefaultOrEmpty)
            return ImmutableArray.Create(this);

        // Sort boundaries
        var sorted = boundaries.Sort();

        var result = ImmutableArray.CreateBuilder<IntervalSet>(sorted.Length + 1);
        var remaining = this;

        for (int i = 0; i < sorted.Length; i++)
        {
            double b = sorted[i];
            // Left partition: everything <= b
            var leftMask = Single(new Interval(double.NegativeInfinity, b, true, true));
            var left = remaining.Intersect(leftMask);
            result.Add(left);

            // Remaining: everything > b
            var rightMask = Single(new Interval(b, double.PositiveInfinity, false, true));
            remaining = remaining.Intersect(rightMask);
        }

        result.Add(remaining);
        return result.MoveToImmutable();
    }

    // ─── Internal helpers ─────────────────────────────────────────────

    /// Normalize: sort by lo, merge overlapping/adjacent intervals.
    static IntervalSet Normalize(ImmutableArray<Interval> intervals)
    {
        if (intervals.IsDefaultOrEmpty)
            return Empty;

        // Filter empty intervals and sort by lo
        var sorted = intervals
            .Where(iv => !iv.IsEmpty)
            .OrderBy(iv => iv.Lo)
            .ThenByDescending(iv => iv.LoInclusive) // inclusive first at same lo
            .ToArray();

        if (sorted.Length == 0)
            return Empty;

        var merged = ImmutableArray.CreateBuilder<Interval>();
        var current = sorted[0];

        for (int i = 1; i < sorted.Length; i++)
        {
            var next = sorted[i];

            if (CanMerge(current, next))
            {
                current = Merge(current, next);
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return new IntervalSet(merged.ToImmutable());
    }

    /// Two intervals can merge if they overlap or are adjacent (touching).
    static bool CanMerge(Interval a, Interval b)
    {
        // a is before b in sorted order (a.Lo <= b.Lo)
        if (a.Hi < b.Lo)
            return false;

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (a.Hi == b.Lo)
        {
            // They touch at a single point — mergeable if at least one includes that point
            return a.HiInclusive || b.LoInclusive;
        }

        // a.Hi > b.Lo → they overlap
        return true;
    }

    /// Merge two overlapping/adjacent intervals into one.
    static Interval Merge(Interval a, Interval b)
    {
        double lo;
        bool loInc;

        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (a.Lo < b.Lo)
        {
            lo = a.Lo;
            loInc = a.LoInclusive;
        }
        else if (a.Lo == b.Lo)
        {
            lo = a.Lo;
            loInc = a.LoInclusive || b.LoInclusive;
        }
        else
        {
            lo = b.Lo;
            loInc = b.LoInclusive;
        }

        double hi;
        bool hiInc;

        if (a.Hi > b.Hi)
        {
            hi = a.Hi;
            hiInc = a.HiInclusive;
        }
        else if (a.Hi == b.Hi)
        {
            hi = a.Hi;
            hiInc = a.HiInclusive || b.HiInclusive;
        }
        else
        {
            hi = b.Hi;
            hiInc = b.HiInclusive;
        }
        // ReSharper restore CompareOfFloatsByEqualityOperator

        return new Interval(lo, hi, loInc, hiInc);
    }

    /// Compute a ∩ b for two individual intervals.
    static Interval IntersectTwo(Interval a, Interval b)
    {
        double lo;
        bool loInc;

        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (a.Lo > b.Lo)
        {
            lo = a.Lo;
            loInc = a.LoInclusive;
        }
        else if (a.Lo == b.Lo)
        {
            lo = a.Lo;
            loInc = a.LoInclusive && b.LoInclusive;
        }
        else
        {
            lo = b.Lo;
            loInc = b.LoInclusive;
        }

        double hi;
        bool hiInc;

        if (a.Hi < b.Hi)
        {
            hi = a.Hi;
            hiInc = a.HiInclusive;
        }
        else if (a.Hi == b.Hi)
        {
            hi = a.Hi;
            hiInc = a.HiInclusive && b.HiInclusive;
        }
        else
        {
            hi = b.Hi;
            hiInc = b.HiInclusive;
        }
        // ReSharper restore CompareOfFloatsByEqualityOperator

        return new Interval(lo, hi, loInc, hiInc);
    }

    /// Subtract all intervals in `toSubtract` from a single interval, adding results to builder.
    static void SubtractFromInterval(
        Interval source,
        ImmutableArray<Interval> toSubtract,
        ImmutableArray<Interval>.Builder result)
    {
        var remaining = source;

        foreach (var sub in toSubtract)
        {
            if (remaining.IsEmpty)
                return;

            // No overlap — skip
            if (!remaining.Overlaps(sub))
                continue;

            // Sub covers entire remaining → nothing left
            if (sub.Lo <= remaining.Lo && sub.Hi >= remaining.Hi)
            {
                // Check inclusiveness at boundaries
                // ReSharper disable CompareOfFloatsByEqualityOperator
                bool coversLo = sub.Lo < remaining.Lo ||
                                (sub.Lo == remaining.Lo && (sub.LoInclusive || !remaining.LoInclusive));
                bool coversHi = sub.Hi > remaining.Hi ||
                                (sub.Hi == remaining.Hi && (sub.HiInclusive || !remaining.HiInclusive));
                // ReSharper restore CompareOfFloatsByEqualityOperator

                if (coversLo && coversHi)
                {
                    remaining = Interval.Empty;
                    return;
                }
            }

            // Sub is entirely contained or partially overlaps

            // Left fragment: [remaining.Lo, sub.Lo) if sub starts after remaining
            if (sub.Lo > remaining.Lo || (sub.Lo == remaining.Lo && !sub.LoInclusive && remaining.LoInclusive))
            {
                var left = new Interval(
                    remaining.Lo,
                    sub.Lo,
                    remaining.LoInclusive,
                    !sub.LoInclusive);
                if (!left.IsEmpty)
                    result.Add(left);
            }

            // Update remaining to be the right portion: (sub.Hi, remaining.Hi]
            if (sub.Hi < remaining.Hi || (sub.Hi == remaining.Hi && !sub.HiInclusive && remaining.HiInclusive))
            {
                remaining = new Interval(
                    sub.Hi,
                    remaining.Hi,
                    !sub.HiInclusive,
                    remaining.HiInclusive);
            }
            else
            {
                // Sub covers everything to the right
                remaining = Interval.Empty;
                return;
            }
        }

        if (!remaining.IsEmpty)
            result.Add(remaining);
    }

    /// Wrap already-sorted, non-overlapping intervals (skip normalization).
    static IntervalSet FromNormalized(ImmutableArray<Interval> intervals)
    {
        if (intervals.IsDefaultOrEmpty)
            return Empty;

        // Filter empty intervals
        var builder = ImmutableArray.CreateBuilder<Interval>();
        foreach (var iv in intervals)
        {
            if (!iv.IsEmpty)
                builder.Add(iv);
        }

        return builder.Count == 0 ? Empty : new IntervalSet(builder.ToImmutable());
    }
}
