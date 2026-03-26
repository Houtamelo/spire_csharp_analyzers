//@ should_fail
// Ensure that SPIRE001 IS triggered when creating an array of a [EnforceInitialization] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_1DConstantSize
{
    public void Method()
    {
        var arr = new EnforceInitializationStructWithAutoProperty[5]; //~ ERROR
    }
}
