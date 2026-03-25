//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a multi-dimensional array of a [EnforceInitialization] struct whose only property is non-auto (no backing field).
public class NoReport_NonAutoPropertyStruct_MultiDimensional
{
    public void Method()
    {
        var arr = new EnforceInitializationStructWithNonAutoProperty[3, 4];
    }
}
