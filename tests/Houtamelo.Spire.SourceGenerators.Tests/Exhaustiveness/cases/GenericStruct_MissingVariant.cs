//@ should_fail
// Generic struct Option<T> switch missing None variant — SPIRE009
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Option<T>
    {
        [Variant] public static partial Option<T> Some(T val);
        [Variant] public static partial Option<T> None();
    }

    class GenericOptionConsumer
    {
        int Unwrap(Option<int> opt) => opt switch //~ ERROR
        {
            (Option<int>.Kind.Some, int v) => v,
        };
    }
}
