using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    partial struct ParameterMapping
    {
        [Variant] public static partial ParameterMapping First(int x, int y);
        [Variant] public static partial ParameterMapping Second(int left, int right);
    }
}
