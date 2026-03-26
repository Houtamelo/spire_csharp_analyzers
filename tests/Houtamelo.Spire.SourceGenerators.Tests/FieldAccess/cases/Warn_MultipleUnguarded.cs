//@ should_fail
// SPIRE014: two different variant fields accessed without guard in same method
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Command
    {
        [Variant] public static partial Command Move(int moveDistance);
        [Variant] public static partial Command Rotate(float rotateAngle);
    }
    class C
    {
        string Test(Command cmd)
        {
            var d = cmd.moveDistance; //~ ERROR
            var a = cmd.rotateAngle; //~ ERROR
            return $"{d} {a}";
        }
    }
}
