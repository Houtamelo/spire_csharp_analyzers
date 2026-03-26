//@ should_fail
// SPIRE_DU003: no variants found on record with no nested inheriting records
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public abstract partial record EmptyRecord //~ ERROR
    {
    }
}
