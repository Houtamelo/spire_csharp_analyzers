using Spire;

namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedTuple)]
    partial struct Option<T>
    {
        [Variant] public static partial Option<T> Some(T value);
        [Variant] public static partial Option<T> None();
    }
}
