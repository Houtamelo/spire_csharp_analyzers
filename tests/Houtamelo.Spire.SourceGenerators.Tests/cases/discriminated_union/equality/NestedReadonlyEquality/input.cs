#nullable enable
using Houtamelo.Spire;

public static partial class Host
{
    [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
    public readonly partial struct Choice
    {
        [Variant] public static partial Choice A(string? value);
        [Variant] public static partial Choice B();
    }
}
