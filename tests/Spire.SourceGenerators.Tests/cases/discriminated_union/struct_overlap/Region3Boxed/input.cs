using Spire;

namespace TestNs
{
    public struct Point { public int X; public int Y; }

    [DiscriminatedUnion]
    partial struct Drawing
    {
        [Variant] static partial void Dot(Point location);
        [Variant] static partial void Line(Point start, Point end);
    }
}
