//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a struct with a get-only auto-property, because it generates a backing field.
[EnforceInitialization]
public struct NoReport_AutoProperty_GetOnly_Struct
{
    public int Value { get; }
}
