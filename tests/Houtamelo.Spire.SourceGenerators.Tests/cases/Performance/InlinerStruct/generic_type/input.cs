using Houtamelo.Spire;

namespace TestNs;

public partial class Box<T>
{
    [InlinerStruct]
    public T Get() => default!;
}
