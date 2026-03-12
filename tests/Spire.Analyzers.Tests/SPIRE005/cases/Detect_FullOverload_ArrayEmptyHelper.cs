//@ should_fail
// Ensure that SPIRE005 IS triggered when full overload args is Array.Empty<object>().
using System.Globalization;

public class Detect_FullOverload_ArrayEmptyHelper
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), BindingFlags.Default, null, Array.Empty<object>(), null, null); //~ ERROR
    }
}
