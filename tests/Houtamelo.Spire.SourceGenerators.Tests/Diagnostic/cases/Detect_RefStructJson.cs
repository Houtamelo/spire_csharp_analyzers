//@ should_fail
// SPIRE_DU008: ref struct cannot use JSON generation
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
    ref partial struct Foo //~ ERROR
    {
        [Variant] public static partial Foo A(int x);
    }
}
