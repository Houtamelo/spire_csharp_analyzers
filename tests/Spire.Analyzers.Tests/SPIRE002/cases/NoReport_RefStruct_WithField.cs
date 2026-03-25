//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a ref struct with an instance field.
[EnforceInitialization]
public ref struct RefStructWithField
{
    public int Value;
}
