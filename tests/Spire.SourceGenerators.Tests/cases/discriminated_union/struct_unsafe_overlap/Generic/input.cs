using Spire;

[DiscriminatedUnion(Layout.UnsafeOverlap)]
partial struct Result<T>
{
    [Variant] public static partial Result<T> Ok(T value, int code);
    [Variant] public static partial Result<T> Err(int code);
}
