//@ should_fail
// Ensure that SPIRE006 IS triggered when clearing array of [EnforceInitialization] class.
#nullable enable
public class Detect_ArrayClearClass
{
    void Bad(EnforceInitializationClass[] arr)
    {
        Array.Clear(arr, 0, arr.Length); //~ ERROR
    }
}
