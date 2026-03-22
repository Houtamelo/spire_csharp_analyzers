//@ should_fail
// SPIRE_DU003: no variants found on class with no nested inheriting classes
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public abstract partial class EmptyClass //~ ERROR
    {
    }
}
