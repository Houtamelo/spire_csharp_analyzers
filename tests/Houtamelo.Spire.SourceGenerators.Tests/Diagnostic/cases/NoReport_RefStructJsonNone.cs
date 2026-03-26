//@ should_pass
// ref struct with Json = None should not report SPIRE_DU008
using Spire;
namespace TestNs
{
    [DiscriminatedUnion(Json = JsonLibrary.None)]
    ref partial struct Foo
    {
        [Variant] public static partial Foo A(int x);
    }
}
