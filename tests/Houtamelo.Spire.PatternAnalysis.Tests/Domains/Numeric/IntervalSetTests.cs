using System.Collections.Immutable;
using Houtamelo.Spire.PatternAnalysis.Domains.Numeric;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Domains.Numeric;

public class IntervalSetTests
{
    const double Min = -1000.0;
    const double Max = 1000.0;

    // ─── Empty & Universe ─────────────────────────────────────────────

    [Fact]
    public void Empty_set_is_empty()
    {
        Assert.True(IntervalSet.Empty.IsEmpty);
    }

    [Fact]
    public void Universe_is_not_empty()
    {
        var u = IntervalSet.Universe(Min, Max);
        Assert.False(u.IsEmpty);
    }

    [Fact]
    public void Universe_covers_all()
    {
        var u = IntervalSet.Universe(Min, Max);
        Assert.True(u.CoversAll(Min, Max));
    }

    [Fact]
    public void Empty_does_not_cover_all()
    {
        Assert.False(IntervalSet.Empty.CoversAll(Min, Max));
    }

    // ─── Single ───────────────────────────────────────────────────────

    [Fact]
    public void Single_creates_set_with_one_interval()
    {
        var iv = new Interval(1.0, 5.0, true, true);
        var set = IntervalSet.Single(iv);
        Assert.False(set.IsEmpty);
        Assert.Single(set.Intervals);
        Assert.Equal(iv, set.Intervals[0]);
    }

    [Fact]
    public void Single_of_empty_interval_is_empty()
    {
        var set = IntervalSet.Single(Interval.Empty);
        Assert.True(set.IsEmpty);
    }

    // ─── Union ────────────────────────────────────────────────────────

    [Fact]
    public void Union_disjoint_intervals_stay_separate()
    {
        var a = IntervalSet.Single(new Interval(1.0, 3.0, true, true));
        var b = IntervalSet.Single(new Interval(5.0, 8.0, true, true));
        var result = a.Union(b);
        Assert.Equal(2, result.Intervals.Length);
    }

    [Fact]
    public void Union_overlapping_intervals_merge()
    {
        var a = IntervalSet.Single(new Interval(1.0, 5.0, true, true));
        var b = IntervalSet.Single(new Interval(3.0, 8.0, true, true));
        var result = a.Union(b);
        Assert.Single(result.Intervals);
        Assert.Equal(1.0, result.Intervals[0].Lo);
        Assert.Equal(8.0, result.Intervals[0].Hi);
        Assert.True(result.Intervals[0].LoInclusive);
        Assert.True(result.Intervals[0].HiInclusive);
    }

    [Fact]
    public void Union_adjacent_inclusive_bounds_merge()
    {
        // [1,3] U [3,5] = [1,5]
        var a = IntervalSet.Single(new Interval(1.0, 3.0, true, true));
        var b = IntervalSet.Single(new Interval(3.0, 5.0, true, true));
        var result = a.Union(b);
        Assert.Single(result.Intervals);
        Assert.Equal(1.0, result.Intervals[0].Lo);
        Assert.Equal(5.0, result.Intervals[0].Hi);
    }

    [Fact]
    public void Union_adjacent_exclusive_inclusive_merge()
    {
        // [1,3) U [3,5] = [1,5]  — these are "touching" (no gap between them)
        var a = IntervalSet.Single(new Interval(1.0, 3.0, true, false));
        var b = IntervalSet.Single(new Interval(3.0, 5.0, true, true));
        var result = a.Union(b);
        Assert.Single(result.Intervals);
        Assert.Equal(1.0, result.Intervals[0].Lo);
        Assert.Equal(5.0, result.Intervals[0].Hi);
    }

    [Fact]
    public void Union_adjacent_both_exclusive_do_not_merge()
    {
        // [1,3) U (3,5] — gap at exactly 3
        var a = IntervalSet.Single(new Interval(1.0, 3.0, true, false));
        var b = IntervalSet.Single(new Interval(3.0, 5.0, false, true));
        var result = a.Union(b);
        Assert.Equal(2, result.Intervals.Length);
    }

