using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Pair
    {
        [Variant] public static partial Pair Entry(int a, float b);
        [Variant] public static partial Pair Empty();
    }

    class Consumer
    {
        int Test(Pair p) => p switch
        {
            { kind: Pair.Kind.Entry, a: var bad1, b: string bad2 } => 1,
            { kind: Pair.Kind.Empty } => 2,
        };
    }
}
