//@ should_fail
// SPIRE027: declaring type is not partial.
using Houtamelo.Spire;
namespace TestNs
{
    public class Host
    {
        [InlinerStruct]
        public static int Double(int x) => x * 2; //~ ERROR
    }
}
