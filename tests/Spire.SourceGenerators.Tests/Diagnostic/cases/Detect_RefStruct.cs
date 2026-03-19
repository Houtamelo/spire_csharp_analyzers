//@ should_fail
// SPIRE_DU002: ref struct not supported
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    ref partial struct Foo //~ ERROR
    {
        [Variant] static partial void A(int x);
    }
}
