using Houtamelo.Spire.Benchmarks.Benchmarks;
using Houtamelo.Spire.Benchmarks.Helpers;
using Houtamelo.Spire.Benchmarks.Types;
using Xunit;

namespace Houtamelo.Spire.Benchmarks.Tests;

public sealed class ArrayFillerDistributionTests
{
    private const int VariantCount = 8;
    private const int SequenceLength = VariantCount * 4;
    private const int HistogramLength = 100_000;
    private const int Seed = 42;

    [Fact]
    public void PickVariant_Uniform_PreservesTheDeterministicCycle()
    {
        var random = new Random(Seed);

        for (var index = 0; index < SequenceLength; index++)
            Assert.Equal(index % VariantCount, ArrayFiller.PickVariant(random, Distribution.Uniform, index, VariantCount));
    }

    [Fact]
    public void PickVariant_Uniform_DoesNotConsumeRandomState()
    {
        var expected = new Random(Seed);
        var actual = new Random(Seed);

        for (var index = 0; index < SequenceLength; index++)
            Assert.Equal(index % VariantCount, ArrayFiller.PickVariant(actual, Distribution.Uniform, index, VariantCount));

        Assert.Equal(expected.Next(), actual.Next());
    }

    [Fact]
    public void PickVariant_Uniform_FixedSeedIsRepeatable()
    {
        var expected = new Random(Seed);
        var actual = new Random(Seed);

        for (var index = 0; index < HistogramLength; index++)
            Assert.Equal(
                ArrayFiller.PickVariant(expected, Distribution.Uniform, index, VariantCount),
                ArrayFiller.PickVariant(actual, Distribution.Uniform, index, VariantCount));
    }

    [Fact]
    public void PickVariant_Skewed80_FixedSeedIsRepeatable()
    {
        var expected = new Random(Seed);
        var actual = new Random(Seed);

        for (var index = 0; index < HistogramLength; index++)
            Assert.Equal(
                ArrayFiller.PickVariant(expected, Distribution.Skewed80, index, VariantCount),
                ArrayFiller.PickVariant(actual, Distribution.Skewed80, index, VariantCount));
    }

