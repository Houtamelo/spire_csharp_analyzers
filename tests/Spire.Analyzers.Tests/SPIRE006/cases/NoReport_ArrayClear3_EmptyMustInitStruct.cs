//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear(arr, 0, n) is called with an EmptyMustInitStruct[] (fieldless [MustBeInit] struct).
public class NoReport_ArrayClear3_EmptyMustInitStruct
{
    public void Method()
    {
        var arr = new EmptyMustInitStruct[5];
        Array.Clear(arr, 0, arr.Length);
    }
}
