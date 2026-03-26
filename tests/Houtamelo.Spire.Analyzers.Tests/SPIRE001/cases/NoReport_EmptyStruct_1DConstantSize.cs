//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating an array of a [EnforceInitialization] struct with no fields.
public class NoReport_EmptyStruct_1DConstantSize
{
    public void Method()
    {
        var arr = new EmptyEnforceInitializationStruct[5];
    }
}
