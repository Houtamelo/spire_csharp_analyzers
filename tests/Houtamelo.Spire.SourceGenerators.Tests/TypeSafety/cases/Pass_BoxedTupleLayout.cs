//@ should_pass
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Token
    {
        [Variant] public static partial Token Ident(string name);
        [Variant] public static partial Token Number(int value);
    }
    class C
    {
        int Test(Token t) => t switch
        {
            { kind: Token.Kind.Ident, ident: { name: var val } } => 1,
            { kind: Token.Kind.Number, number: { value: var val } } => 2,
        };
    }
}
