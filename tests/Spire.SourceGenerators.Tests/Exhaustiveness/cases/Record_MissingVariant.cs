//@ should_fail
// Missing None variant
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
        int Test(Option<int> o) => o switch //~ ERROR
        {
            Option<int>.Some { Value: var v } => v,
        };
    }
}
