//@ should_pass
// Ensure that SPIRE001 is NOT triggered when calling Array.CreateInstance on a plain struct.
public class NoReport_ArrayCreateInstance_PlainStruct
{
    public void Method()
    {
        var arr = Array.CreateInstance(typeof(PlainStruct), 5);
    }
}
