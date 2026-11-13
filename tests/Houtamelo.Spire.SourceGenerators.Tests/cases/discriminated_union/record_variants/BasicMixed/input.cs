using Houtamelo.Spire;

namespace Demo
{
    [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
    public partial struct Customization
    {
        [Variant] public record struct Item(int value);
        [Variant] public static partial Customization Empty();
    }
}
