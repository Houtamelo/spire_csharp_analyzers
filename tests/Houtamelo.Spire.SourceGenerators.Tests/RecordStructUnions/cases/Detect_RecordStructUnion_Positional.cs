//@ should_fail
// Positional parameters do not make a record-struct union valid.
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial record struct PositionalUnion(int seed) //~ ERROR
{
    [Variant] public record struct Value(int amount);
}
