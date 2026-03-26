//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a struct with multiple non-auto expression-bodied properties and no instance fields.
[EnforceInitialization] //~ ERROR
public struct Detect_NonAutoProperty_MultipleProperties_Struct
{
    public int X => 1;
    public int Y => 2;
    public int Z => 3;
}
