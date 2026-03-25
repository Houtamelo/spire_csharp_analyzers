//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.CreateInstance with a constant size.
public class Detect_ArrayCreateInstance_ConstantSize
{
    public void Method()
    {
        var arr = Array.CreateInstance(typeof(EnforceInitializationStruct), 5); //~ ERROR
    }
}
