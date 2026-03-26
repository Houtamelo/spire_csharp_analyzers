using Houtamelo.Spire;

namespace My.Deep.Namespace
{
    [DiscriminatedUnion]
    partial struct Result
    {
        [Variant] public static partial Result Ok(int value);
        [Variant] public static partial Result Err(string message);
    }
}
