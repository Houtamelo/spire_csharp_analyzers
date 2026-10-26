using System.Numerics;
using Houtamelo.Spire.PatternAnalysis.Domains.Numeric;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Domains.Numeric;

public class IntegralIntervalTests
{
    static readonly BigInteger Large = BigInteger.Parse("9007199254740992");
    static readonly BigInteger LargeNext = BigInteger.Parse("9007199254740993");

    [Fact]
    public void Empty_interval_is_empty()
    {
        Assert.True(IntegralInterval.Empty.IsEmpty);
        Assert.False(IntegralInterval.Empty.Contains(BigInteger.Zero));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Inclusivity_flags_are_preserved_for_non_empty_ranges(
        bool loInclusive,
        bool hiInclusive)
    {
        var interval = new IntegralInterval(1, 3, loInclusive, hiInclusive);

        Assert.Equal(loInclusive, interval.LoInclusive);
        Assert.Equal(hiInclusive, interval.HiInclusive);
        Assert.Equal(loInclusive, interval.Contains(BigInteger.One));
        Assert.Equal(hiInclusive, interval.Contains(new BigInteger(3)));
        Assert.False(interval.IsEmpty);
    }

    [Fact]
    public void Equal_inclusive_bounds_are_a_singleton()
    {
        var interval = new IntegralInterval(LargeNext, LargeNext, true, true);

        Assert.False(interval.IsEmpty);
        Assert.True(interval.Contains(LargeNext));
    }

    [Fact]
    public void Exact_endpoints_are_not_narrowed_to_double()
    {
        var interval = new IntegralInterval(Large, LargeNext, true, true);

        Assert.Equal(Large, interval.Lo);
        Assert.Equal(LargeNext, interval.Hi);
        Assert.True(interval.Contains(LargeNext));
    }

    [Fact]
    public void Equal_exclusive_bounds_are_empty()
    {
        var interval = new IntegralInterval(LargeNext, LargeNext, false, true);

        Assert.True(interval.IsEmpty);
        Assert.False(interval.Contains(LargeNext));
    }

    [Fact]
    public void Open_bounds_can_represent_adjacent_integer_values()
    {
        var interval = new IntegralInterval(Large, LargeNext + 1, false, false);

        Assert.True(interval.Contains(LargeNext));
        Assert.False(interval.Contains(Large));
        Assert.False(interval.Contains(LargeNext + 1));
    }

    [Fact]
    public void Min_and_max_endpoints_remain_exact()
    {
        var min = BigInteger.Parse("-9223372036854775808");
        var max = BigInteger.Parse("18446744073709551615");
        var interval = IntegralInterval.Universe(min, max);

        Assert.True(interval.Contains(min));
        Assert.True(interval.Contains(max));
        Assert.Equal(min, interval.Lo);
        Assert.Equal(max, interval.Hi);
    }

    [Fact]
    public void Overlap_respects_exclusive_shared_endpoint()
    {
        var touching = new IntegralInterval(1, 3, true, true);
        var included = new IntegralInterval(3, 5, true, true);
        var excluded = new IntegralInterval(3, 5, false, true);

        Assert.True(touching.Overlaps(included));
        Assert.False(touching.Overlaps(excluded));
    }

    [Fact]
    public void Discrete_open_ranges_with_no_shared_integer_do_not_overlap()
    {
        var left = new IntegralInterval(0, 2, false, false);
        var right = new IntegralInterval(1, 3, false, false);

        Assert.True(left.Contains(BigInteger.One));
        Assert.True(right.Contains(new BigInteger(2)));
        Assert.False(left.Overlaps(right));
        Assert.False(right.Overlaps(left));
    }

    [Fact]
    public void Discrete_open_range_overlaps_closed_singleton_interior()
    {
        var open = new IntegralInterval(0, 2, false, false);
        var singleton = new IntegralInterval(1, 1, true, true);

        Assert.True(open.Overlaps(singleton));
        Assert.True(singleton.Overlaps(open));
    }

    [Fact]
    public void Empty_open_adjacent_range_does_not_overlap()
    {
        var empty = new IntegralInterval(1, 2, false, false);
        var containing = new IntegralInterval(0, 3, true, true);

        Assert.True(empty.IsEmpty);
        Assert.False(empty.Overlaps(containing));
        Assert.False(containing.Overlaps(empty));
    }
}
