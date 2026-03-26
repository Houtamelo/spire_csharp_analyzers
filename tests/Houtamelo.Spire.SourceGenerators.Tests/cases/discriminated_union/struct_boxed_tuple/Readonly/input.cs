using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedTuple)]
    readonly partial struct Immutable
    {
        [Variant] public static partial Immutable A(int x);
        [Variant] public static partial Immutable B(string y);
    }
}
