#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
public partial struct Option<T> where T : class
{
    [Variant] public static partial Option<T> Some(T? value);
    [Variant] public static partial Option<T> None();
}
