//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a struct with multiple static fields and no instance fields.
[EnforceInitialization] //~ ERROR
public struct Detect_MultipleStaticFields_Struct
{
    public static int A;
    public static int B;
    public static int C;
}
