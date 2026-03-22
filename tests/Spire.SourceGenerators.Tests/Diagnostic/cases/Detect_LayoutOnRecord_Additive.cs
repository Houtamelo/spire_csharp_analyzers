//@ should_fail
// SPIRE_DU004: layout parameter ignored for record union
using Spire;
namespace TestNs
{
    [DiscriminatedUnion(Layout.Additive)]
    public abstract partial record Fruit //~ ERROR
    {
        public sealed partial record Apple(double WeightKg) : Fruit;
        public sealed partial record Banana(int LengthCm) : Fruit;
    }
}
