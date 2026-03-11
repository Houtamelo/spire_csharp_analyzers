//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a struct containing both a static field and a constant but no instance fields.
[MustBeInit] //~ ERROR
public struct Detect_StaticAndConstOnly_Struct
{
    public static int X;
    public const int Y = 1;
}
