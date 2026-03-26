//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) is called with a EnforceInitializationReadonlyStruct[] argument.
public class Detect_ArrayClear1_ReadonlyStruct
{
    public void Method()
    {
        var arr = new EnforceInitializationReadonlyStruct[5];
        Array.Clear(arr); //~ ERROR
    }
}
