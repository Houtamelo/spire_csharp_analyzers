//@ should_pass
using Houtamelo.Spire;
namespace TestNs
{
    public partial struct Foo
    {
        [InlinerStruct]
        public void Do() { }
    }
}
