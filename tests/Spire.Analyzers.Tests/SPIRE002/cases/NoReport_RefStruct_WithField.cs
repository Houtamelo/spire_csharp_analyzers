//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a ref struct with an instance field.
[MustBeInit]
public ref struct RefStructWithField
{
    public int Value;
}
