//@ should_fail
// SPIRE019: instance method with 8 parameters has total arity 9 (receiver counted).
using Houtamelo.Spire;
namespace TestNs
{
    public partial class Host
    {
        [InlinerStruct]
        public void Take(int a, int b, int c, int d, int e, int f, int g, int h) { } //~ ERROR
    }
}
