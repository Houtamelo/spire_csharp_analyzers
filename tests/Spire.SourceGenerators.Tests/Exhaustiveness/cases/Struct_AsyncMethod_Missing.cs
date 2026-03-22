//@ should_fail
// Switch inside async Task<int> method, missing Err variant — SPIRE009
using Spire;
using System.Threading.Tasks;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct AsyncResult
    {
        [Variant] public static partial AsyncResult Success(int code);
        [Variant] public static partial AsyncResult Failure(string reason);
    }

    class AsyncConsumer
    {
        async Task<int> ProcessAsync(AsyncResult r)
        {
            await Task.Yield();
            return r switch //~ ERROR
            {
                (AsyncResult.Kind.Success, int c) => c,
            };
        }
    }
}
