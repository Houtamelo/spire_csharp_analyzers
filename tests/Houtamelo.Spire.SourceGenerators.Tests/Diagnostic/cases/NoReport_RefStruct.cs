//@ should_pass
// ref struct is allowed for [DiscriminatedUnion]
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    ref partial struct Foo
    {
        [Variant] public static partial Foo A(int x);
    }
}
