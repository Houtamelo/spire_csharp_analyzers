using System;

namespace Houtamelo.Spire.PatternAnalysis.Domains.Numeric;

/// Immutable interval representing a single (lo, hi) range with inclusive/exclusive bounds.
internal readonly struct Interval : IEquatable<Interval>
{
    public double Lo { get; }
    public double Hi { get; }
    public bool LoInclusive { get; }
    public bool HiInclusive { get; }

    public Interval(double lo, double hi, bool loInclusive, bool hiInclusive)
    {
        Lo = lo;
        Hi = hi;
        LoInclusive = loInclusive;
        HiInclusive = hiInclusive;
    }

    /// True when the interval represents no values.
    public bool IsEmpty
    {
        get
        {
            if (Lo > Hi)
                return true;

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (Lo == Hi)
                return !LoInclusive || !HiInclusive;

            return false;
        }
    }

    /// Canonical empty interval.
    public static Interval Empty => new(1.0, 0.0, false, false);

    /// Full range with inclusive bounds.
    public static Interval Universe(double min, double max) => new(min, max, true, true);

    /// True if the value falls within this interval, respecting bound types.
    public bool Contains(double value)
    {
        if (IsEmpty)
            return false;

        bool aboveLo = LoInclusive ? value >= Lo : value > Lo;
        bool belowHi = HiInclusive ? value <= Hi : value < Hi;
        return aboveLo && belowHi;
    }

    /// True if this interval and other share at least one value.
    public bool Overlaps(Interval other)
    {
        if (IsEmpty || other.IsEmpty)
            return false;

        // Check if this is entirely before other
        if (Lo > other.Hi || other.Lo > Hi)
            return false;

        // ReSharper disable CompareOfFloatsByEqualityOperator

        // Check the boundary cases where Lo == other.Hi or Hi == other.Lo
        if (Hi == other.Lo)
            return HiInclusive && other.LoInclusive;

        if (Lo == other.Hi)
            return LoInclusive && other.HiInclusive;

        // ReSharper restore CompareOfFloatsByEqualityOperator

        return true;
    }

    public bool Equals(Interval other) =>
        Lo.Equals(other.Lo) &&
        Hi.Equals(other.Hi) &&
        LoInclusive == other.LoInclusive &&
        HiInclusive == other.HiInclusive;

    public override bool Equals(object? obj) => obj is Interval other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Lo.GetHashCode();
            hash = hash * 397 ^ Hi.GetHashCode();
            hash = hash * 397 ^ LoInclusive.GetHashCode();
            hash = hash * 397 ^ HiInclusive.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(Interval left, Interval right) => left.Equals(right);
    public static bool operator !=(Interval left, Interval right) => !left.Equals(right);

    public override string ToString()
    {
        if (IsEmpty)
            return "{}";

        char lo = LoInclusive ? '[' : '(';
        char hi = HiInclusive ? ']' : ')';
        return $"{lo}{Lo}, {Hi}{hi}";
    }
}
