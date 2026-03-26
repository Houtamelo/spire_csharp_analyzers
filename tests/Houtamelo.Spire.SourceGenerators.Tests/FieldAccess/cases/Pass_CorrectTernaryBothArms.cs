//@ should_pass
// Ternary true branch uses the correct guarded variant's field
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Sensor
    {
        [Variant] public static partial Sensor Temperature(double celsius);
        [Variant] public static partial Sensor Pressure(float pascals);
    }
    class C
    {
        double Test(Sensor s) =>
            s.kind == Sensor.Kind.Temperature ? s.celsius : 0;
    }
}
