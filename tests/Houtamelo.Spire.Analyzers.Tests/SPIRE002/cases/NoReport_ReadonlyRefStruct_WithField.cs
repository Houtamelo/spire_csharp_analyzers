//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a readonly ref struct with an instance field.
[EnforceInitialization]
public readonly ref struct ReadonlyRefStructWithField
{
    public readonly int Value;
}
