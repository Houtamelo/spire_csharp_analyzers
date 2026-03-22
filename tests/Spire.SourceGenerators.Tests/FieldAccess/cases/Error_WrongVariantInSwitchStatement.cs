//@ should_fail
// SPIRE013: switch statement — accessing Square's field inside Circle case body
using Spire;
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
                case (Vehicle.Kind.Car, _):
                    return v.wheelDiameter; //~ ERROR
                default:
                    return 0;
            }
        }
    }
}
