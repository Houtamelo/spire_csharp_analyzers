//@ codefix: Spire.Union.RemoveRecord
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct CombinedState
{
    [Variant] public record struct Value(int amount);
}

public partial struct CombinedState
{
    [Variant] public static partial CombinedState Empty();
}
