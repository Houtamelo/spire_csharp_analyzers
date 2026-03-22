//@ should_fail
// Switch inside local function, missing LocalErr variant — SPIRE009
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct LocalResult
    {
        [Variant] public static partial LocalResult LocalOk(int localCode);
        [Variant] public static partial LocalResult LocalErr(string localMsg);
    }

    class LocalFunctionConsumer
    {
        int Compute(LocalResult r)
        {
            return Inner(r);

            int Inner(LocalResult result) => result switch //~ ERROR
            {
                (LocalResult.Kind.LocalOk, int c) => c,
            };
        }
    }
}
