using System.Collections.Immutable;
using System.Numerics;
using Houtamelo.Spire.PatternAnalysis.Domains.Numeric;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Domains.Numeric;

public class IntegralIntervalSetTests
{
    static readonly BigInteger Min = BigInteger.Parse("-9223372036854775808");
    static readonly BigInteger SignedMax = BigInteger.Parse("9223372036854775807");
    static readonly BigInteger Max = BigInteger.Parse("18446744073709551615");
    static readonly BigInteger Large = BigInteger.Parse("9007199254740992");

    [Fact]
    public void Empty_set_is_empty()
    {
        Assert.True(IntegralIntervalSet.Empty.IsEmpty);
    }

    [Fact]
    public void Single_of_empty_interval_is_empty()
    {
        var result = IntegralIntervalSet.Single(IntegralInterval.Empty);

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Adjacent_integer_ranges_cover_without_a_gap()
    {
        var left = IntegralIntervalSet.Single(new IntegralInterval(1, 3, true, true));
        var right = IntegralIntervalSet.Single(new IntegralInterval(4, 7, true, true));

        var result = left.Union(right);

        Assert.True(result.CoversAll(1, 7));
    }

    [Fact]
    public void Adjacent_integer_ranges_do_not_hide_a_singleton_gap()
    {
        var left = IntegralIntervalSet.Single(new IntegralInterval(1, 3, true, true));
        var right = IntegralIntervalSet.Single(new IntegralInterval(5, 7, true, true));

        var result = left.Union(right);

        Assert.Equal(2, result.Intervals.Length);
        Assert.False(result.CoversAll(1, 7));
        Assert.False(result.Intervals[0].Contains(new BigInteger(4)));
        Assert.False(result.Intervals[1].Contains(new BigInteger(4)));
    }

    [Fact]
    public void Subtract_preserves_the_odd_value_above_double_precision()
    {
        var universe = IntegralIntervalSet.Universe(Large, Large + 2);
        var excluded = IntegralIntervalSet.Single(new IntegralInterval(Large, Large, true, true));

        var result = universe.Subtract(excluded);

        Assert.Single(result.Intervals);
        Assert.Equal(Large + 1, result.Intervals[0].Lo);
        Assert.Equal(Large + 2, result.Intervals[0].Hi);
    }

    [Fact]
    public void Split_uses_discrete_right_partition()
    {
        var universe = IntegralIntervalSet.Universe(0, 4);
        var partitions = universe.SplitByBoundaries(ImmutableArray.Create(new BigInteger(2)));

        Assert.Equal(2, partitions.Length);
        Assert.True(partitions[0].Intervals[0].Contains(2));
        Assert.False(partitions[0].Intervals[0].Contains(3));
        Assert.False(partitions[1].Intervals[0].Contains(2));
        Assert.True(partitions[1].Intervals[0].Contains(3));
    }

    [Fact]
    public void Split_at_universe_extrema_keeps_empty_side_empty()
    {
        var universe = IntegralIntervalSet.Universe(0, 4);

        var atMin = universe.SplitByBoundaries(ImmutableArray.Create(BigInteger.Zero));
        Assert.Equal(2, atMin.Length);
        Assert.True(atMin[0].Intervals[0].Contains(BigInteger.Zero));
        Assert.True(atMin[1].Intervals[0].Contains(BigInteger.One));

        var atMax = universe.SplitByBoundaries(ImmutableArray.Create(new BigInteger(4)));
        Assert.Equal(2, atMax.Length);
        Assert.True(atMax[0].Intervals[0].Contains(new BigInteger(4)));
        Assert.True(atMax[1].IsEmpty);
    }

    [Fact]
    public void Split_preserves_odd_boundary_above_double_precision()
    {
        var universe = IntegralIntervalSet.Universe(Large, Large + 2);
        var partitions = universe.SplitByBoundaries(
            ImmutableArray.Create(Large + 1));

        Assert.Equal(2, partitions.Length);
        Assert.True(partitions[0].Intervals[0].Contains(Large + 1));
        Assert.False(partitions[0].Intervals[0].Contains(Large + 2));
        Assert.True(partitions[1].Intervals[0].Contains(Large + 2));
    }

    [Fact]
    public void CoversAll_handles_full_signed_and_unsigned_ranges()
    {
        var signed = IntegralIntervalSet.Universe(Min, SignedMax);
        var unsigned = IntegralIntervalSet.Universe(BigInteger.Zero, Max);

        Assert.True(signed.CoversAll(Min, SignedMax));
        Assert.True(unsigned.CoversAll(BigInteger.Zero, Max));
    }

    [Fact]
    public void Intersect_preserves_two_disjoint_components()
    {
        var left = IntegralIntervalSet.Single(new IntegralInterval(1, 10, true, true));
        var right = IntegralIntervalSet.Single(new IntegralInterval(3, 4, true, true))
            .Union(IntegralIntervalSet.Single(new IntegralInterval(7, 8, true, true)));

        var result = left.Intersect(right);

        Assert.Equal(2, result.Intervals.Length);
        Assert.Equal(new BigInteger(3), result.Intervals[0].Lo);
        Assert.Equal(new BigInteger(4), result.Intervals[0].Hi);
        Assert.Equal(new BigInteger(7), result.Intervals[1].Lo);
        Assert.Equal(new BigInteger(8), result.Intervals[1].Hi);
    }

    [Fact]
    public void Subtract_two_singletons_leaves_three_exact_components()
    {
        var universe = IntegralIntervalSet.Universe(1, 5);
        var excluded = IntegralIntervalSet.Single(new IntegralInterval(2, 2, true, true))
            .Union(IntegralIntervalSet.Single(new IntegralInterval(4, 4, true, true)));

        var result = universe.Subtract(excluded);

        Assert.Equal(3, result.Intervals.Length);
        Assert.Equal(new BigInteger(1), result.Intervals[0].Lo);
        Assert.Equal(new BigInteger(1), result.Intervals[0].Hi);
        Assert.Equal(new BigInteger(3), result.Intervals[1].Lo);
        Assert.Equal(new BigInteger(3), result.Intervals[1].Hi);
        Assert.Equal(new BigInteger(5), result.Intervals[2].Lo);
        Assert.Equal(new BigInteger(5), result.Intervals[2].Hi);
    }

    [Fact]
    public void Complement_of_two_components_preserves_both_gaps()
    {
        var set = IntegralIntervalSet.Single(new IntegralInterval(1, 2, true, true))
            .Union(IntegralIntervalSet.Single(new IntegralInterval(4, 5, true, true)));

        var result = set.Complement(0, 6);

        Assert.Equal(3, result.Intervals.Length);
        Assert.Equal(new BigInteger(0), result.Intervals[0].Lo);
        Assert.Equal(new BigInteger(0), result.Intervals[0].Hi);
        Assert.Equal(new BigInteger(3), result.Intervals[1].Lo);
        Assert.Equal(new BigInteger(3), result.Intervals[1].Hi);
        Assert.Equal(new BigInteger(6), result.Intervals[2].Lo);
        Assert.Equal(new BigInteger(6), result.Intervals[2].Hi);
    }
}
