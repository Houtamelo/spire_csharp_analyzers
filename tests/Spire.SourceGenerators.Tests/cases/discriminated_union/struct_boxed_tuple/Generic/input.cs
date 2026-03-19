using Spire;

namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedTuple)]
    partial struct Option<T>
    {
        [Variant] static partial void Some(T value);
        [Variant] static partial void None();
    }
}
