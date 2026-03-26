//@ should_pass
// No diagnostics: Auto on generic struct silently uses BoxedFields
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Option<T>
    {
        [Variant] public static partial Option<T> Some(T value);
        [Variant] public static partial Option<T> None();
    }
}
