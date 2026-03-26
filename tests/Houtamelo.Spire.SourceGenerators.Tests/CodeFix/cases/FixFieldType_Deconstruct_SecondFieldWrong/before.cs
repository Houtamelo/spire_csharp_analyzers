using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Triple
    {
        [Variant] public static partial Triple Entry(int a, float b, string c);
        [Variant] public static partial Triple Empty();
    }

    class Consumer
    {
        int Test(Triple t) => t switch
        {
            (Triple.Kind.Entry, int a, string bad, var c) => 1,
            (Triple.Kind.Empty, _) => 2,
        };
    }
}
