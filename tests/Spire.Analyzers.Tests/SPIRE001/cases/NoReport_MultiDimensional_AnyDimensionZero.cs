//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a multi-dimensional array where any dimension is zero.
public class NoReport_MultiDimensional_AnyDimensionZero
{
    public void Method()
    {
        var arr = new MustInitStruct[0, 5];
        var arr2 = new MustInitStruct[3, 0];
        var arr3 = new MustInitStruct[0, 5, 3];
    }
}
