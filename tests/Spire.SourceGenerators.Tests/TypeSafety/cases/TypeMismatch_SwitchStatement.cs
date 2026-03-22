//@ should_fail
using Spire;
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
                case (TokenStmt.Kind.Ident, double bad): //~ ERROR
                    return 1;
                case (TokenStmt.Kind.Number, int n):
                    return n;
                default:
                    return 0;
            }
        }
    }
}
