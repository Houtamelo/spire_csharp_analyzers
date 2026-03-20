//@ should_pass
// Ensure that SPIRE006 is NOT triggered when clearing array of class without [MustBeInit].
#nullable enable
public class NoReport_ArrayClearPlainClass
{
    void Ok(object[] arr)
    {
        Array.Clear(arr, 0, arr.Length);
    }
}
