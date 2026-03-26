//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a struct containing both a static field and a constant but no instance fields.
[EnforceInitialization] //~ ERROR
public struct Detect_StaticAndConstOnly_Struct
{
    public static int X;
    public const int Y = 1;
}
