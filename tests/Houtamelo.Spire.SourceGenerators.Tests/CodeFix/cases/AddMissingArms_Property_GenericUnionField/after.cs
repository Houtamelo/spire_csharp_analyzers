using Houtamelo.Spire;
using System.Collections.Generic;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Bucket
    {
        [Variant] public static partial Bucket Empty();
        [Variant] public static partial Bucket Full(List<int> items);
    }

    class Consumer
    {
        int Test(Bucket b) => b switch
        {
            { kind: Bucket.Kind.Empty } => 0,
            { kind: Bucket.Kind.Full, items: List<int> items } => throw new System.NotImplementedException()
        };
    }
}
