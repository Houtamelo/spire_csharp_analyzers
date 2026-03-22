namespace Spire.Benchmarks.Types;

// ── Scenario 1: WideTypes ──
// Many unique field types across variants → maximum slot/field count.
// Exposes: Additive (slot explosion — one slot per unique type combo)
//          BoxedFields (many object? slots, every access boxes)
[BenchmarkUnion(Name = "Wide")]
public partial struct WideBench
{
    [Variant] public static partial WideBench IntOnly(int ia, int ib);
    [Variant] public static partial WideBench FloatOnly(float fa, float fb);
    [Variant] public static partial WideBench DoubleOnly(double da, double db);
    [Variant] public static partial WideBench LongOnly(long la, long lb);
    [Variant] public static partial WideBench Mixed(int ia, float fb, double dc, long ld);
    [Variant] public static partial WideBench Empty();
}

// ── Scenario 2: AllManaged ──
// Only reference-type fields + one numeric for measurability.
// Exposes: Overlap (can't overlap managed refs, no size benefit)
//          UnsafeOverlap (byte buffer unused, falls back to object? slots)
[BenchmarkUnion(Name = "Managed")]
public partial struct ManagedBench
{
    [Variant] public static partial ManagedBench One(string a, int len);
    [Variant] public static partial ManagedBench Two(string a, string b, int len);
    [Variant] public static partial ManagedBench Three(string a, string b, string c, int len);
    [Variant] public static partial ManagedBench None();
}

// ── Scenario 3: BigVariant ──
// One variant with 8 fields, others small.
// Exposes: All struct strategies (struct size dominated by largest variant)
//          Copy benchmarks penalize large structs
//          BoxedTuple (8-element tuple creation + cast)
[BenchmarkUnion(Name = "Big")]
public partial struct BigBench
{
    [Variant] public static partial BigBench Huge(float a, float b, float c, float d, float e, float f, float g, float h);
    [Variant] public static partial BigBench Small(float x, float y);
    [Variant] public static partial BigBench Empty();
}

// ── Scenario 4: ManyVariants ──
// 16 variants with uniform field types (float) — tests variant count scaling.
// Exposes: Switch dispatch overhead with many arms
//          Branch predictor stress (16-way uniform distribution)
[BenchmarkUnion(Name = "Many")]
public partial struct ManyBench
{
    [Variant] public static partial ManyBench V00();
    [Variant] public static partial ManyBench V01(float a);
    [Variant] public static partial ManyBench V02(float a, float b);
    [Variant] public static partial ManyBench V03(float a, float b, float c);
    [Variant] public static partial ManyBench V04(float a, float b, float c, float d);
    [Variant] public static partial ManyBench V05(float a, float b, float c, float d, float e);
    [Variant] public static partial ManyBench V06(float a);
    [Variant] public static partial ManyBench V07(float a, float b);
    [Variant] public static partial ManyBench V08(float a, float b, float c);
    [Variant] public static partial ManyBench V09(float a, float b, float c, float d);
    [Variant] public static partial ManyBench V10(float a, float b, float c, float d, float e);
    [Variant] public static partial ManyBench V11(float a);
    [Variant] public static partial ManyBench V12(float a, float b);
    [Variant] public static partial ManyBench V13(float a, float b, float c);
    [Variant] public static partial ManyBench V14(float a, float b, float c, float d);
    [Variant] public static partial ManyBench V15(float a, float b, float c, float d, float e);
}
