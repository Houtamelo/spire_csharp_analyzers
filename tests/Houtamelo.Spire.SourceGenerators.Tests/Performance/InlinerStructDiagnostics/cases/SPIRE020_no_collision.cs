//@ should_pass
using Houtamelo.Spire;
namespace TestNs
{
    public partial class Host
    {
        [InlinerStruct]
        public static int Foo() => 0;
    }
}
