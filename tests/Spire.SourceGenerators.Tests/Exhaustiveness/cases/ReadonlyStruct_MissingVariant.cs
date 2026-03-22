//@ should_fail
// readonly struct Result missing Err variant — SPIRE009
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    readonly partial struct Result
    {
        [Variant] public static partial Result Ok(int value);
        [Variant] public static partial Result Err(string message);
    }

    class ReadonlyResultConsumer
    {
        string Describe(Result r) => r switch //~ ERROR
        {
            (Result.Kind.Ok, int v) => $"ok:{v}",
        };
    }
}
