//@ should_pass
// readonly partial struct — correct field accessed inside matching switch arm
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    readonly partial struct Result
    {
        [Variant] public static partial Result Ok(int okValue);
        [Variant] public static partial Result Err(string errMsg);
    }
    class C
    {
        int Test(Result r) => r switch
        {
            (Result.Kind.Ok, _) => r.okValue,
            _ => -1,
        };
    }
}
