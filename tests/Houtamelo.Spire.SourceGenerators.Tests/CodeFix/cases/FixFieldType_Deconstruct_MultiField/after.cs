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
            (Rect.Kind.Box, float bad, var a, var b, var c) => 1,
            (Rect.Kind.Point, var a, var b) => 2,
        };
    }
}
