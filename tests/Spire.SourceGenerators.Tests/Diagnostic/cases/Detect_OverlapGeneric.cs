//@ should_fail
// SPIRE_DU005: generic struct cannot use Overlap
using Spire;
namespace TestNs
{
    [DiscriminatedUnion(Layout.Overlap)]
    partial struct Wrapper<T> //~ ERROR
    {
        [Variant] static partial void Some(T value);
        [Variant] static partial void None();
    }
}
