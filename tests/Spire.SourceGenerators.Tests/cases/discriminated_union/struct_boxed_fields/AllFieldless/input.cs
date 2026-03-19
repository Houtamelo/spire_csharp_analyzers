using Spire;

namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedFields)]
    partial struct Light
    {
        [Variant] public static partial Light Red();
        [Variant] public static partial Light Yellow();
        [Variant] public static partial Light Green();
    }
}
