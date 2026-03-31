//@ should_fail
// Abstract class with 3 sealed subtypes, switch expression covers only Dog, missing Cat and Bird — SPIRE009
#nullable enable
using Houtamelo.Spire;
namespace TestNs
{
    [EnforceExhaustiveness]
    public abstract class Vehicle { }
    public sealed class Car : Vehicle { }
    public sealed class Truck : Vehicle { }
    public sealed class Motorcycle : Vehicle { }

    class Consumer
    {
        string Describe(Vehicle v) => v switch //~ ERROR
        {
            Car => "car",
        };
    }
}
