//@ should_fail
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct ShapeFC1
    {
        [Variant] public static partial ShapeFC1 Dot(double dotX);
        [Variant] public static partial ShapeFC1 Rect(float rectWidth, float rectHeight);
    }
    class CFC1
    {
        int Test(ShapeFC1 s) => s switch
        {
            (ShapeFC1.Kind.Dot, double x, string extra) => 1, //~ ERROR
            _ => 0,
        };
    }
}
