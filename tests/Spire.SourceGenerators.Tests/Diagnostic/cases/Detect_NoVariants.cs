//@ should_fail
// SPIRE_DU003: no variants found
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Empty //~ ERROR
    {
    }
}
