//@ should_fail
// SPIRE_DU003: no variants found
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Empty //~ ERROR
    {
    }
}
