//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance args is Array.Empty<object>().
public class Detect_Params_ArrayEmptyHelper
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationStruct), Array.Empty<object>()); //~ ERROR
    }
}
