//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a struct nested inside another struct and that inner struct has no instance fields.
public struct OuterStruct
{
    [MustBeInit] //~ ERROR
    public struct InnerStruct { }
}
