//@ should_fail
// SPIRE013: switch statement — accessing Square's field inside Circle case body
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Vehicle
    {
        [Variant] public static partial Vehicle Car(int doors);
        [Variant] public static partial Vehicle Bike(double wheelDiameter);
    }
    class C
    {
        double Test(Vehicle v)
        {
            switch (v)
            {
                case { kind: Vehicle.Kind.Car }:
                    return v.bike.wheelDiameter; //~ ERROR
                default:
                    return 0;
            }
        }
    }
}
