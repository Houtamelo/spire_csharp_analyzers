//@ should_pass
// No diagnostics: readonly partial struct with default auto layout and variants
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    readonly partial struct Color
    {
        [Variant] public static partial Color Red();
        [Variant] public static partial Color Green();
        [Variant] public static partial Color Blue();
    }
}
