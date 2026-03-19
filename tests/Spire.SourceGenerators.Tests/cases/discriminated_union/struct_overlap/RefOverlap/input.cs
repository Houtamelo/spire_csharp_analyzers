using Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Message
    {
        [Variant] static partial void Text(string content);
        [Variant] static partial void Error(object detail);
    }
}
