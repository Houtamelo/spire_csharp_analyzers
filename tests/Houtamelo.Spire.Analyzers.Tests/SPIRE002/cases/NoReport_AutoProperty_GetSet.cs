//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a struct with a standard auto-property, because it generates a backing field.
[EnforceInitialization]
public struct NoReport_AutoProperty_GetSet_Struct
{
    public int Value { get; set; }
}
