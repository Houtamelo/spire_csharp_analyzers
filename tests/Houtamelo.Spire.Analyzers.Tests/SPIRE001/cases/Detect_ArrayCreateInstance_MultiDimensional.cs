//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.CreateInstance for a multi-dimensional array.
public class Detect_ArrayCreateInstance_MultiDimensional
{
    public void Method()
    {
        var arr = Array.CreateInstance(typeof(EnforceInitializationStruct), 2, 3); //~ ERROR
    }
}
