//@ should_pass
// Ensure that SPIRE001 is NOT triggered when calling Array.CreateInstance with size zero.
public class NoReport_ArrayCreateInstance_ZeroSize
{
    public void Method()
    {
        var arr = Array.CreateInstance(typeof(MustInitStruct), 0);
    }
}
