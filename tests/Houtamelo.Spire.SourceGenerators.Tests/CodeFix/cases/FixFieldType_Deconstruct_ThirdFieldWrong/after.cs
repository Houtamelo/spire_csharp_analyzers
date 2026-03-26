using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Vector3
    {
        [Variant] public static partial Vector3 Coords(int x, float y, double z);
        [Variant] public static partial Vector3 Origin();
    }

    class Consumer
    {
        int Test(Vector3 v) => v switch
        {
            (Vector3.Kind.Coords, var x, var y, double bad) => 1,
            (Vector3.Kind.Origin, _) => 2,
        };
    }
}
