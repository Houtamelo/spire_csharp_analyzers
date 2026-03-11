//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.CreateInstance for a multi-dimensional array with a variable size.
public class Detect_ArrayCreateInstance_MultiDimensional_VariableSize
{
    public void Method(int n, int m)
    {
        var arr = Array.CreateInstance(typeof(MustInitStruct), n, m); //~ ERROR
    }
}
