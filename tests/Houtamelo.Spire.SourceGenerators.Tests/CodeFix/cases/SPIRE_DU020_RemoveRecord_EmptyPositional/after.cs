//@ codefix: Spire.Union.RemoveRecord
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct EmptyState
{
    [Variant] public static partial EmptyState None();
}
