//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a struct with an init-only auto-property, because it generates a backing field.
[MustBeInit]
public struct NoReport_AutoProperty_InitOnly_Struct
{
    public int Value { get; init; }
}
