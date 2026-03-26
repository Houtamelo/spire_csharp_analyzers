//@ should_pass
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct TokenPass
    {
        [Variant] public static partial TokenPass Ident(string identName);
        [Variant] public static partial TokenPass Number(int numVal);
    }
    class CStmtPass
    {
        int Test(TokenPass t)
        {
            switch (t)
            {
                case (TokenPass.Kind.Ident, string name):
                    return name.Length;
                case (TokenPass.Kind.Number, int n):
                    return n;
                default:
                    return 0;
            }
        }
    }
}
