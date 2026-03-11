//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a struct with a single explicit instance field.
[MustBeInit]
public struct NoReport_InstanceFieldOnly_Struct
{
    public int Value;
}
