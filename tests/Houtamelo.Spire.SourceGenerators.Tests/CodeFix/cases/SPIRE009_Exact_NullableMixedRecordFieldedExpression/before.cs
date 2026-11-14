#nullable enable
using Houtamelo.Spire;

[DiscriminatedUnion]
partial struct GenericCommand<T>
{
    [Variant] public static partial GenericCommand<T> Legacy(T item, string? note);
    [Variant] public record struct Modern(T item, string? label);
    [Variant] public static partial GenericCommand<T> Empty();
}

class Consumer
{
    int Read(GenericCommand<int>? command) => command switch
    {
        { IsEmpty: true } => 0,
    };
}
