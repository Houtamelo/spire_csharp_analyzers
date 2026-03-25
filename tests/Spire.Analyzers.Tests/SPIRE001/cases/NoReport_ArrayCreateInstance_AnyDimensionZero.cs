//@ should_pass
// Ensure that SPIRE001 is NOT triggered when calling Array.CreateInstance with any dimension zero.
public class NoReport_ArrayCreateInstance_AnyDimensionZero
{
    public void Method()
    {
        var arr = Array.CreateInstance(typeof(EnforceInitializationStruct), 0, 5);
        var arr2 = Array.CreateInstance(typeof(EnforceInitializationStruct), 3, 0);
    }
}
