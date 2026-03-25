//@ should_pass
// Ensure that SPIRE006 is NOT triggered when clearing array of nullable [EnforceInitialization] class.
#nullable enable
public class NoReport_ArrayClearNullableClass
{
    void Ok(EnforceInitializationClass?[] arr)
    {
        Array.Clear(arr, 0, arr.Length);
    }
}
