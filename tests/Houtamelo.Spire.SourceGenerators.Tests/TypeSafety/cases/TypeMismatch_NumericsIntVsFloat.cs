//@ should_fail
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct MeasureUnion
    {
        [Variant] public static partial MeasureUnion Length(float meters);
        [Variant] public static partial MeasureUnion Weight(double kilos);
    }
    class CNumeric
    {
        int Test(MeasureUnion m) => m switch
        {
            (MeasureUnion.Kind.Length, int bad) => 1, //~ ERROR
            (MeasureUnion.Kind.Weight, double w) => 2,
        };
    }
}
