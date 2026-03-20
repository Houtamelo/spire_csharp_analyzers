//@ should_fail
// Ensure that SPIRE006 IS triggered when clearing array of [MustBeInit] class.
#nullable enable
public class Detect_ArrayClearClass
{
    void Bad(MustInitClass[] arr)
    {
        Array.Clear(arr, 0, arr.Length); //~ ERROR
    }
}
