//@ should_pass
// Wildcard covers None — no diagnostic (refactoring only)
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
            Option<int>.Some { Value: var v } => v,
            _ => 0,
        };
    }
}