    [Theory]
    [InlineData(Distribution.Uniform)]
    [InlineData(Distribution.Skewed80)]
    public void PickVariant_WithOneVariantAlwaysReturnsZero(Distribution distribution)
    {
        var random = new Random(Seed);

        for (var index = 0; index < SequenceLength; index++)
            Assert.Equal(0, ArrayFiller.PickVariant(random, distribution, index, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PickVariant_RejectsNonPositiveVariantCounts(int count)
    {
        var uniform = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ArrayFiller.PickVariant(new Random(Seed), Distribution.Uniform, 0, count));
        Assert.Equal("count", uniform.ParamName);

        var skewed = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ArrayFiller.PickVariant(new Random(Seed), Distribution.Skewed80, 0, count));
        Assert.Equal("count", skewed.ParamName);
    }

    [Fact]
    public void Uniform_FillOverloadsPreserveTheDeterministicCycle()
    {
        AssertUniform(new EventBench[SequenceLength], ArrayFiller.Fill, static value => (int)value.kind);
        AssertUniform(new EventAuto[SequenceLength], ArrayFiller.Fill, static value => (int)value.kind);
        AssertUniform(new EventTyped[SequenceLength], ArrayFiller.Fill, static value => (int)value.kind);
        AssertUniform(new EventShared[SequenceLength], ArrayFiller.Fill, static value => (int)value.kind);
        AssertUniform(new EventRecord[SequenceLength], ArrayFiller.Fill, EventRecordKind);
        AssertUniform(new EventNative[SequenceLength], ArrayFiller.Fill, EventNativeKind);

        AssertUniform(new EventJsonBench[SequenceLength], ArrayFiller.Fill, static value => (int)value.kind);
        AssertUniform(new EventJsonAuto[SequenceLength], ArrayFiller.Fill, static value => (int)value.kind);
        AssertUniform(new EventJsonTyped[SequenceLength], ArrayFiller.Fill, static value => (int)value.kind);
        AssertUniform(new EventJsonShared[SequenceLength], ArrayFiller.Fill, static value => (int)value.kind);
        AssertUniform(new EventJsonRecord[SequenceLength], ArrayFiller.Fill, EventJsonRecordKind);

        AssertUniform(new PhysicsBench[SequenceLength], ArrayFiller.Fill, static value => (int)value.kind);
        AssertUniform(new PhysicsAuto[SequenceLength], ArrayFiller.Fill, static value => (int)value.kind);
        AssertUniform(new PhysicsTyped[SequenceLength], ArrayFiller.Fill, static value => (int)value.kind);
        AssertUniform(new PhysicsShared[SequenceLength], ArrayFiller.Fill, static value => (int)value.kind);
        AssertUniform(new PhysicsRecord[SequenceLength], ArrayFiller.Fill, PhysicsRecordKind);
        AssertUniform(new PhysicsNative[SequenceLength], ArrayFiller.Fill, PhysicsNativeKind);
    }

    [Fact]
    public void Skewed80_FillOverloadsKeepEveryKindInRangeAndUseAn80PercentFirstKind()
    {
        var failures = new List<string>();

        AssertSkewed80(new EventBench[HistogramLength], ArrayFiller.Fill, static value => (int)value.kind, failures);
        AssertSkewed80(new EventAuto[HistogramLength], ArrayFiller.Fill, static value => (int)value.kind, failures);
        AssertSkewed80(new EventTyped[HistogramLength], ArrayFiller.Fill, static value => (int)value.kind, failures);
        AssertSkewed80(new EventShared[HistogramLength], ArrayFiller.Fill, static value => (int)value.kind, failures);
        AssertSkewed80(new EventRecord[HistogramLength], ArrayFiller.Fill, EventRecordKind, failures);
        AssertSkewed80(new EventNative[HistogramLength], ArrayFiller.Fill, EventNativeKind, failures);

        AssertSkewed80(new EventJsonBench[HistogramLength], ArrayFiller.Fill, static value => (int)value.kind, failures);
        AssertSkewed80(new EventJsonAuto[HistogramLength], ArrayFiller.Fill, static value => (int)value.kind, failures);
        AssertSkewed80(new EventJsonTyped[HistogramLength], ArrayFiller.Fill, static value => (int)value.kind, failures);
        AssertSkewed80(new EventJsonShared[HistogramLength], ArrayFiller.Fill, static value => (int)value.kind, failures);
        AssertSkewed80(new EventJsonRecord[HistogramLength], ArrayFiller.Fill, EventJsonRecordKind, failures);

        AssertSkewed80(new PhysicsBench[HistogramLength], ArrayFiller.Fill, static value => (int)value.kind, failures);
        AssertSkewed80(new PhysicsAuto[HistogramLength], ArrayFiller.Fill, static value => (int)value.kind, failures);
        AssertSkewed80(new PhysicsTyped[HistogramLength], ArrayFiller.Fill, static value => (int)value.kind, failures);
        AssertSkewed80(new PhysicsShared[HistogramLength], ArrayFiller.Fill, static value => (int)value.kind, failures);
        AssertSkewed80(new PhysicsRecord[HistogramLength], ArrayFiller.Fill, PhysicsRecordKind, failures);
        AssertSkewed80(new PhysicsNative[HistogramLength], ArrayFiller.Fill, PhysicsNativeKind, failures);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Skewed80_FillersWithTheSameSeedProduceTheSameKindSequencePerFamily()
    {
        var eventKinds = FillKinds(new EventBench[SequenceLength * 4], ArrayFiller.Fill, static value => (int)value.kind);
        Assert.Equal(eventKinds, FillKinds(new EventAuto[SequenceLength * 4], ArrayFiller.Fill, static value => (int)value.kind));
        Assert.Equal(eventKinds, FillKinds(new EventTyped[SequenceLength * 4], ArrayFiller.Fill, static value => (int)value.kind));
        Assert.Equal(eventKinds, FillKinds(new EventShared[SequenceLength * 4], ArrayFiller.Fill, static value => (int)value.kind));
        Assert.Equal(eventKinds, FillKinds(new EventRecord[SequenceLength * 4], ArrayFiller.Fill, EventRecordKind));
        Assert.Equal(eventKinds, FillKinds(new EventNative[SequenceLength * 4], ArrayFiller.Fill, EventNativeKind));

        var jsonKinds = FillKinds(new EventJsonBench[SequenceLength * 4], ArrayFiller.Fill, static value => (int)value.kind);
        Assert.Equal(jsonKinds, FillKinds(new EventJsonAuto[SequenceLength * 4], ArrayFiller.Fill, static value => (int)value.kind));
        Assert.Equal(jsonKinds, FillKinds(new EventJsonTyped[SequenceLength * 4], ArrayFiller.Fill, static value => (int)value.kind));
        Assert.Equal(jsonKinds, FillKinds(new EventJsonShared[SequenceLength * 4], ArrayFiller.Fill, static value => (int)value.kind));
        Assert.Equal(jsonKinds, FillKinds(new EventJsonRecord[SequenceLength * 4], ArrayFiller.Fill, EventJsonRecordKind));

        var physicsKinds = FillKinds(new PhysicsBench[SequenceLength * 4], ArrayFiller.Fill, static value => (int)value.kind);
        Assert.Equal(physicsKinds, FillKinds(new PhysicsAuto[SequenceLength * 4], ArrayFiller.Fill, static value => (int)value.kind));
        Assert.Equal(physicsKinds, FillKinds(new PhysicsTyped[SequenceLength * 4], ArrayFiller.Fill, static value => (int)value.kind));
        Assert.Equal(physicsKinds, FillKinds(new PhysicsShared[SequenceLength * 4], ArrayFiller.Fill, static value => (int)value.kind));
        Assert.Equal(physicsKinds, FillKinds(new PhysicsRecord[SequenceLength * 4], ArrayFiller.Fill, PhysicsRecordKind));
        Assert.Equal(physicsKinds, FillKinds(new PhysicsNative[SequenceLength * 4], ArrayFiller.Fill, PhysicsNativeKind));
    }

    [Theory]
    [InlineData(Distribution.Uniform)]
    [InlineData(Distribution.Skewed80)]
    public void MatchBenchmarks_SetupProducesEqualResultsAcrossAllRepresentations(Distribution distribution)
    {
        var benchmarks = new MatchBenchmarks { N = 1_000, Dist = distribution };
        benchmarks.Setup();

        var expected = benchmarks.Auto();
        Assert.Equal(expected, benchmarks.ExplicitAuto());
        Assert.Equal(expected, benchmarks.Typed());
        Assert.Equal(expected, benchmarks.Shared());
        Assert.Equal(expected, benchmarks.Record());
        Assert.Equal(expected, benchmarks.Native());
        Assert.Equal(expected, benchmarks.PayloadDeconstruct());
    }

    private static void AssertUniform<T>(
        T[] values,
        Action<T[], Random, Distribution> fill,
        Func<T, int> getKind)
    {
        fill(values, new Random(Seed), Distribution.Uniform);

        for (var index = 0; index < values.Length; index++)
            Assert.Equal(index % VariantCount, getKind(values[index]));
    }

    private static void AssertSkewed80<T>(
        T[] values,
        Action<T[], Random, Distribution> fill,
        Func<T, int> getKind,
        ICollection<string> failures)
    {
        fill(values, new Random(Seed), Distribution.Skewed80);
        var counts = new int[VariantCount];
        var hasOutOfRangeKind = false;

        foreach (var value in values)
        {
            var kind = getKind(value);
            if (kind < 0 || kind >= VariantCount)
            {
                hasOutOfRangeKind = true;
                continue;
            }

            counts[kind]++;
        }

        var typeName = typeof(T).Name;
        var firstKindInRange = counts[0] >= values.Length * 0.79 && counts[0] <= values.Length * 0.81;
        var nonzeroKindsInRange = counts.Skip(1).All(count => count >= values.Length * 0.02 && count <= values.Length * 0.04);
        if (hasOutOfRangeKind || values.Length != counts.Sum() || !firstKindInRange || !nonzeroKindsInRange)
            failures.Add($"{typeName}: histogram [{string.Join(", ", counts)}], out-of-range={hasOutOfRangeKind}.");
    }

    private static int[] FillKinds<T>(
        T[] values,
        Action<T[], Random, Distribution> fill,
        Func<T, int> getKind)
    {
        fill(values, new Random(Seed), Distribution.Skewed80);
        return values.Select(getKind).ToArray();
    }

    private static int EventRecordKind(EventRecord value) => value switch
    {
        EventRecord.Point => 0,
        EventRecord.Circle => 1,
        EventRecord.Label => 2,
        EventRecord.Rectangle => 3,
        EventRecord.ColoredLine => 4,
        EventRecord.Transform => 5,
        EventRecord.RichText => 6,
        EventRecord.Error => 7,
        _ => throw new InvalidOperationException("Unknown EventRecord variant."),
    };

    private static int EventNativeKind(EventNative value) => value switch
    {
        EvtPoint => 0,
        EvtCircle => 1,
        EvtLabel => 2,
        EvtRectangle => 3,
        EvtColoredLine => 4,
        EvtTransform => 5,
        EvtRichText => 6,
        EvtError => 7,
        _ => throw new InvalidOperationException("Unknown EventNative variant."),
    };

    private static int EventJsonRecordKind(EventJsonRecord value) => value switch
    {
        EventJsonRecord.Point => 0,
        EventJsonRecord.Circle => 1,
        EventJsonRecord.Label => 2,
        EventJsonRecord.Rectangle => 3,
        EventJsonRecord.ColoredLine => 4,
        EventJsonRecord.Transform => 5,
        EventJsonRecord.RichText => 6,
        EventJsonRecord.Error => 7,
        _ => throw new InvalidOperationException("Unknown EventJsonRecord variant."),
    };

    private static int PhysicsRecordKind(PhysicsRecord value) => value switch
    {
        PhysicsRecord.Idle => 0,
        PhysicsRecord.Impulse => 1,
        PhysicsRecord.Position => 2,
        PhysicsRecord.Force => 3,
        PhysicsRecord.Rotation => 4,
        PhysicsRecord.Spring => 5,
        PhysicsRecord.Gravity => 6,
        PhysicsRecord.Collision => 7,
        _ => throw new InvalidOperationException("Unknown PhysicsRecord variant."),
    };

    private static int PhysicsNativeKind(PhysicsNative value) => value switch
    {
        PhysIdle => 0,
        PhysImpulse => 1,
        PhysPosition => 2,
        PhysForce => 3,
        PhysRotation => 4,
        PhysSpring => 5,
        PhysGravity => 6,
        PhysCollision => 7,
        _ => throw new InvalidOperationException("Unknown PhysicsNative variant."),
    };
}
