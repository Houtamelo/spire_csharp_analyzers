//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.CreateInstance with a variable size.
public class Detect_ArrayCreateInstance_VariableSize
{
    public void Method(int n)
    {
        var arr = Array.CreateInstance(typeof(MustInitStruct), n); //~ ERROR
    }
}
