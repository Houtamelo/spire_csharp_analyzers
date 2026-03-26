//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a partial struct and neither partial declaration contains any instance fields.
[EnforceInitialization] //~ ERROR
public partial struct FieldlessPartial { }

public partial struct FieldlessPartial
{
    public void DoNothing() { }
}
