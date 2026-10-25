using Houtamelo.Spire;

[DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
partial struct JsonOptionAddStj<T>
{
    [Variant] public static partial JsonOptionAddStj<T> Some(T value);
    [Variant] public static partial JsonOptionAddStj<T> None();
}

[DiscriminatedUnion(json: JsonLibrary.NewtonsoftJson)]
partial struct JsonOptionAddNsj<T>
{
    [Variant] public static partial JsonOptionAddNsj<T> Some(T value);
    [Variant] public static partial JsonOptionAddNsj<T> None();
}
