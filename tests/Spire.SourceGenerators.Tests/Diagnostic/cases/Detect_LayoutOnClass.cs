//@ should_fail
// SPIRE_DU004: layout ignored for class
using Spire;
namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedFields)]
    partial class Bar //~ ERROR
    {
        partial class X : Bar { }
    }
}
