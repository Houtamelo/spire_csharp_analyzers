using Houtamelo.Spire.PatternAnalysis.Domains;
using Houtamelo.Spire.PatternAnalysis.Domains.Numeric;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests.Domains;

public class NumericDomainTests
{
    static ITypeSymbol GetType(SpecialType specialType)
    {
        var compilation = CSharpCompilation.Create("Test",
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetSpecialType(specialType);
    }

    // ─── Universe ────────────────────────────────────────────────────

    [Fact]
    public void Universe_for_int_is_universe()
    {
        var type = GetType(SpecialType.System_Int32);
        var domain = NumericDomain.Universe(type);
        Assert.True(domain.IsUniverse);
        Assert.False(domain.IsEmpty);
    }

    [Fact]
    public void Universe_for_byte_is_0_to_255()
    {
        var type = GetType(SpecialType.System_Byte);
        var domain = NumericDomain.Universe(type);
        Assert.True(domain.IsUniverse);
        Assert.False(domain.IsEmpty);
    }

    [Fact]
    public void Universe_for_double_handles_range()
    {
        var type = GetType(SpecialType.System_Double);
        var domain = NumericDomain.Universe(type);
        Assert.True(domain.IsUniverse);
        Assert.False(domain.IsEmpty);
    }

    // ─── Subtract ────────────────────────────────────────────────────

    [Fact]
    public void Subtract_greater_than_30_from_int_leaves_min_to_30()
    {
        var type = GetType(SpecialType.System_Int32);
        var universe = NumericDomain.Universe(type);

        // Subtract (30, int.MaxValue] — represents "> 30"
        var greaterThan30 = new NumericDomain(
            type,
            IntervalSet.Single(new Interval(30.0, int.MaxValue, false, true)),
            int.MinValue, int.MaxValue);

        var result = (NumericDomain)universe.Subtract(greaterThan30);

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);

        // Result should be [int.MinValue, 30]
        var intervals = result.Intervals;
        Assert.Single(intervals.Intervals);
        Assert.Equal((double)int.MinValue, intervals.Intervals[0].Lo);
        Assert.Equal(30.0, intervals.Intervals[0].Hi);
        Assert.True(intervals.Intervals[0].LoInclusive);
        Assert.True(intervals.Intervals[0].HiInclusive);
    }

    [Fact]
    public void Subtract_less_than_or_equal_30_from_int_leaves_30_to_max()
    {
        var type = GetType(SpecialType.System_Int32);
        var universe = NumericDomain.Universe(type);

        // Subtract [int.MinValue, 30] — represents "<= 30"
        var leq30 = new NumericDomain(
            type,
            IntervalSet.Single(new Interval(int.MinValue, 30.0, true, true)),
            int.MinValue, int.MaxValue);

        var result = (NumericDomain)universe.Subtract(leq30);

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);

        // Result should be (30, int.MaxValue]
        var intervals = result.Intervals;
        Assert.Single(intervals.Intervals);
        Assert.Equal(30.0, intervals.Intervals[0].Lo);
        Assert.Equal((double)int.MaxValue, intervals.Intervals[0].Hi);
        Assert.False(intervals.Intervals[0].LoInclusive);
        Assert.True(intervals.Intervals[0].HiInclusive);
    }

    // ─── Complement ──────────────────────────────────────────────────

    [Fact]
    public void Complement_of_greater_than_30_is_min_to_30_for_int()
    {
        var type = GetType(SpecialType.System_Int32);

        // (30, int.MaxValue] — represents "> 30"
        var greaterThan30 = new NumericDomain(
            type,
            IntervalSet.Single(new Interval(30.0, int.MaxValue, false, true)),
            int.MinValue, int.MaxValue);

        var result = (NumericDomain)greaterThan30.Complement();

        // Complement should be [int.MinValue, 30]
        var intervals = result.Intervals;
        Assert.Single(intervals.Intervals);
        Assert.Equal((double)int.MinValue, intervals.Intervals[0].Lo);
        Assert.Equal(30.0, intervals.Intervals[0].Hi);
        Assert.True(intervals.Intervals[0].LoInclusive);
        Assert.True(intervals.Intervals[0].HiInclusive);
    }

    // ─── Complementary ranges ────────────────────────────────────────

    [Fact]
    public void Two_complementary_ranges_union_to_universe()
    {
        var type = GetType(SpecialType.System_Int32);
        var universe = NumericDomain.Universe(type);

        // [int.MinValue, 30]
        var left = new NumericDomain(
            type,
            IntervalSet.Single(new Interval(int.MinValue, 30.0, true, true)),
            int.MinValue, int.MaxValue);

        // Subtract left from universe should give (30, int.MaxValue]
        var right = (NumericDomain)universe.Subtract(left);

        // The right should be the complement
        Assert.False(right.IsEmpty);
        var rightIntervals = right.Intervals;
        Assert.Single(rightIntervals.Intervals);
        Assert.Equal(30.0, rightIntervals.Intervals[0].Lo);
        Assert.False(rightIntervals.Intervals[0].LoInclusive);
        Assert.Equal((double)int.MaxValue, rightIntervals.Intervals[0].Hi);
        Assert.True(rightIntervals.Intervals[0].HiInclusive);
    }

    // ─── Split ───────────────────────────────────────────────────────

    [Fact]
    public void Split_with_boundary_at_30_returns_two_partitions()
    {
        var type = GetType(SpecialType.System_Int32);
        var universe = NumericDomain.Universe(type);

        // Subtract the single point [30, 30] from the universe to create two disjoint intervals:
        // [int.MinValue, 30) U (30, int.MaxValue]
        var point30 = new NumericDomain(
            type,
            IntervalSet.Single(new Interval(30.0, 30.0, true, true)),
            int.MinValue, int.MaxValue);

        var domain = (NumericDomain)universe.Subtract(point30);

        // The domain now has two intervals with boundaries at 30
        var splits = domain.Split();

        Assert.Equal(2, splits.Length);
        // First partition: [int.MinValue, 30)
        Assert.False(splits[0].IsEmpty);
        // Second partition: (30, int.MaxValue]
        Assert.False(splits[1].IsEmpty);
    }

    // ─── IsEmpty ─────────────────────────────────────────────────────

    [Fact]
    public void IsEmpty_after_subtracting_full_range()
    {
        var type = GetType(SpecialType.System_Int32);
        var universe = NumericDomain.Universe(type);
        var result = universe.Subtract(universe);
        Assert.True(result.IsEmpty);
    }

    // ─── Intersect ───────────────────────────────────────────────────

    [Fact]
    public void Intersect_overlapping_ranges()
    {
        var type = GetType(SpecialType.System_Int32);

        // [0, 50]
        var a = new NumericDomain(
            type,
            IntervalSet.Single(new Interval(0.0, 50.0, true, true)),
            int.MinValue, int.MaxValue);

        // [30, 100]
        var b = new NumericDomain(
            type,
            IntervalSet.Single(new Interval(30.0, 100.0, true, true)),
            int.MinValue, int.MaxValue);

        var result = (NumericDomain)a.Intersect(b);

        Assert.False(result.IsEmpty);
        var intervals = result.Intervals;
        Assert.Single(intervals.Intervals);
        Assert.Equal(30.0, intervals.Intervals[0].Lo);
        Assert.Equal(50.0, intervals.Intervals[0].Hi);
        Assert.True(intervals.Intervals[0].LoInclusive);
        Assert.True(intervals.Intervals[0].HiInclusive);
    }
}
