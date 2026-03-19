//@ should_pass
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Token
    {
        [Variant] public static partial Token Ident(string name);
        [Variant] public static partial Token Number(int value);
        [Variant] public static partial Token Eof();
    }
    class C
    {
        int Test(Token t) => t switch
        {
            (Token.Kind.Ident, object? val) => 1,
            (Token.Kind.Number, object? val) => 2,
            (Token.Kind.Eof, _) => 3,
        };
    }
}
