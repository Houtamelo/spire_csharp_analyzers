//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array in a local variable.
public class Detect_1DConstantSize_LocalVariable
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[5]; //~ ERROR
    }
}
