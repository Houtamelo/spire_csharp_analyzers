//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a struct containing only a const field.
[MustBeInit] //~ ERROR
public struct Detect_ConstantOnly_Struct
{
    public const int X = 1;
}
