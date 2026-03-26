//@ should_pass
#nullable enable
// CS8509 suppressed — all record variants covered
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
        int Test(Option<int> o) => o switch
        {
            Option<int>.Some { Value: var v } => v,
            Option<int>.None => 0,
        };
    }
}
