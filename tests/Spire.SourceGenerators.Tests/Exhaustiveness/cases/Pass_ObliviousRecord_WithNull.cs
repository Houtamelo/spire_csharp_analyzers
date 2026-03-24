//@ should_pass
// No #nullable — oblivious context, but null arm present — no diagnostic
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public partial record Option<T>
    {
        public partial record Some(T Value) : Option<T>;
        public partial record None() : Option<T>;
    }
    class C
    {
        int Test(Option<int> o) => o switch
        {
            null => -1,
            Option<int>.Some { Value: var v } => v,
            Option<int>.None => 0,
        };
    }
}
