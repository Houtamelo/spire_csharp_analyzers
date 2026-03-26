//@ should_fail
// Circle arm has when guard AND WhenSquare variant is missing entirely — SPIRE009
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct WhenShape
    {
        [Variant] public static partial WhenShape WhenCircle(double whenRadius);
        [Variant] public static partial WhenShape WhenSquare(int whenSide);
    }

    class WhenGuardAndMissingConsumer
    {
        int Test(WhenShape s) => s switch //~ ERROR
        {
            (WhenShape.Kind.WhenCircle, double r) when r > 0 => 1,
        };
    }
}
