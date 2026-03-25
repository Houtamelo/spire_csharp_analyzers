//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a variable-size array of a [EnforceInitialization] struct with no fields.
public class NoReport_EmptyStruct_1DVariableSize
{
    public void Method(int n)
    {
        var arr = new EmptyEnforceInitializationStruct[n];
    }
}
