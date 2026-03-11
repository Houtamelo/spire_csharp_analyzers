//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a struct containing only a static field.
[MustBeInit] //~ ERROR
public struct Detect_StaticFieldOnly_Struct
{
    public static int X;
}
