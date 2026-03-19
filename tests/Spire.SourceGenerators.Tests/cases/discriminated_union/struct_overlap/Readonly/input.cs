using Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    readonly partial struct Immutable
    {
        [Variant] static partial void A(int x);
        [Variant] static partial void B(string y);
    }
}
