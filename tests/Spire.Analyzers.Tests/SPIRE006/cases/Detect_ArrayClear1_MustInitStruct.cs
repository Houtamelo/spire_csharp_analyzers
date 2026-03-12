//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) is called with a MustInitStruct[] argument.
public class Detect_ArrayClear1_MustInitStruct
{
    public void Method()
    {
        var arr = new MustInitStruct[5];
        Array.Clear(arr); //~ ERROR
    }
}
