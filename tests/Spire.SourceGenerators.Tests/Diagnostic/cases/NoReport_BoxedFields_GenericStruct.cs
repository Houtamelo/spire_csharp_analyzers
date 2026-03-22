//@ should_pass
// No diagnostics: BoxedFields layout on generic struct is valid
using Spire;
namespace TestNs
{
    [DiscriminatedUnion(Layout.BoxedFields)]
    partial struct Container<T>
    {
        [Variant] public static partial Container<T> Full(T item);
        [Variant] public static partial Container<T> Empty();
    }
}
