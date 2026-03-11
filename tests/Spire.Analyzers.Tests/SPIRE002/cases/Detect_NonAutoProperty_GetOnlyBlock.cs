//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a struct whose only member is a get-only property with block accessor.
[MustBeInit] //~ ERROR
public struct Detect_NonAutoProperty_GetOnlyBlock_Struct
{
    public int Value { get => 42; }
}
