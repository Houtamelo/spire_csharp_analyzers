using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Token
    {
        [Variant] public static partial Token Ident(string name);
        [Variant] public static partial Token Number(int value);
        [Variant] public static partial Token Eof();
    }
}
