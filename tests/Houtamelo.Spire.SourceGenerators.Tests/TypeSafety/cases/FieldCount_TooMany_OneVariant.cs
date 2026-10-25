//@ should_pass
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
            // Roslyn owns invalid positional arity after the payload accessor binds.
            { kind: ShapeFC1.Kind.Rect, rect: (float x, float y, string extra) } => 1,
            _ => 0,
        };
    }
}
