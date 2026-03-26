//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is used in a local variable declaration.
public class Detect_ExplicitDefault_LocalVariable
{
    public void Method()
    {
        var s = default(EnforceInitializationStruct); //~ ERROR
    }
}
