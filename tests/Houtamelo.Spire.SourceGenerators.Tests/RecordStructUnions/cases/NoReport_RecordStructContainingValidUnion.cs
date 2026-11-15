//@ should_pass
// A record-struct containing type may contain a valid ordinary struct union.
using Houtamelo.Spire;

public record struct Container
{
    [DiscriminatedUnion]
    public partial struct State
    {
        [Variant] public static partial State Ready();
    }
}
