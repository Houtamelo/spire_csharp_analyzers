using Spire;

namespace My.Deep.Namespace
{
    [DiscriminatedUnion(Layout.BoxedTuple)]
    partial struct Result
    {
        [Variant] public static partial Result Ok(int value);
        [Variant] public static partial Result Err(string message);
    }
}
