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
            { kind: Token.Kind.Ident, ident: { name: var n  } } => 1,
            { kind: Token.Kind.Number, number: { value: var v  } } => 2,
            { IsEof: true } => throw new System.NotImplementedException()
        };
    }
}
