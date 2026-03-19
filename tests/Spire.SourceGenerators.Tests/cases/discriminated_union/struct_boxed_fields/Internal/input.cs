using Spire;

namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedFields)]
    internal partial struct Internal
    {
        [Variant] static partial void X(int v);
    }
}
