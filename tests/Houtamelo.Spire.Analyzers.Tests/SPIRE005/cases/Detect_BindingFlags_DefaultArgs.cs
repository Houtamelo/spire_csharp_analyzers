//@ should_fail
// Ensure that SPIRE005 IS triggered when BindingFlags overload args is default(object[]).
using System.Globalization;

public class Detect_BindingFlags_DefaultArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationStruct), BindingFlags.Default, null, default(object[]), null); //~ ERROR
    }
}
