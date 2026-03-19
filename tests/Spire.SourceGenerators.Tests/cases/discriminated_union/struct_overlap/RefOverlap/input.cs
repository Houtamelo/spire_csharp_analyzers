using Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Message
    {
        [Variant] public static partial Message Text(string content);
        [Variant] public static partial Message Error(object detail);
    }
}
