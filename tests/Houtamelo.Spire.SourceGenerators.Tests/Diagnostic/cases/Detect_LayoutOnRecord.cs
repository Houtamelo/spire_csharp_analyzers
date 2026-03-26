//@ should_fail
// SPIRE_DU004: layout ignored for record
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion(Layout.Overlap)]
    partial record Foo //~ ERROR
    {
        partial record A(int X) : Foo;
    }
}
