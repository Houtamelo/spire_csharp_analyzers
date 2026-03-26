using Spire;

namespace TestNs;

[DiscriminatedUnion(Layout.BoxedFields, Json = JsonLibrary.SystemTextJson)]
partial struct Option<T>
{
    [Variant] public static partial Option<T> Some(T value);
    [Variant] public static partial Option<T> None();
}
