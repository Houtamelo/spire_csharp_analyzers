using Spire;

namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedFields)]
    readonly partial struct Immutable
    {
        [Variant] public static partial Immutable A(int x);
        [Variant] public static partial Immutable B(string y);
    }
}
