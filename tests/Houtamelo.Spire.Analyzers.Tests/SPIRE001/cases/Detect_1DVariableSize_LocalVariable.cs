//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D variable-size array in a local variable.
public class Detect_1DVariableSize_LocalVariable
{
    public void Method(int n)
    {
        var arr = new EnforceInitializationStruct[n]; //~ ERROR
    }
}
