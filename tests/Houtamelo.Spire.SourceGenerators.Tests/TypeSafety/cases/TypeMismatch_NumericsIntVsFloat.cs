//@ should_fail
using Houtamelo.Spire;
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
            { kind: MeasureUnion.Kind.Length, length: { meters: int bad } } => 1, //~ ERROR
            { kind: MeasureUnion.Kind.Weight, weight: { kilos: double w } } => 2,
        };
    }
}
