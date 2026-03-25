//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a multi-dimensional array with a constant size.
public class Detect_MultiDimensional_ConstantSize
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[2, 3]; //~ ERROR
    }
}
