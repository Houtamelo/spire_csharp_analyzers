using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Payload
    {
        [Variant] public static partial Payload Single(int item);
        [Variant] public static partial Payload Batch(int[] items);
    }

    class Consumer
    {
        int Test(Payload p) => p switch
        {
            { kind: Payload.Kind.Single, item: var i } => i,
        };
    }
}
