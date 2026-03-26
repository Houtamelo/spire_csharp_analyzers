using Houtamelo.Spire.PatternAnalysis.Domains.Numeric;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Domains.Numeric;

public class IntervalTests
{
    // ─── Construction & IsEmpty ───────────────────────────────────────

    [Fact]
    public void Inclusive_bounds_are_stored_correctly()
    {
        var iv = new Interval(1.0, 5.0, loInclusive: true, hiInclusive: true);
        Assert.Equal(1.0, iv.Lo);
        Assert.Equal(5.0, iv.Hi);
        Assert.True(iv.LoInclusive);
        Assert.True(iv.HiInclusive);
        Assert.False(iv.IsEmpty);
    }

    [Fact]
    public void Exclusive_bounds_are_stored_correctly()
    {
        var iv = new Interval(1.0, 5.0, loInclusive: false, hiInclusive: false);
        Assert.Equal(1.0, iv.Lo);
        Assert.Equal(5.0, iv.Hi);
        Assert.False(iv.LoInclusive);
        Assert.False(iv.HiInclusive);
        Assert.False(iv.IsEmpty);
    }

    [Fact]
    public void Lo_greater_than_hi_is_empty()
    {
        var iv = new Interval(5.0, 1.0, loInclusive: true, hiInclusive: true);
        Assert.True(iv.IsEmpty);
    }

    [Fact]
    public void Equal_bounds_inclusive_is_not_empty()
    {
        // [3, 3] contains exactly {3}
        var iv = new Interval(3.0, 3.0, loInclusive: true, hiInclusive: true);
        Assert.False(iv.IsEmpty);
    }

    [Fact]
    public void Equal_bounds_lo_exclusive_is_empty()
    {
        // (3, 3] is empty
        var iv = new Interval(3.0, 3.0, loInclusive: false, hiInclusive: true);
        Assert.True(iv.IsEmpty);
    }

    [Fact]
    public void Equal_bounds_hi_exclusive_is_empty()
    {
        // [3, 3) is empty
        var iv = new Interval(3.0, 3.0, loInclusive: true, hiInclusive: false);
        Assert.True(iv.IsEmpty);
    }

    [Fact]
    public void Equal_bounds_both_exclusive_is_empty()
    {
        // (3, 3) is empty
        var iv = new Interval(3.0, 3.0, loInclusive: false, hiInclusive: false);
        Assert.True(iv.IsEmpty);
    }

    [Fact]
    public void Static_Empty_is_empty()
    {
        Assert.True(Interval.Empty.IsEmpty);
    }

    [Fact]
    public void Universe_is_not_empty()
    {
        var iv = Interval.Universe(double.MinValue, double.MaxValue);
        Assert.False(iv.IsEmpty);
        Assert.True(iv.LoInclusive);
        Assert.True(iv.HiInclusive);
    }

    // ─── Contains ─────────────────────────────────────────────────────

    [Fact]
    public void Contains_interior_value()
    {
        var iv = new Interval(1.0, 5.0, loInclusive: true, hiInclusive: true);
        Assert.True(iv.Contains(3.0));
    }

    [Fact]
    public void Contains_lo_inclusive()
    {
        var iv = new Interval(1.0, 5.0, loInclusive: true, hiInclusive: true);
        Assert.True(iv.Contains(1.0));
    }

    [Fact]
    public void Does_not_contain_lo_exclusive()
    {
        var iv = new Interval(1.0, 5.0, loInclusive: false, hiInclusive: true);
        Assert.False(iv.Contains(1.0));
    }

    [Fact]
    public void Contains_hi_inclusive()
    {
        var iv = new Interval(1.0, 5.0, loInclusive: true, hiInclusive: true);
        Assert.True(iv.Contains(5.0));
    }

    [Fact]
    public void Does_not_contain_hi_exclusive()
    {
        var iv = new Interval(1.0, 5.0, loInclusive: true, hiInclusive: false);
        Assert.False(iv.Contains(5.0));
    }

