using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    readonly partial struct Result
    {
        [Variant] public static partial Result Ok(int value);
        [Variant] public static partial Result Err(string message);
    }

    class Consumer
    {
        int Test(Result r) => r switch
        {
            { kind: Result.Kind.Ok, value: var v } => 1,
        };
    }
}
