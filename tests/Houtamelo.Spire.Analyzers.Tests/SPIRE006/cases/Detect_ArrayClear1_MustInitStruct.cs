//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) is called with a EnforceInitializationStruct[] argument.
public class Detect_ArrayClear1_EnforceInitializationStruct
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[5];
        Array.Clear(arr); //~ ERROR
    }
}
