//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a readonly ref struct with an instance field.
[MustBeInit]
public readonly ref struct ReadonlyRefStructWithField
{
    public readonly int Value;
}
