using Spire;

namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedFields)]
    partial struct Light
    {
        [Variant] static partial void Red();
        [Variant] static partial void Yellow();
        [Variant] static partial void Green();
    }
}