    [Fact]
    public void Does_not_contain_below_lo()
    {
        var iv = new Interval(1.0, 5.0, loInclusive: true, hiInclusive: true);
        Assert.False(iv.Contains(0.5));
    }

    [Fact]
    public void Does_not_contain_above_hi()
    {
        var iv = new Interval(1.0, 5.0, loInclusive: true, hiInclusive: true);
        Assert.False(iv.Contains(5.5));
    }

    [Fact]
    public void Empty_interval_contains_nothing()
    {
        Assert.False(Interval.Empty.Contains(0.0));
        Assert.False(Interval.Empty.Contains(double.NaN));
    }

    [Fact]
    public void Singleton_inclusive_interval_contains_its_value()
    {
        var iv = new Interval(7.0, 7.0, loInclusive: true, hiInclusive: true);
        Assert.True(iv.Contains(7.0));
        Assert.False(iv.Contains(7.1));
        Assert.False(iv.Contains(6.9));
    }

    // ─── Overlaps ─────────────────────────────────────────────────────

    [Fact]
    public void Overlapping_intervals_detected()
    {
        var a = new Interval(1.0, 5.0, true, true);
        var b = new Interval(3.0, 8.0, true, true);
        Assert.True(a.Overlaps(b));
        Assert.True(b.Overlaps(a));
    }

    [Fact]
    public void Disjoint_intervals_do_not_overlap()
    {
        var a = new Interval(1.0, 3.0, true, true);
        var b = new Interval(5.0, 8.0, true, true);
        Assert.False(a.Overlaps(b));
        Assert.False(b.Overlaps(a));
    }

    [Fact]
    public void Adjacent_inclusive_bounds_overlap()
    {
        // [1,3] and [3,5] share the point 3
        var a = new Interval(1.0, 3.0, true, true);
        var b = new Interval(3.0, 5.0, true, true);
        Assert.True(a.Overlaps(b));
        Assert.True(b.Overlaps(a));
    }

    [Fact]
    public void Adjacent_exclusive_bounds_do_not_overlap()
    {
        // [1,3) and (3,5] share no points
        var a = new Interval(1.0, 3.0, true, false);
        var b = new Interval(3.0, 5.0, false, true);
        Assert.False(a.Overlaps(b));
        Assert.False(b.Overlaps(a));
    }

    [Fact]
    public void Adjacent_one_inclusive_one_exclusive_do_not_overlap()
    {
        // [1,3) and [3,5] — a does not include 3, b does, but a.Hi==b.Lo with a.HiInclusive=false
        var a = new Interval(1.0, 3.0, true, false);
        var b = new Interval(3.0, 5.0, true, true);
        Assert.False(a.Overlaps(b));
        Assert.False(b.Overlaps(a));
    }

    [Fact]
    public void Empty_interval_does_not_overlap_anything()
    {
        var a = Interval.Empty;
        var b = new Interval(1.0, 5.0, true, true);
        Assert.False(a.Overlaps(b));
        Assert.False(b.Overlaps(a));
    }

    [Fact]
    public void Contained_intervals_overlap()
    {
        var outer = new Interval(1.0, 10.0, true, true);
        var inner = new Interval(3.0, 7.0, true, true);
        Assert.True(outer.Overlaps(inner));
        Assert.True(inner.Overlaps(outer));
    }

    // ─── Equals / GetHashCode ─────────────────────────────────────────

    [Fact]
    public void Equal_intervals_are_equal()
    {
        var a = new Interval(1.0, 5.0, true, false);
        var b = new Interval(1.0, 5.0, true, false);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Different_bounds_not_equal()
    {
        var a = new Interval(1.0, 5.0, true, true);
        var b = new Interval(1.0, 6.0, true, true);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Different_inclusivity_not_equal()
    {
        var a = new Interval(1.0, 5.0, true, true);
        var b = new Interval(1.0, 5.0, true, false);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_via_object_works()
    {
        var a = new Interval(1.0, 5.0, true, true);
        object b = new Interval(1.0, 5.0, true, true);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_null_object_returns_false()
    {
        var a = new Interval(1.0, 5.0, true, true);
        Assert.False(a.Equals(null));
    }
}
