//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a struct containing only a static field.
[EnforceInitialization] //~ ERROR
public struct Detect_StaticFieldOnly_Struct
{
    public static int X;
}
