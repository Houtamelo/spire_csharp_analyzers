//@ should_pass
using Houtamelo.Spire;
namespace TestNs
{
    public partial class Host
    {
        [InlinerStruct]
        public void Take(int a, int b, int c, int d, int e, int f, int g) { }
    }
}
