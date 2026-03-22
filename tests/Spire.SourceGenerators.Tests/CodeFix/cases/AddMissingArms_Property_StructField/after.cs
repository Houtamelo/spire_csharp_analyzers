using Spire;
namespace TestNs
{
    struct Point { public int X; public int Y; }

    [DiscriminatedUnion]
    partial struct Geometry
    {
        [Variant] public static partial Geometry Origin();
        [Variant] public static partial Geometry Located(Point point);
    }

    class Consumer
    {
        int Test(Geometry g) => g switch
        {
            { kind: Geometry.Kind.Origin } => 0,
            { kind: Geometry.Kind.Located, point: Point point } => throw new System.NotImplementedException()
        };
    }
}
