//@ should_fail
// Ensure that SPIRE006 IS triggered in nullable-disabled context when clearing array of [EnforceInitialization] class.
#nullable disable
public class Detect_ArrayClearClass_NullableDisabled
{
    void Bad(EnforceInitializationClass[] arr)
    {
        Array.Clear(arr, 0, arr.Length); //~ ERROR
    }
}
