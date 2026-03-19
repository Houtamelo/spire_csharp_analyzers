using Spire;

namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedFields)]
    partial struct Token
    {
        [Variant] public static partial Token Ident(string name);
        [Variant] public static partial Token Number(int value);
        [Variant] public static partial Token Eof();
    }
}
