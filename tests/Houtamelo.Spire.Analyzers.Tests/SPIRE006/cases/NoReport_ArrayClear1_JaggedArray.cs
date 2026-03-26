//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear(arr) is called with a EnforceInitializationStruct[][] — element type is EnforceInitializationStruct[] which is a reference type.
public class NoReport_ArrayClear1_JaggedArray
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[3][];
        Array.Clear(arr);
    }
}
