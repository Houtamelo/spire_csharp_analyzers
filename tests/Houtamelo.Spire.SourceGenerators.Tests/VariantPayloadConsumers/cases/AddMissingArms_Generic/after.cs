using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Result<T> where T : unmanaged
{
    [Variant] public static partial Result<T> Ok(T value);
    [Variant] public static partial Result<T> Error(int code);
}

public static class Consumer
{
    public static int Match(Result<int> result) => result switch
    {
        { kind: Result<int>.Kind.Ok, ok: { value: var value } } => value,
        { kind: Result<int>.Kind.Error, error: { code: var code } } => throw new System.NotImplementedException()
    };
}
