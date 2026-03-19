//@ should_pass
// All record variants covered
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial record Option<T>
    {
        partial record Some(T Value) : Option<T>;
        partial record None() : Option<T>;
    }
    class C
    {
        int Test(Option<int> o) => o switch
        {
            Option<int>.Some { Value: var v } => v,
            Option<int>.None => 0,
        };
    }
}
