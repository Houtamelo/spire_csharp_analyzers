//@ should_fail
// SPIRE018: [InlinerStruct] on a ref struct is unsupported.
using Houtamelo.Spire;
namespace TestNs
{
    public ref partial struct Gadget
    {
        [InlinerStruct]
        public void Do() { } //~ ERROR
    }
}
