//@ should_pass
// Ensure that SPIRE006 is NOT triggered for Array.Clear(arr) when element type is EmptyMustInitStruct (fieldless, [MustBeInit]).
public class NoReport_Fieldless_ArrayClear1
{
    public void Method()
    {
        var arr = new EmptyMustInitStruct[5];
        Array.Clear(arr);
    }
}
