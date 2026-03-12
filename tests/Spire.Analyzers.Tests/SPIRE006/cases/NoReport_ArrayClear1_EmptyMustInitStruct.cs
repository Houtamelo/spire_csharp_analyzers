//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear(arr) is called with an EmptyMustInitStruct[] (fieldless [MustBeInit] struct).
public class NoReport_ArrayClear1_EmptyMustInitStruct
{
    public void Method()
    {
        var arr = new EmptyMustInitStruct[5];
        Array.Clear(arr);
    }
}
