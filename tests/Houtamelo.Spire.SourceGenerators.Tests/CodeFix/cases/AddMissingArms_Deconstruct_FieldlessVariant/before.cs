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

    class Consumer
    {
        int Test(Token t) => t switch
        {
            (Token.Kind.Ident, var n) => 1,
            (Token.Kind.Number, var v) => 2,
        };
    }
}
