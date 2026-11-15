//@ should_fail
// JSON configuration does not change the outer union declaration rule.
using Houtamelo.Spire;

[DiscriminatedUnion(JsonLibrary.SystemTextJson)]
public partial record struct JsonUnion //~ ERROR
{
    [Variant] public record struct Value(int amount);
}
