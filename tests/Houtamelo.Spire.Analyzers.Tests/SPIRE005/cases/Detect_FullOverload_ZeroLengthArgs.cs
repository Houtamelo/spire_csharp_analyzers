//@ should_fail
// Ensure that SPIRE005 IS triggered when full overload args is new object[0].
using System.Globalization;

public class Detect_FullOverload_ZeroLengthArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationStruct), BindingFlags.Default, null, new object[0], null, null); //~ ERROR
    }
}
