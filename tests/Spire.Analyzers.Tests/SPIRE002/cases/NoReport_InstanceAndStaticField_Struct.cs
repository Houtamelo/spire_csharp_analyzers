//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a struct that has both an instance field and a static field.
[EnforceInitialization]
public struct NoReport_InstanceAndStaticField_Struct
{
    public int InstanceField;
    public static int StaticField;
}
