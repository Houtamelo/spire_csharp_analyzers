using System;
using System.Numerics;

namespace Houtamelo.Spire.PatternAnalysis.Domains.Numeric;

/// Immutable interval over integral values. The endpoints remain exact and
/// retain their construction-time inclusivity until an interval set
/// canonicalizes them to a closed range.
internal readonly struct IntegralInterval : IEquatable<IntegralInterval>
{
    public BigInteger Lo { get; }
    public BigInteger Hi { get; }
    public bool LoInclusive { get; }
    public bool HiInclusive { get; }

    public IntegralInterval(
        BigInteger lo,
        BigInteger hi,
        bool loInclusive,
        bool hiInclusive)
    {
        Lo = lo;
        Hi = hi;
        LoInclusive = loInclusive;
        HiInclusive = hiInclusive;
    }

    /// True when the interval represents no integral values.
    public bool IsEmpty
    {
        get
        {
            var effectiveLo = LoInclusive ? Lo : Lo + BigInteger.One;
            var effectiveHi = HiInclusive ? Hi : Hi - BigInteger.One;
            return effectiveLo > effectiveHi;
        }
    }

    /// Canonical empty interval.
    public static IntegralInterval Empty =>
        new(BigInteger.One, BigInteger.Zero, loInclusive: false, hiInclusive: false);

    /// Full range with inclusive bounds.
    public static IntegralInterval Universe(BigInteger min, BigInteger max) =>
        new(min, max, loInclusive: true, hiInclusive: true);

    /// True if the exact integral value falls within this interval.
    public bool Contains(BigInteger value)
    {
        if (IsEmpty)
            return false;

        bool aboveLo = LoInclusive ? value >= Lo : value > Lo;
        bool belowHi = HiInclusive ? value <= Hi : value < Hi;
        return aboveLo && belowHi;
    }

    /// True if this interval and other share at least one integral value.
    public bool Overlaps(IntegralInterval other)
    {
        if (IsEmpty || other.IsEmpty)
            return false;

        var thisLo = LoInclusive ? Lo : Lo + BigInteger.One;
        var thisHi = HiInclusive ? Hi : Hi - BigInteger.One;
        var otherLo = other.LoInclusive ? other.Lo : other.Lo + BigInteger.One;
        var otherHi = other.HiInclusive ? other.Hi : other.Hi - BigInteger.One;

        return BigInteger.Max(thisLo, otherLo) <= BigInteger.Min(thisHi, otherHi);
    }

    public bool Equals(IntegralInterval other) =>
        Lo.Equals(other.Lo) &&
        Hi.Equals(other.Hi) &&
        LoInclusive == other.LoInclusive &&
        HiInclusive == other.HiInclusive;

    public override bool Equals(object? obj) =>
        obj is IntegralInterval other && Equals(other);

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

    public static bool operator ==(IntegralInterval left, IntegralInterval right) =>
        left.Equals(right);

    public static bool operator !=(IntegralInterval left, IntegralInterval right) =>
        !left.Equals(right);

    public override string ToString()
    {
        if (IsEmpty)
            return "{}";

        char lo = LoInclusive ? '[' : '(';
        char hi = HiInclusive ? ']' : ')';
        return $"{lo}{Lo}, {Hi}{hi}";
    }
}