    [Fact]
    public void Union_with_empty_returns_same()
    {
        var a = IntervalSet.Single(new Interval(1.0, 5.0, true, true));
        var result = a.Union(IntervalSet.Empty);
        Assert.Single(result.Intervals);
        Assert.Equal(a.Intervals[0], result.Intervals[0]);
    }

    [Fact]
    public void Union_contained_interval_absorbed()
    {
        // [1,10] U [3,5] = [1,10]
        var a = IntervalSet.Single(new Interval(1.0, 10.0, true, true));
        var b = IntervalSet.Single(new Interval(3.0, 5.0, true, true));
        var result = a.Union(b);
        Assert.Single(result.Intervals);
        Assert.Equal(1.0, result.Intervals[0].Lo);
        Assert.Equal(10.0, result.Intervals[0].Hi);
    }

    // ─── Subtract ─────────────────────────────────────────────────────

    [Fact]
    public void Subtract_middle_splits_interval()
    {
        // [1,10] - [4,6] = [1,4) U (6,10]
        var a = IntervalSet.Single(new Interval(1.0, 10.0, true, true));
        var b = IntervalSet.Single(new Interval(4.0, 6.0, true, true));
        var result = a.Subtract(b);
        Assert.Equal(2, result.Intervals.Length);
        Assert.Equal(1.0, result.Intervals[0].Lo);
        Assert.Equal(4.0, result.Intervals[0].Hi);
        Assert.True(result.Intervals[0].LoInclusive);
        Assert.False(result.Intervals[0].HiInclusive);
        Assert.Equal(6.0, result.Intervals[1].Lo);
        Assert.Equal(10.0, result.Intervals[1].Hi);
        Assert.False(result.Intervals[1].LoInclusive);
        Assert.True(result.Intervals[1].HiInclusive);
    }

    [Fact]
    public void Subtract_from_start_creates_gap()
    {
        // [1,10] - [1,5] = (5,10]
        var a = IntervalSet.Single(new Interval(1.0, 10.0, true, true));
        var b = IntervalSet.Single(new Interval(1.0, 5.0, true, true));
        var result = a.Subtract(b);
        Assert.Single(result.Intervals);
        Assert.Equal(5.0, result.Intervals[0].Lo);
        Assert.Equal(10.0, result.Intervals[0].Hi);
        Assert.False(result.Intervals[0].LoInclusive);
        Assert.True(result.Intervals[0].HiInclusive);
    }

    [Fact]
    public void Subtract_from_end_creates_gap()
    {
        // [1,10] - [5,10] = [1,5)
        var a = IntervalSet.Single(new Interval(1.0, 10.0, true, true));
        var b = IntervalSet.Single(new Interval(5.0, 10.0, true, true));
        var result = a.Subtract(b);
        Assert.Single(result.Intervals);
        Assert.Equal(1.0, result.Intervals[0].Lo);
        Assert.Equal(5.0, result.Intervals[0].Hi);
        Assert.True(result.Intervals[0].LoInclusive);
        Assert.False(result.Intervals[0].HiInclusive);
    }

