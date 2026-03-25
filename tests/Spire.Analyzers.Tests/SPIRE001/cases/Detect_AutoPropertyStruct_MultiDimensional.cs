//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a multi-dimensional array of a [EnforceInitialization] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_MultiDimensional
{
    public void Method()
    {
        var arr = new EnforceInitializationStructWithAutoProperty[3, 4]; //~ ERROR
    }
}
