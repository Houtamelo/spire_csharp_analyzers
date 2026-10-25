using Houtamelo.Spire;

namespace Houtamelo.Spire.Benchmarks.Types;

// The benchmark suite measures the single current struct representation.
[DiscriminatedUnion]
public partial struct VariantPayloadBenchmarkUnion
{
    [Variant] public static partial VariantPayloadBenchmarkUnion Empty();
    [Variant] public static partial VariantPayloadBenchmarkUnion Transform(float x, float y, float z, float w);
    [Variant] public static partial VariantPayloadBenchmarkUnion Label(string text);
}
