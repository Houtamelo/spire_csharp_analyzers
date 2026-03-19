using Spire;

namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedTuple)]
    readonly partial struct Immutable
    {
        [Variant] static partial void A(int x);
        [Variant] static partial void B(string y);
    }
}
