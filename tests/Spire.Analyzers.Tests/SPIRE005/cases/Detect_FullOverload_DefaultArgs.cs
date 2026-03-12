//@ should_fail
// Ensure that SPIRE005 IS triggered when full overload args is default(object[]).
using System.Globalization;

public class Detect_FullOverload_DefaultArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), BindingFlags.Default, null, default(object[]), null, null); //~ ERROR
    }
}
