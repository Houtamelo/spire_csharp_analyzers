//@ should_pass
// BoxedFields layout — no EditorBrowsable variant fields, analyzer doesn't fire
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedFields)]
    partial struct Token
    {
        [Variant] public static partial Token Ident(string name);
        [Variant] public static partial Token Number(int value);
    }
    class C
    {
        Token.Kind Test(Token t) => t.kind;
    }
}