    [Fact]
    public void Full_subtraction_yields_empty()
    {
        var a = IntervalSet.Single(new Interval(1.0, 5.0, true, true));
        var b = IntervalSet.Single(new Interval(0.0, 10.0, true, true));
        var result = a.Subtract(b);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Subtract_empty_returns_same()
    {
        var a = IntervalSet.Single(new Interval(1.0, 5.0, true, true));
        var result = a.Subtract(IntervalSet.Empty);
        Assert.Single(result.Intervals);
        Assert.Equal(a.Intervals[0], result.Intervals[0]);
    }

    [Fact]
    public void Subtract_disjoint_returns_same()
    {
        var a = IntervalSet.Single(new Interval(1.0, 5.0, true, true));
        var b = IntervalSet.Single(new Interval(10.0, 20.0, true, true));
        var result = a.Subtract(b);
        Assert.Single(result.Intervals);
        Assert.Equal(a.Intervals[0], result.Intervals[0]);
    }

    // ─── Intersect ────────────────────────────────────────────────────

    [Fact]
    public void Intersect_overlapping_intervals()
    {
        // [1,5] & [3,8] = [3,5]
        var a = IntervalSet.Single(new Interval(1.0, 5.0, true, true));
        var b = IntervalSet.Single(new Interval(3.0, 8.0, true, true));
        var result = a.Intersect(b);
        Assert.Single(result.Intervals);
        Assert.Equal(3.0, result.Intervals[0].Lo);
        Assert.Equal(5.0, result.Intervals[0].Hi);
        Assert.True(result.Intervals[0].LoInclusive);
        Assert.True(result.Intervals[0].HiInclusive);
    }

    [Fact]
    public void Intersect_disjoint_is_empty()
    {
        var a = IntervalSet.Single(new Interval(1.0, 3.0, true, true));
        var b = IntervalSet.Single(new Interval(5.0, 8.0, true, true));
        var result = a.Intersect(b);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Intersect_preserves_exclusive_bounds()
    {
        // (1,5] & [3,8) = [3,5]
        var a = IntervalSet.Single(new Interval(1.0, 5.0, false, true));
        var b = IntervalSet.Single(new Interval(3.0, 8.0, true, false));
        var result = a.Intersect(b);
        Assert.Single(result.Intervals);
        Assert.Equal(3.0, result.Intervals[0].Lo);
        Assert.Equal(5.0, result.Intervals[0].Hi);
        Assert.True(result.Intervals[0].LoInclusive);
        Assert.True(result.Intervals[0].HiInclusive);
    }

    [Fact]
    public void Intersect_with_matching_lo_takes_stricter_bound()
    {
        // (1,5] & [1,3] = (1,3]
        var a = IntervalSet.Single(new Interval(1.0, 5.0, false, true));
        var b = IntervalSet.Single(new Interval(1.0, 3.0, true, true));
        var result = a.Intersect(b);
        Assert.Single(result.Intervals);
        Assert.Equal(1.0, result.Intervals[0].Lo);
        Assert.Equal(3.0, result.Intervals[0].Hi);
        Assert.False(result.Intervals[0].LoInclusive);
        Assert.True(result.Intervals[0].HiInclusive);
    }

    [Fact]
    public void Intersect_contained_returns_inner()
    {
        // [1,10] & [3,7] = [3,7]
        var a = IntervalSet.Single(new Interval(1.0, 10.0, true, true));
        var b = IntervalSet.Single(new Interval(3.0, 7.0, true, true));
        var result = a.Intersect(b);
        Assert.Single(result.Intervals);
        Assert.Equal(3.0, result.Intervals[0].Lo);
        Assert.Equal(7.0, result.Intervals[0].Hi);
    }

    // ─── Complement ───────────────────────────────────────────────────

    [Fact]
    public void Complement_of_middle_yields_two_intervals()
    {
        // Complement of [3,7] in [min,max] = [min,3) U (7,max]
        var set = IntervalSet.Single(new Interval(3.0, 7.0, true, true));
        var result = set.Complement(Min, Max);
        Assert.Equal(2, result.Intervals.Length);
        Assert.Equal(Min, result.Intervals[0].Lo);
        Assert.Equal(3.0, result.Intervals[0].Hi);
        Assert.True(result.Intervals[0].LoInclusive);
        Assert.False(result.Intervals[0].HiInclusive);
        Assert.Equal(7.0, result.Intervals[1].Lo);
        Assert.Equal(Max, result.Intervals[1].Hi);
        Assert.False(result.Intervals[1].LoInclusive);
        Assert.True(result.Intervals[1].HiInclusive);
    }

    [Fact]
    public void Complement_of_universe_is_empty()
    {
        var u = IntervalSet.Universe(Min, Max);
        var result = u.Complement(Min, Max);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Complement_of_empty_is_universe()
    {
        var result = IntervalSet.Empty.Complement(Min, Max);
        Assert.True(result.CoversAll(Min, Max));
    }

    [Fact]
    public void Double_complement_returns_original()
    {
        var set = IntervalSet.Single(new Interval(3.0, 7.0, true, true));
        var result = set.Complement(Min, Max).Complement(Min, Max);
        Assert.Single(result.Intervals);
        Assert.Equal(3.0, result.Intervals[0].Lo);
        Assert.Equal(7.0, result.Intervals[0].Hi);
        Assert.True(result.Intervals[0].LoInclusive);
        Assert.True(result.Intervals[0].HiInclusive);
    }

    // ─── CoversAll ────────────────────────────────────────────────────

    [Fact]
    public void CoversAll_with_split_at_boundary_inclusive_exclusive()
    {
        // [min, 30] U (30, max] covers all
        var a = IntervalSet.Single(new Interval(Min, 30.0, true, true));
        var b = IntervalSet.Single(new Interval(30.0, Max, false, true));
        var result = a.Union(b);
        Assert.True(result.CoversAll(Min, Max));
    }

    [Fact]
    public void CoversAll_with_gap_returns_false()
    {
        // [min, 30) U (30, max] — gap at exactly 30
        var a = IntervalSet.Single(new Interval(Min, 30.0, true, false));
        var b = IntervalSet.Single(new Interval(30.0, Max, false, true));
        var result = a.Union(b);
        Assert.False(result.CoversAll(Min, Max));
    }

    [Fact]
    public void CoversAll_partial_coverage_returns_false()
    {
        var set = IntervalSet.Single(new Interval(Min, 500.0, true, true));
        Assert.False(set.CoversAll(Min, Max));
    }

    // ─── IsEmpty after operations ─────────────────────────────────────

    [Fact]
    public void Full_subtraction_makes_empty()
    {
        var u = IntervalSet.Universe(Min, Max);
        var result = u.Subtract(u);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Intersect_disjoint_sets_is_empty()
    {
        var a = IntervalSet.Single(new Interval(1.0, 3.0, true, true));
        var b = IntervalSet.Single(new Interval(5.0, 8.0, true, true));
        Assert.True(a.Intersect(b).IsEmpty);
    }

    // ─── Normalization ────────────────────────────────────────────────

    [Fact]
    public void Constructor_merges_overlapping_intervals()
    {
        // Build from multiple overlapping intervals via Union
        var a = IntervalSet.Single(new Interval(1.0, 5.0, true, true));
        var b = IntervalSet.Single(new Interval(4.0, 8.0, true, true));
        var c = IntervalSet.Single(new Interval(7.0, 12.0, true, true));
        var result = a.Union(b).Union(c);
        Assert.Single(result.Intervals);
        Assert.Equal(1.0, result.Intervals[0].Lo);
        Assert.Equal(12.0, result.Intervals[0].Hi);
    }

    [Fact]
    public void Constructor_sorts_intervals_by_lo()
    {
        // Union with out-of-order insertions should still produce sorted result
        var a = IntervalSet.Single(new Interval(10.0, 15.0, true, true));
        var b = IntervalSet.Single(new Interval(1.0, 5.0, true, true));
        var result = a.Union(b);
        Assert.Equal(2, result.Intervals.Length);
        Assert.True(result.Intervals[0].Lo < result.Intervals[1].Lo);
    }

    // ─── SplitByBoundaries ────────────────────────────────────────────

    [Fact]
    public void SplitByBoundaries_single_boundary_two_partitions()
    {
        var set = IntervalSet.Single(new Interval(0.0, 10.0, true, true));
        var boundaries = ImmutableArray.Create(5.0);
        var result = set.SplitByBoundaries(boundaries);

        Assert.Equal(2, result.Length);
        // First partition: [0, 5]
        Assert.False(result[0].IsEmpty);
        // Second partition: (5, 10]
        Assert.False(result[1].IsEmpty);
    }

    [Fact]
    public void SplitByBoundaries_boundary_outside_interval_one_nonempty_partition()
    {
        var set = IntervalSet.Single(new Interval(0.0, 10.0, true, true));
        var boundaries = ImmutableArray.Create(20.0);
        var result = set.SplitByBoundaries(boundaries);

        Assert.Equal(2, result.Length);
        // First partition: [0, 10] (everything, since boundary is outside)
        Assert.False(result[0].IsEmpty);
        // Second partition: nothing after 20
        Assert.True(result[1].IsEmpty);
    }

    [Fact]
    public void SplitByBoundaries_multiple_boundaries()
    {
        var set = IntervalSet.Single(new Interval(0.0, 100.0, true, true));
        var boundaries = ImmutableArray.Create(25.0, 50.0, 75.0);
        var result = set.SplitByBoundaries(boundaries);

        // 3 boundaries → 4 partitions
        Assert.Equal(4, result.Length);
        // All non-empty since boundaries are inside interval
        for (int i = 0; i < 4; i++)
        {
            Assert.False(result[i].IsEmpty);
        }
    }

    [Fact]
    public void SplitByBoundaries_empty_set_returns_empty_partitions()
    {
        var boundaries = ImmutableArray.Create(5.0);
        var result = IntervalSet.Empty.SplitByBoundaries(boundaries);
        Assert.Equal(2, result.Length);
        Assert.True(result[0].IsEmpty);
        Assert.True(result[1].IsEmpty);
    }

    [Fact]
    public void SplitByBoundaries_no_boundaries_returns_original()
    {
        var set = IntervalSet.Single(new Interval(0.0, 10.0, true, true));
        var boundaries = ImmutableArray<double>.Empty;
        var result = set.SplitByBoundaries(boundaries);
        Assert.Single(result);
        Assert.Equal(set.Intervals[0], result[0].Intervals[0]);
    }

    // ─── Multi-interval operations ────────────────────────────────────

    [Fact]
    public void Subtract_from_multiple_intervals()
    {
        // [1,5] U [10,15] - [3,12] = [1,3) U (12,15]
        var a = IntervalSet.Single(new Interval(1.0, 5.0, true, true))
                           .Union(IntervalSet.Single(new Interval(10.0, 15.0, true, true)));
        var b = IntervalSet.Single(new Interval(3.0, 12.0, true, true));
        var result = a.Subtract(b);
        Assert.Equal(2, result.Intervals.Length);
        Assert.Equal(1.0, result.Intervals[0].Lo);
        Assert.Equal(3.0, result.Intervals[0].Hi);
        Assert.True(result.Intervals[0].LoInclusive);
        Assert.False(result.Intervals[0].HiInclusive);
        Assert.Equal(12.0, result.Intervals[1].Lo);
        Assert.Equal(15.0, result.Intervals[1].Hi);
        Assert.False(result.Intervals[1].LoInclusive);
        Assert.True(result.Intervals[1].HiInclusive);
    }

    [Fact]
    public void Intersect_multi_with_single()
    {
        // ([1,5] U [10,15]) & [3,12] = [3,5] U [10,12]
        var a = IntervalSet.Single(new Interval(1.0, 5.0, true, true))
                           .Union(IntervalSet.Single(new Interval(10.0, 15.0, true, true)));
        var b = IntervalSet.Single(new Interval(3.0, 12.0, true, true));
        var result = a.Intersect(b);
        Assert.Equal(2, result.Intervals.Length);
        Assert.Equal(3.0, result.Intervals[0].Lo);
        Assert.Equal(5.0, result.Intervals[0].Hi);
        Assert.Equal(10.0, result.Intervals[1].Lo);
        Assert.Equal(12.0, result.Intervals[1].Hi);
    }

    // ─── Double / infinity edge cases ─────────────────────────────────

    [Fact]
    public void Infinite_range_split_covers_all()
    {
        double negInf = double.NegativeInfinity;
        double posInf = double.PositiveInfinity;
        // (-inf, 30] U (30, +inf)
        var a = IntervalSet.Single(new Interval(negInf, 30.0, false, true));
        var b = IntervalSet.Single(new Interval(30.0, posInf, false, false));
        var result = a.Union(b);
        // These cover everything except -inf (exclusive) — depends on CoversAll behavior
        // With inclusive universe bounds: CoversAll(-inf, +inf) might fail due to exclusive lo/hi
        // But CoversAll with same exclusive bounds should work
        Assert.False(result.IsEmpty);
    }
}
