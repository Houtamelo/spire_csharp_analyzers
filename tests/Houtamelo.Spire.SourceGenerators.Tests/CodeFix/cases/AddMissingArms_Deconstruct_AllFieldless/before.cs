using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Light
    {
        [Variant] public static partial Light Red();
        [Variant] public static partial Light Yellow();
        [Variant] public static partial Light Green();
    }

    class Consumer
    {
        int Test(Light l) => l switch
        {
            (Light.Kind.Red, _) => 1,
            (Light.Kind.Yellow, _) => 2,
        };
    }
}
