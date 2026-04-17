//@ should_fail
// SPIRE017: out parameter is not supported by [InlinerStruct].
using Houtamelo.Spire;
namespace TestNs
{
    public static partial class Samples
    {
        [InlinerStruct]
        public static void Take(out int x) //~ ERROR
        { x = 0; }
    }
}
