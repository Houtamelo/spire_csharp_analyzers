//@ should_pass
// readonly struct Result all variants covered — no diagnostic
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    readonly partial struct ReadonlyResult
    {
        [Variant] public static partial ReadonlyResult ReadonlyOk(int okValue);
        [Variant] public static partial ReadonlyResult ReadonlyErr(string errMsg);
    }

    class PassReadonlyResultConsumer
    {
        string Describe(ReadonlyResult r) => r switch
        {
            (ReadonlyResult.Kind.ReadonlyOk, int v) => $"ok:{v}",
            (ReadonlyResult.Kind.ReadonlyErr, string m) => $"err:{m}",
        };
    }
}
