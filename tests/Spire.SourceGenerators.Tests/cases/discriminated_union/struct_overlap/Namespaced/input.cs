using Spire;

namespace My.Deep.Namespace
{
    [DiscriminatedUnion]
    partial struct Result
    {
        [Variant] static partial void Ok(int value);
        [Variant] static partial void Err(string message);
    }
}
