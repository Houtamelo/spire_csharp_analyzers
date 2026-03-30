using Houtamelo.Spire;

[DiscriminatedUnion(Layout.Additive, json: JsonLibrary.SystemTextJson)]
partial struct JsonOptionAddStj<T>
{
    [Variant] public static partial JsonOptionAddStj<T> Some(T value);
    [Variant] public static partial JsonOptionAddStj<T> None();
}

[DiscriminatedUnion(Layout.BoxedFields, json: JsonLibrary.SystemTextJson)]
partial struct JsonOptionBfStj<T>
{
    [Variant] public static partial JsonOptionBfStj<T> Some(T value);
    [Variant] public static partial JsonOptionBfStj<T> None();
}

[DiscriminatedUnion(Layout.BoxedTuple, json: JsonLibrary.SystemTextJson)]
partial struct JsonOptionBtStj<T>
{
    [Variant] public static partial JsonOptionBtStj<T> Some(T value);
    [Variant] public static partial JsonOptionBtStj<T> None();
}

[DiscriminatedUnion(Layout.Additive, json: JsonLibrary.NewtonsoftJson)]
partial struct JsonOptionAddNsj<T>
{
    [Variant] public static partial JsonOptionAddNsj<T> Some(T value);
    [Variant] public static partial JsonOptionAddNsj<T> None();
}

[DiscriminatedUnion(Layout.BoxedFields, json: JsonLibrary.NewtonsoftJson)]
partial struct JsonOptionBfNsj<T>
{
    [Variant] public static partial JsonOptionBfNsj<T> Some(T value);
    [Variant] public static partial JsonOptionBfNsj<T> None();
}

[DiscriminatedUnion(Layout.BoxedTuple, json: JsonLibrary.NewtonsoftJson)]
partial struct JsonOptionBtNsj<T>
{
    [Variant] public static partial JsonOptionBtNsj<T> Some(T value);
    [Variant] public static partial JsonOptionBtNsj<T> None();
}
