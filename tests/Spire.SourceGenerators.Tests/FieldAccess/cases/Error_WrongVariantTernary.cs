//@ should_fail
// SPIRE013: ternary true branch uses wrong variant's field
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Measure
    {
        [Variant] public static partial Measure Distance(double meters);
        [Variant] public static partial Measure Weight(float kilograms);
    }
    class C
    {
        double Test(Measure m) =>
            m.kind == Measure.Kind.Distance ? m.kilograms : 0; //~ ERROR
    }
}
