//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a multi-dimensional array of a [EnforceInitialization] struct with no fields.
public class NoReport_EmptyStruct_MultiDimensional
{
    public void Method()
    {
        var arr = new EmptyEnforceInitializationStruct[3, 4];
    }
}
