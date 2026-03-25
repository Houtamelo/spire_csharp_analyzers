//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a struct with a single explicit instance field.
[EnforceInitialization]
public struct NoReport_InstanceFieldOnly_Struct
{
    public int Value;
}
