//@ should_pass
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct ExprFCP3
    {
        [Variant] public static partial ExprFCP3 Literal(int litValue);
        [Variant] public static partial ExprFCP3 Add(float addLeft, float addRight);
    }
    class CFCP3
    {
        int Test(ExprFCP3 e) => e switch
        {
            (ExprFCP3.Kind.Literal, int v) => v,
            (ExprFCP3.Kind.Add, float l, float r) => (int)(l + r),
        };
    }
}
