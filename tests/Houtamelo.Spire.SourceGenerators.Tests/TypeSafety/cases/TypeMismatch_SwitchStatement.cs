//@ should_fail
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct TokenStmt
    {
        [Variant] public static partial TokenStmt Ident(string name);
        [Variant] public static partial TokenStmt Number(int numValue);
    }
    class CStmt
    {
        int Test(TokenStmt t)
        {
            switch (t)
            {
                case { kind: TokenStmt.Kind.Ident, ident: { name: double bad } }: //~ ERROR
                    return 1;
                case { kind: TokenStmt.Kind.Number, number: { numValue: int n } }:
                    return n;
                default:
                    return 0;
            }
        }
    }
}
