//@ should_fail
// SPIRE_DU001: nested types not supported
using Spire;
namespace TestNs
{
    class Outer
    {
        [DiscriminatedUnion]
        partial struct Inner //~ ERROR
        {
            [Variant] public static partial Inner A(int x);
        }
    }
}
