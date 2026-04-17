//@ should_fail
// SPIRE027: Outer is not declared partial.
using Houtamelo.Spire;
namespace TestNs
{
    public class Outer
    {
        public partial class Inner
        {
            [InlinerStruct]
            public static int Double(int x) => x * 2; //~ ERROR
        }
    }
}
