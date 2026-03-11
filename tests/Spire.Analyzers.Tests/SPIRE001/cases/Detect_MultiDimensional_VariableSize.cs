//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a multi-dimensional array with a variable size.
public class Detect_MultiDimensional_VariableSize
{
    public void Method(int n, int m)
    {
        var arr = new MustInitStruct[n, m]; //~ ERROR
    }
}
