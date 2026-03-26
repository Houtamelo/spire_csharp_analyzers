using Houtamelo.Spire;

namespace TestNs
{
    public struct Point { public int X; public int Y; }

    [DiscriminatedUnion]
    partial struct Drawing
    {
        [Variant] public static partial Drawing Dot(Point location);
        [Variant] public static partial Drawing Line(Point start, Point end);
    }
}
