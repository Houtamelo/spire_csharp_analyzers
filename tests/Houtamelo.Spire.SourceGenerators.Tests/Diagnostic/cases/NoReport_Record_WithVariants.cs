//@ should_pass
// No diagnostics: record union with two proper nested variant records
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public abstract partial record Vehicle
    {
        public sealed partial record Car(string Model, int Year) : Vehicle;
        public sealed partial record Truck(double PayloadTons) : Vehicle;
    }
}
