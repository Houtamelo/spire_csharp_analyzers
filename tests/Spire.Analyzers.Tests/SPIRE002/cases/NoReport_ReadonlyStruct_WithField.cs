//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a readonly struct with a readonly instance field.
[EnforceInitialization]
public readonly struct ReadonlyStructWithField
{
    public readonly int Value;
}
