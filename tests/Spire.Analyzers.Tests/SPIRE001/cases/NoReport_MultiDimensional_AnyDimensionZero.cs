//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a multi-dimensional array where any dimension is zero.
public class NoReport_MultiDimensional_AnyDimensionZero
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[0, 5];
        var arr2 = new EnforceInitializationStruct[3, 0];
        var arr3 = new EnforceInitializationStruct[0, 5, 3];
    }
}
