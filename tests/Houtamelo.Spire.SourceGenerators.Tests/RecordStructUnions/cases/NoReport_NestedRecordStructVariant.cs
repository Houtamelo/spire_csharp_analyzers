//@ should_pass
// Nested record-struct payloads remain valid inside ordinary struct unions.
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Customization
{
    [Variant] public record struct Slider(int amount);
}
