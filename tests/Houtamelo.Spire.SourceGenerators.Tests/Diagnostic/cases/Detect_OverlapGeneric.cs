//@ should_fail
// SPIRE_DU005: generic struct cannot use Overlap
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion(Layout.Overlap)]
    partial struct Wrapper<T> //~ ERROR
    {
        [Variant] public static partial Wrapper<T> Some(T value);
        [Variant] public static partial Wrapper<T> None();
    }
}
