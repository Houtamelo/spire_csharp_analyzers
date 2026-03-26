//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a zero-length array.
public class NoReport_1DConstantSize_LocalVariable
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[0];
    }
}
