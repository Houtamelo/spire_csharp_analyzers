//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a variable-size array of a [EnforceInitialization] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_1DVariableSize
{
    public void Method(int n)
    {
        var arr = new EnforceInitializationStructWithAutoProperty[n]; //~ ERROR
    }
}
