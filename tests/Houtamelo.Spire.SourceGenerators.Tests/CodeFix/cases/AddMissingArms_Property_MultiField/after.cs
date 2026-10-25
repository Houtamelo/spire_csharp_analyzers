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
            { kind: Vec.Kind.V3, v3: { x: var x, y: var y, z: var z  } } => throw new System.NotImplementedException(),
            { kind: Vec.Kind.V4, v4: { x: var x, y: var y, z: var z, w: var w  } } => throw new System.NotImplementedException()
        };
    }
}
