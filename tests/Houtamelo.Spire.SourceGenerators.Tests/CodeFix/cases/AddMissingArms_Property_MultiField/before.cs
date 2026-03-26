using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Vec
    {
        [Variant] public static partial Vec V2(float x, float y);
        [Variant] public static partial Vec V3(float x, float y, float z);
        [Variant] public static partial Vec V4(float x, float y, float z, float w);
    }

    class Consumer
    {
        int Test(Vec v) => v switch
        {
            { kind: Vec.Kind.V2, x: var a, y: var b } => 2,
        };
    }
}
