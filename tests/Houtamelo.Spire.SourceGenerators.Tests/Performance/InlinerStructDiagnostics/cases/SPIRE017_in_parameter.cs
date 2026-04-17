//@ should_fail
// SPIRE017: in parameter is not supported by [InlinerStruct].
using Houtamelo.Spire;
namespace TestNs
{
    public static partial class Samples
    {
        [InlinerStruct]
        public static void Take(in int x) //~ ERROR
        { }
    }
}
