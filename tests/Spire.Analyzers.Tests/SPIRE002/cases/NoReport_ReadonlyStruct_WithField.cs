//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a readonly struct with a readonly instance field.
[MustBeInit]
public readonly struct ReadonlyStructWithField
{
    public readonly int Value;
}
