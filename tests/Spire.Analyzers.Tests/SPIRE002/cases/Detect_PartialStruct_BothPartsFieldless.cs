//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a partial struct and neither partial declaration contains any instance fields.
[MustBeInit] //~ ERROR
public partial struct FieldlessPartial { }

public partial struct FieldlessPartial
{
    public void DoNothing() { }
}
