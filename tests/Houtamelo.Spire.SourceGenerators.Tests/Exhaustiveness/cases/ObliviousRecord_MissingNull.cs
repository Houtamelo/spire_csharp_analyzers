//@ should_fail
// No #nullable — oblivious context, reference type is implicitly nullable
using Houtamelo.Spire;
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
        int Test(Option<int> o) => o switch //~ ERROR
        {
            Option<int>.Some { Value: var v } => v,
            Option<int>.None => 0,
        };
    }
}
