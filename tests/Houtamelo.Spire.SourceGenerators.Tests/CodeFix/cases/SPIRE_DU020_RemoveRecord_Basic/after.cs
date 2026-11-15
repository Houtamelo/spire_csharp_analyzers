//@ codefix: Spire.Union.RemoveRecord
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Customization
{
    [Variant] public record struct Slider(int amount);
    [Variant] public static partial Customization Empty();
}
