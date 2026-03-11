//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a partial struct whose instance field is declared in a different partial declaration.
[MustBeInit]
public partial struct PartialWithField { }

public partial struct PartialWithField
{
    public int Value;
}
