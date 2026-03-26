using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Rect
    {
        [Variant] public static partial Rect Box(float x, float y, float w, float h);
        [Variant] public static partial Rect Point(float x, float y);
    }

    class Consumer
    {
        int Test(Rect r) => r switch
        {
            { kind: Rect.Kind.Box, x: var bad, y: var a, w: var b, h: var c } => 1,
            { kind: Rect.Kind.Point, x: var a, y: var b } => 2,
        };
    }
}
