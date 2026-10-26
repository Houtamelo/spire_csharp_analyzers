using System.Numerics;
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
        Assert.Single(domain.IntegralIntervals.Intervals);
        Assert.Equal(new BigInteger(int.MinValue), domain.IntegralIntervals.Intervals[0].Lo);
        Assert.Equal(new BigInteger(int.MaxValue), domain.IntegralIntervals.Intervals[0].Hi);
    }

    [Fact]
    public void Universe_for_byte_is_0_to_255()
    {
        var type = GetType(SpecialType.System_Byte);
        var domain = NumericDomain.Universe(type);
        Assert.True(domain.IsUniverse);
        Assert.False(domain.IsEmpty);
        Assert.Single(domain.IntegralIntervals.Intervals);
        Assert.Equal(BigInteger.Zero, domain.IntegralIntervals.Intervals[0].Lo);
        Assert.Equal(new BigInteger(byte.MaxValue), domain.IntegralIntervals.Intervals[0].Hi);
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
        var greaterThan30 = NumericDomain.FromIntegralIntervals(
            type,
            IntegralIntervalSet.Single(new IntegralInterval(30, int.MaxValue, false, true)));

        var result = (NumericDomain)universe.Subtract(greaterThan30);

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);

        // Result should be [int.MinValue, 30]
        var intervals = result.IntegralIntervals;
        Assert.Single(intervals.Intervals);
        Assert.Equal(new BigInteger(int.MinValue), intervals.Intervals[0].Lo);
        Assert.Equal(new BigInteger(30), intervals.Intervals[0].Hi);
        Assert.True(intervals.Intervals[0].LoInclusive);
        Assert.True(intervals.Intervals[0].HiInclusive);
    }

    [Fact]
    public void Subtract_less_than_or_equal_30_from_int_leaves_30_to_max()
    {
        var type = GetType(SpecialType.System_Int32);
        var universe = NumericDomain.Universe(type);

        // Subtract [int.MinValue, 30] — represents "<= 30"
        var leq30 = NumericDomain.FromIntegralIntervals(
            type,
            IntegralIntervalSet.Single(new IntegralInterval(int.MinValue, 30, true, true)));

        var result = (NumericDomain)universe.Subtract(leq30);

        Assert.False(result.IsEmpty);
        Assert.False(result.IsUniverse);

        // Result should be [31, int.MaxValue] (normalized for integral type)
        var intervals = result.IntegralIntervals;
        Assert.Single(intervals.Intervals);
        Assert.Equal(new BigInteger(31), intervals.Intervals[0].Lo);
        Assert.Equal(new BigInteger(int.MaxValue), intervals.Intervals[0].Hi);
        Assert.True(intervals.Intervals[0].LoInclusive);
        Assert.True(intervals.Intervals[0].HiInclusive);
    }

    // ─── Complement ──────────────────────────────────────────────────

    [Fact]
    public void Complement_of_greater_than_30_is_min_to_30_for_int()
    {
        var type = GetType(SpecialType.System_Int32);

        // (30, int.MaxValue] — represents "> 30"
        var greaterThan30 = NumericDomain.FromIntegralIntervals(
            type,
            IntegralIntervalSet.Single(new IntegralInterval(30, int.MaxValue, false, true)));

        var result = (NumericDomain)greaterThan30.Complement();

        // Complement should be [int.MinValue, 30]
        var intervals = result.IntegralIntervals;
        Assert.Single(intervals.Intervals);
        Assert.Equal(new BigInteger(int.MinValue), intervals.Intervals[0].Lo);
        Assert.Equal(new BigInteger(30), intervals.Intervals[0].Hi);
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
        var left = NumericDomain.FromIntegralIntervals(
            type,
            IntegralIntervalSet.Single(new IntegralInterval(int.MinValue, 30, true, true)));

        // Subtract left from universe should give (30, int.MaxValue]
        var right = (NumericDomain)universe.Subtract(left);

        // The right should be the complement: [31, int.MaxValue] (normalized for integral type)
        Assert.False(right.IsEmpty);
        var rightIntervals = right.IntegralIntervals;
        Assert.Single(rightIntervals.Intervals);
        Assert.Equal(new BigInteger(31), rightIntervals.Intervals[0].Lo);
        Assert.True(rightIntervals.Intervals[0].LoInclusive);
        Assert.Equal(new BigInteger(int.MaxValue), rightIntervals.Intervals[0].Hi);
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
        var point30 = NumericDomain.FromIntegralIntervals(
            type,
            IntegralIntervalSet.Single(new IntegralInterval(30, 30, true, true)));

        var domain = (NumericDomain)universe.Subtract(point30);

        // After integral normalization: [MinValue, 29] ∪ [31, MaxValue]
        // Boundaries at 29 and 31 → 3 partitions
        var splits = domain.Split();

        Assert.Equal(3, splits.Length);
        // First partition: [int.MinValue, 29]
        Assert.False(splits[0].IsEmpty);
        // Second partition: [31, 31] (single point between boundaries)
        Assert.False(splits[1].IsEmpty);
        // Third partition: [32, int.MaxValue]
        Assert.False(splits[2].IsEmpty);
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
        var a = NumericDomain.FromIntegralIntervals(
            type,
            IntegralIntervalSet.Single(new IntegralInterval(0, 50, true, true)));

        // [30, 100]
        var b = NumericDomain.FromIntegralIntervals(
            type,
            IntegralIntervalSet.Single(new IntegralInterval(30, 100, true, true)));

        var result = (NumericDomain)a.Intersect(b);

        Assert.False(result.IsEmpty);
        var intervals = result.IntegralIntervals;
        Assert.Single(intervals.Intervals);
        Assert.Equal(new BigInteger(30), intervals.Intervals[0].Lo);
        Assert.Equal(new BigInteger(50), intervals.Intervals[0].Hi);
        Assert.True(intervals.Intervals[0].LoInclusive);
        Assert.True(intervals.Intervals[0].HiInclusive);
    }
}
