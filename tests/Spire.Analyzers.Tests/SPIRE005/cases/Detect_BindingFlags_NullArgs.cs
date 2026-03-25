//@ should_fail
// Ensure that SPIRE005 IS triggered when BindingFlags overload args is null.
using System.Globalization;

public class Detect_BindingFlags_NullArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationStruct), BindingFlags.Default, null, null, null); //~ ERROR
    }
}
