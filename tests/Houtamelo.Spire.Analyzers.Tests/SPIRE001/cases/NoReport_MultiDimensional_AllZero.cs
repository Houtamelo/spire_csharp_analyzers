//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a multi-dimensional array where all dimensions are zero.
public class NoReport_MultiDimensional_AllZero
{
    public void Method()
    {
        var arr2d = new EnforceInitializationStruct[0, 0];
        var arr3d = new EnforceInitializationStruct[0, 0, 0];
    }
}
