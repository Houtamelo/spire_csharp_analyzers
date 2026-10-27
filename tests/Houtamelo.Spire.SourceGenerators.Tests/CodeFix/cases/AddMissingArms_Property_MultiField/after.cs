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
            { kind: Vec.Kind.V2, v2: { x: var a, y: var b  } } => 2,
            { IsV3: true, v3: var (x, y, z) } => throw new System.NotImplementedException(),
            { IsV4: true, v4: var (x, y, z, w) } => throw new System.NotImplementedException()
        };
    }
}
