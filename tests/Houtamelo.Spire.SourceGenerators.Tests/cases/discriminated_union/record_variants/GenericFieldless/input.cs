#nullable enable
using Houtamelo.Spire;

namespace Demo
{
    [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
    public partial struct Choice<T>
        where T : class
    {
        [Variant] public record struct Some(T value);
        [Variant] public record struct None;
        [Variant] public static partial Choice<T> Other(T value);
    }
}
