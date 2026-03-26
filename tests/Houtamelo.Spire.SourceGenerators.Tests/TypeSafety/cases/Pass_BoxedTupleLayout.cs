//@ should_pass
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedTuple)]
    partial struct Token
    {
        [Variant] public static partial Token Ident(string name);
        [Variant] public static partial Token Number(int value);
    }
    class C
    {
        int Test(Token t) => t switch
        {
            (Token.Kind.Ident, object? val) => 1,
            (Token.Kind.Number, object? val) => 2,
        };
    }
}
