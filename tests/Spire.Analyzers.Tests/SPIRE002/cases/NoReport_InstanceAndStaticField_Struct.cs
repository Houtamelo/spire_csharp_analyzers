//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a struct that has both an instance field and a static field.
[MustBeInit]
public struct NoReport_InstanceAndStaticField_Struct
{
    public int InstanceField;
    public static int StaticField;
}
