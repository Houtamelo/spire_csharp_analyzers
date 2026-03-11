//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a struct with a get-only auto-property, because it generates a backing field.
[MustBeInit]
public struct NoReport_AutoProperty_GetOnly_Struct
{
    public int Value { get; }
}
