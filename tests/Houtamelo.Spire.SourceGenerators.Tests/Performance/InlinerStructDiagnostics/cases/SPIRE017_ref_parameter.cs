//@ should_fail
// SPIRE017: ref parameter is not supported by [InlinerStruct].
using Houtamelo.Spire;
namespace TestNs
{
    public static partial class Samples
    {
        [InlinerStruct]
        public static void Take(ref int x) //~ ERROR
        { }
    }
}
